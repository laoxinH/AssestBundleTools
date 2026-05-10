using System.Diagnostics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.UI.Dispatching;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class D3D11SpineBatchPrewarmer : IDisposable
{
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly SpineAnimationRenderBatchCache _batchCache;
    private readonly D3D11SpineRenderDiagnostics _diagnostics;
    private readonly Func<SpineAnimationFrameDto?> _getCurrentFrame;
    private readonly Func<int> _getTexturePageVersion;
    private readonly Action _invalidate;
    private IReadOnlyList<SpineAnimationFrameDto> _frames =
        Array.Empty<SpineAnimationFrameDto>();
    private CancellationTokenSource? _cancellation;
    private int _generation;
    private bool _isActive;

    public D3D11SpineBatchPrewarmer(
        DispatcherQueue dispatcherQueue,
        SpineAnimationRenderBatchCache batchCache,
        D3D11SpineRenderDiagnostics diagnostics,
        Func<SpineAnimationFrameDto?> getCurrentFrame,
        Func<int> getTexturePageVersion,
        Action invalidate)
    {
        _dispatcherQueue = dispatcherQueue;
        _batchCache = batchCache;
        _diagnostics = diagnostics;
        _getCurrentFrame = getCurrentFrame;
        _getTexturePageVersion = getTexturePageVersion;
        _invalidate = invalidate;
    }

    public void SetFrames(IReadOnlyList<SpineAnimationFrameDto> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        if (!ReferenceEquals(_frames, frames))
        {
            Cancel();
        }

        _frames = frames;
    }

    public bool CanPrewarmCurrentFrame(int texturePageSizeCount) =>
        _getCurrentFrame() is not null &&
        _frames.Count > 0 &&
        texturePageSizeCount > 0;

    public bool TryStart(
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePageSizes,
        int texturePageVersion)
    {
        ArgumentNullException.ThrowIfNull(texturePageSizes);

        if (_frames.Count == 0 || texturePageSizes.Count == 0)
        {
            return false;
        }

        if (_isActive)
        {
            return true;
        }

        var frames = ResolvePrewarmFrames();
        if (frames.Count == 0)
        {
            return false;
        }

        _cancellation?.Cancel();
        _cancellation?.Dispose();
        var generation = ++_generation;
        var texturePageSnapshot = new Dictionary<string, SpineAnimationTexturePageSize>(
            texturePageSizes,
            StringComparer.Ordinal);
        var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        _cancellation = cancellation;
        _isActive = true;
        _ = Task.Run(
            () => PrewarmAsync(frames, texturePageSnapshot, texturePageVersion, generation, cancellationToken),
            cancellationToken);
        return true;
    }

    public void Cancel()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
        _isActive = false;
        _generation++;
    }

    public void Dispose()
    {
        Cancel();
    }

    private async Task PrewarmAsync(
        IReadOnlyList<SpineAnimationFrameDto> frames,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePageSizes,
        int texturePageVersion,
        int generation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var prewarmedCount = 0;
        try
        {
            foreach (var frame in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batches = SpineAnimationVertexBatchProjector.Project(frame, texturePageSizes);
                if (!await StoreAsync(frame, batches, texturePageVersion, generation, cancellationToken))
                {
                    return;
                }

                prewarmedCount++;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            stopwatch.Stop();
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (generation != _generation)
                {
                    return;
                }

                _isActive = false;
                if (texturePageVersion != _getTexturePageVersion())
                {
                    return;
                }

                _diagnostics.BatchPrewarmCompleted(prewarmedCount, frames.Count, stopwatch.Elapsed);
            });
        }
    }

    private Task<bool> StoreAsync(
        SpineAnimationFrameDto frame,
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        int texturePageVersion,
        int generation,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var canStore = generation == _generation &&
                                   texturePageVersion == _getTexturePageVersion();
                    if (canStore)
                    {
                        _batchCache.StoreVertexBatches(frame, texturePageVersion, batches);
                        if (ReferenceEquals(frame, _getCurrentFrame()))
                        {
                            _invalidate();
                        }
                    }

                    completion.TrySetResult(canStore);
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            completion.TrySetCanceled(cancellationToken);
        }

        return completion.Task;
    }

    private IReadOnlyList<SpineAnimationFrameDto> ResolvePrewarmFrames()
    {
        var frames = new List<SpineAnimationFrameDto>(_frames.Count + 1);
        var currentFrame = _getCurrentFrame();
        if (currentFrame is not null)
        {
            frames.Add(currentFrame);
        }

        foreach (var frame in _frames)
        {
            if (!frames.Any(existing => ReferenceEquals(existing, frame)))
            {
                frames.Add(frame);
            }
        }

        return frames;
    }
}
