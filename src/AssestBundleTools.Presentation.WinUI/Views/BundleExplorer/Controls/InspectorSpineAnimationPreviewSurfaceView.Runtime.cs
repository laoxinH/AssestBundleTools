using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView
{
    private readonly ISpineAnimationRuntimePlaybackService? _runtimePlaybackService =
        ResolveRuntimePlaybackService();

    private ISpineAnimationRuntimePlaybackSession? _runtimePlaybackSession;
    private SpineAnimationRuntimePlaybackSourceDto? _runtimePlaybackSource;
    private CancellationTokenSource? _runtimeSessionCancellation;
    private CancellationTokenSource? _runtimeFrameCancellation;
    private bool _isRenderingLoopAttached;
    private bool _isRuntimeSessionCreationActive;
    private bool _runtimeFrameRequestInFlight;
    private bool _runtimeFrameRequestPending;
    private int _runtimeSessionGeneration;

    private void StartRenderingLoop()
    {
        _lastTickUtc = DateTimeOffset.UtcNow;
        if (_isRenderingLoopAttached)
        {
            return;
        }

        CompositionTarget.Rendering += OnCompositionTargetRendering;
        _isRenderingLoopAttached = true;
    }

    private void StopRenderingLoop()
    {
        if (!_isRenderingLoopAttached)
        {
            return;
        }

        CompositionTarget.Rendering -= OnCompositionTargetRendering;
        _isRenderingLoopAttached = false;
    }

    private void OnCompositionTargetRendering(object? sender, object e)
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = now - _lastTickUtc;
        _lastTickUtc = now;
        if (!_isPreviewRunning ||
            ViewModel is not { IsPlaying: true } viewModel)
        {
            return;
        }

        if (_isPlaybackSliderScrubbing)
        {
            return;
        }

        var playbackTimeChanged = viewModel.Advance(elapsed);
        if (viewModel.HasRuntimePlayback && _runtimePlaybackSession is not null)
        {
            if (playbackTimeChanged)
            {
                QueueRuntimeFrameRefresh();
            }

            return;
        }

        if (SyncRenderFrame())
        {
            InvalidatePreviewSurface();
        }
    }

    private void RefreshRuntimePlaybackSession()
    {
        var source = ViewModel?.Preview?.PlaybackSource;
        if (!_isPreviewRunning ||
            _runtimePlaybackService is null ||
            source is null)
        {
            DisposeRuntimePlaybackSession();
            return;
        }

        if (ReferenceEquals(_runtimePlaybackSource, source) &&
            (_runtimePlaybackSession is not null || _isRuntimeSessionCreationActive))
        {
            QueueRuntimeFrameRefresh();
            return;
        }

        DisposeRuntimePlaybackSession();
        _runtimePlaybackSource = source;
        _runtimeSessionCancellation = new CancellationTokenSource();
        _isRuntimeSessionCreationActive = true;
        var generation = ++_runtimeSessionGeneration;
        _ = CreateRuntimePlaybackSessionAsync(
            source,
            generation,
            _runtimeSessionCancellation.Token);
    }

    private async Task CreateRuntimePlaybackSessionAsync(
        SpineAnimationRuntimePlaybackSourceDto source,
        int generation,
        CancellationToken cancellationToken)
    {
        SpineAnimationRuntimePlaybackSessionResultDto result;
        try
        {
            result = await _runtimePlaybackService!.TryCreateSessionAsync(source, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result = SpineAnimationRuntimePlaybackSessionResultDto.Cancelled;
        }
        catch
        {
            result = SpineAnimationRuntimePlaybackSessionResultDto.NotHandled;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _runtimeSessionGeneration ||
                !ReferenceEquals(_runtimePlaybackSource, source))
            {
                result.Session?.Dispose();
                return;
            }

            _isRuntimeSessionCreationActive = false;
            if (result.IsHandled && result.Session is not null)
            {
                _runtimePlaybackSession = result.Session;
                QueueRuntimeFrameRefresh();
                return;
            }

            _runtimePlaybackSession?.Dispose();
            _runtimePlaybackSession = null;
        });
    }

    private void QueueRuntimeFrameRefresh()
    {
        if (!_isPreviewRunning ||
            _runtimePlaybackSession is null ||
            ViewModel is not { HasRuntimePlayback: true } viewModel)
        {
            return;
        }

        if (_runtimeFrameRequestInFlight)
        {
            _runtimeFrameRequestPending = true;
            return;
        }

        _runtimeFrameCancellation?.Dispose();
        _runtimeFrameCancellation = new CancellationTokenSource();
        _runtimeFrameRequestInFlight = true;
        var session = _runtimePlaybackSession;
        var request = CreateRuntimeFrameRequest(viewModel);
        var generation = _runtimeSessionGeneration;
        _ = ResolveRuntimeFrameAsync(
            session,
            request,
            generation,
            _runtimeFrameCancellation.Token);
    }

    private async Task ResolveRuntimeFrameAsync(
        ISpineAnimationRuntimePlaybackSession session,
        SpineAnimationRuntimePlaybackFrameRequestDto request,
        int generation,
        CancellationToken cancellationToken)
    {
        SpineAnimationRuntimePlaybackFrameResultDto result;
        try
        {
            result = await session.GetFrameAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            result = SpineAnimationRuntimePlaybackFrameResultDto.Cancelled;
        }
        catch
        {
            result = SpineAnimationRuntimePlaybackFrameResultDto.NotHandled;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (generation != _runtimeSessionGeneration ||
                !ReferenceEquals(session, _runtimePlaybackSession))
            {
                return;
            }

            _runtimeFrameRequestInFlight = false;
            _runtimeFrameCancellation?.Dispose();
            _runtimeFrameCancellation = null;

            if (result.IsHandled &&
                result.Frame is not null &&
                ViewModel is { HasRuntimePlayback: true } viewModel)
            {
                if (IsRuntimeFrameRequestCurrent(request, viewModel))
                {
                    if (result.RuntimeDiagnostics is not null)
                    {
                        _diagnostics?.RuntimeFrame(
                            request.AnimationName,
                            request.SkinName,
                            result.PlaybackTime,
                            result.RuntimeDiagnostics);
                    }

                    viewModel.ApplyRuntimeFrame(result.Frame);
                    if (SyncRenderFrame())
                    {
                        InvalidatePreviewSurface();
                    }
                }
                else if (!_runtimeFrameRequestPending)
                {
                    _runtimeFrameRequestPending = true;
                }
            }

            if (_runtimeFrameRequestPending)
            {
                _runtimeFrameRequestPending = false;
                QueueRuntimeFrameRefresh();
            }
        });
    }

    private void DisposeRuntimePlaybackSession()
    {
        _runtimeSessionGeneration++;
        _isRuntimeSessionCreationActive = false;
        _runtimeFrameRequestInFlight = false;
        _runtimeFrameRequestPending = false;
        _runtimeSessionCancellation?.Cancel();
        _runtimeSessionCancellation?.Dispose();
        _runtimeSessionCancellation = null;
        _runtimeFrameCancellation?.Cancel();
        _runtimeFrameCancellation?.Dispose();
        _runtimeFrameCancellation = null;
        _runtimePlaybackSession?.Dispose();
        _runtimePlaybackSession = null;
        _runtimePlaybackSource = null;
    }

    private static SpineAnimationRuntimePlaybackFrameRequestDto CreateRuntimeFrameRequest(
        InspectorPreviewSpineAnimationViewModel viewModel) =>
        new(
            viewModel.SelectedAnimationName,
            viewModel.SelectedSkinName,
            viewModel.PlaybackTime,
            viewModel.IsShowingSetupPose);

    private static bool IsRuntimeFrameRequestCurrent(
        SpineAnimationRuntimePlaybackFrameRequestDto request,
        InspectorPreviewSpineAnimationViewModel viewModel) =>
        string.Equals(request.AnimationName, viewModel.SelectedAnimationName, StringComparison.Ordinal) &&
        string.Equals(request.SkinName, viewModel.SelectedSkinName, StringComparison.Ordinal) &&
        request.ShowSetupPose == viewModel.IsShowingSetupPose;

    private static bool ShouldRefreshRuntimePlaybackSession(string? propertyName) =>
        propertyName is nameof(InspectorPreviewSpineAnimationViewModel.Preview);

    private static bool ShouldRefreshRuntimeFrame(string? propertyName) =>
        propertyName is nameof(InspectorPreviewSpineAnimationViewModel.SelectedAnimationName) or
            nameof(InspectorPreviewSpineAnimationViewModel.SelectedSkinName) or
            nameof(InspectorPreviewSpineAnimationViewModel.PlaybackTime) or
            nameof(InspectorPreviewSpineAnimationViewModel.IsPlaying);

    private static ISpineAnimationRuntimePlaybackService? ResolveRuntimePlaybackService()
    {
        try
        {
            return App.CurrentApp.SpineAnimationRuntimePlaybackService;
        }
        catch
        {
            return null;
        }
    }
}
