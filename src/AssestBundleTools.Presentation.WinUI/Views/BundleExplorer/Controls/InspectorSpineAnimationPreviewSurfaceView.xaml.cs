using System.ComponentModel;
using System.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView : UserControl
{
    private static readonly TimeSpan DeferredResourceReleaseDelay = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _deferredResourceReleaseTimer;
    private readonly ISpineAnimationPreviewRenderBackend _renderBackend;
    private readonly D3D11SpineRenderDiagnostics? _diagnostics;
    private InspectorPreviewSpineAnimationViewModel? _observedViewModel;
    private SpineAnimationFrameDto? _lastSyncedFrame;
    private DateTimeOffset _lastTickUtc;
    private bool _isPreviewRunning;
    private bool _isBindingsUpdateQueued;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewSpineAnimationViewModel),
            typeof(InspectorSpineAnimationPreviewSurfaceView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public static readonly DependencyProperty IsPreviewActiveProperty =
        DependencyProperty.Register(
            nameof(IsPreviewActive),
            typeof(bool),
            typeof(InspectorSpineAnimationPreviewSurfaceView),
            new PropertyMetadata(true, OnIsPreviewActiveChanged));

    public static readonly DependencyProperty UseSquareViewportProperty =
        DependencyProperty.Register(
            nameof(UseSquareViewport),
            typeof(bool),
            typeof(InspectorSpineAnimationPreviewSurfaceView),
            new PropertyMetadata(true, OnUseSquareViewportChanged));

    public InspectorSpineAnimationPreviewSurfaceView()
    {
        InitializeComponent();
        _diagnostics = CreateDiagnostics();
        _renderBackend = SpineAnimationPreviewRenderBackendFactory.Create(
            SpineCanvas,
            SpineSwapChainPanel);
        _deferredResourceReleaseTimer = new DispatcherTimer
        {
            Interval = DeferredResourceReleaseDelay
        };
        _deferredResourceReleaseTimer.Tick += OnDeferredResourceReleaseTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RegisterPlaybackSliderInputHandlers();
        ApplyLocalizedText();
    }

    public InspectorPreviewSpineAnimationViewModel? ViewModel
    {
        get => (InspectorPreviewSpineAnimationViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public bool IsPreviewActive
    {
        get => (bool)GetValue(IsPreviewActiveProperty);
        set => SetValue(IsPreviewActiveProperty, value);
    }

    public bool UseSquareViewport
    {
        get => (bool)GetValue(UseSquareViewportProperty);
        set => SetValue(UseSquareViewportProperty, value);
    }

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorSpineAnimationPreviewSurfaceView view)
        {
            return;
        }

        view.DetachObservedViewModel(args.OldValue as InspectorPreviewSpineAnimationViewModel);
        view.CancelPlaybackSliderScrub();
        view.RestoreViewportStateFromViewModel(args.NewValue as InspectorPreviewSpineAnimationViewModel);
        view.ResetAutoFitReferenceCapture();
        view._renderBackend.ResetFrameCache();
        if (view._isPreviewRunning)
        {
            view.AttachObservedViewModel(args.NewValue as InspectorPreviewSpineAnimationViewModel);
            view.RefreshTexturePages();
            view.RefreshRuntimePlaybackSession();
            view.SyncRenderFrame();
            view.RefreshPrewarmFrames();
            view.RefreshBackgroundMode();
        }

        view.UpdateBindingsNow();
        view.RefreshSpineBackgroundActionToolTip();
        if (view._isPreviewRunning)
        {
            view.InvalidatePreviewSurface();
        }
    }

    private static void OnIsPreviewActiveChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is InspectorSpineAnimationPreviewSurfaceView view)
        {
            view.UpdatePreviewActivation();
        }
    }

    private static void OnUseSquareViewportChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is InspectorSpineAnimationPreviewSurfaceView view)
        {
            view.RefreshPreviewViewportFrameSizing();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdatePreviewActivation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopPreview();
    }

    private void UpdatePreviewActivation()
    {
        if (IsLoaded && IsPreviewActive)
        {
            StartPreview();
            return;
        }

        StopPreview();
    }

    private void StartPreview()
    {
        if (_isPreviewRunning)
        {
            return;
        }

        _isPreviewRunning = true;
        _deferredResourceReleaseTimer.Stop();
        ResetAutoFitReferenceCapture();
        AttachObservedViewModel(ViewModel);
        RefreshTexturePages();
        RefreshRuntimePlaybackSession();
        SyncRenderFrame();
        RefreshPrewarmFrames();
        RestoreViewportStateFromViewModel(ViewModel);
        RefreshBackgroundMode();
        StartRenderingLoop();
        UpdateBindingsNow();
        InvalidatePreviewSurface();
    }

    private void StopPreview()
    {
        if (!_isPreviewRunning)
        {
            return;
        }

        _isPreviewRunning = false;
        CancelPlaybackSliderScrub();
        CancelFrameSequenceExport();
        StopRenderingLoop();
        DisposeRuntimePlaybackSession();
        DetachObservedViewModel(_observedViewModel);
        _renderBackend.SetCurrentFrame(null);
        ResetAutoFitReferenceCapture();
        _lastSyncedFrame = null;
        _isViewportDragging = false;
        _deferredResourceReleaseTimer.Stop();
        _deferredResourceReleaseTimer.Start();
    }

    private void OnDeferredResourceReleaseTimerTick(object? sender, object e)
    {
        _deferredResourceReleaseTimer.Stop();
        if (_isPreviewRunning)
        {
            return;
        }

        _renderBackend.ClearTexturePages();
        _renderBackend.ReleaseResources();
    }

    private void ApplyLocalizedText()
    {
        ToolTipService.SetToolTip(
            AnimationComboBox,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewAnimationSelectorToolTip));
        ToolTipService.SetToolTip(
            SkinComboBox,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewSkinSelectorToolTip));
        ToolTipService.SetToolTip(
            PlaybackSpeedComboBox,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewSpeedToolTip));
        ToolTipService.SetToolTip(
            PlaybackSlider,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewSeekToolTip));
        ToolTipService.SetToolTip(
            SetupPoseButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewSetupPoseToolTip));
        ToolTipService.SetToolTip(
            StepPreviousButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewStepPreviousToolTip));
        ToolTipService.SetToolTip(
            PlayPauseButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewPlayPauseToolTip));
        ToolTipService.SetToolTip(
            StepNextButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewStepNextToolTip));
        ToolTipService.SetToolTip(
            RestartButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewRestartToolTip));
        ToolTipService.SetToolTip(
            ExportFramesButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewExportFramesToolTip));
        RefreshSpineBackgroundActionToolTip();
        ToolTipService.SetToolTip(
            ZoomOutViewportButton,
            UiText.Get(ResourceKeys.InspectorPreviewImageZoomOutToolTip));
        ToolTipService.SetToolTip(
            ZoomInViewportButton,
            UiText.Get(ResourceKeys.InspectorPreviewImageZoomInToolTip));
        ToolTipService.SetToolTip(
            ResetViewportButton,
            UiText.Get(ResourceKeys.InspectorSpineAnimationPreviewResetViewportToolTip));
    }

    private void AttachObservedViewModel(InspectorPreviewSpineAnimationViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
    }

    private void DetachObservedViewModel(InspectorPreviewSpineAnimationViewModel? viewModel)
    {
        if (viewModel is null || !ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        _observedViewModel = null;
    }

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var texturePagesChanged =
            e.PropertyName is nameof(InspectorPreviewSpineAnimationViewModel.TexturePageContents);
        var shouldRefreshPrewarmFrames = ShouldRefreshPrewarmFrames(e.PropertyName);
        var shouldRefreshRuntimeSession = ShouldRefreshRuntimePlaybackSession(e.PropertyName);
        var shouldRefreshRuntimeFrame = ShouldRefreshRuntimeFrame(e.PropertyName);
        var shouldCaptureRuntimeCurrentFrameAutoFitReference =
            ShouldCaptureRuntimeCurrentFrameAutoFitReference(e.PropertyName);
        var shouldUpdateBindings = ShouldUpdateBindings(e.PropertyName);
        var shouldRefreshBackgroundMode = ShouldRefreshBackgroundMode(e.PropertyName);
        var shouldResetRenderFrameCache = ShouldResetRenderFrameCache(e.PropertyName);

        LogInteractionForProperty(e.PropertyName);

        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyObservedViewModelPropertyChange(
                texturePagesChanged,
                shouldRefreshPrewarmFrames,
                shouldRefreshRuntimeSession,
                shouldRefreshRuntimeFrame,
                shouldCaptureRuntimeCurrentFrameAutoFitReference,
                shouldRefreshBackgroundMode,
                shouldResetRenderFrameCache,
                shouldUpdateBindings);
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
            ApplyObservedViewModelPropertyChange(
                texturePagesChanged,
                shouldRefreshPrewarmFrames,
                shouldRefreshRuntimeSession,
                shouldRefreshRuntimeFrame,
                shouldCaptureRuntimeCurrentFrameAutoFitReference,
                shouldRefreshBackgroundMode,
                shouldResetRenderFrameCache,
                shouldUpdateBindings));
    }

    private void ApplyObservedViewModelPropertyChange(
        bool texturePagesChanged,
        bool shouldRefreshPrewarmFrames,
        bool shouldRefreshRuntimeSession,
        bool shouldRefreshRuntimeFrame,
        bool shouldCaptureRuntimeCurrentFrameAutoFitReference,
        bool shouldRefreshBackgroundMode,
        bool shouldResetRenderFrameCache,
        bool shouldUpdateBindings)
    {
        if (texturePagesChanged)
        {
            RefreshTexturePages();
        }

        if (shouldResetRenderFrameCache)
        {
            ResetAutoFitReferenceCapture();
            _renderBackend.ResetFrameCache();
        }

        if (shouldRefreshPrewarmFrames)
        {
            RefreshPrewarmFrames();
        }

        if (shouldRefreshRuntimeSession)
        {
            RefreshRuntimePlaybackSession();
        }
        else if (shouldRefreshRuntimeFrame)
        {
            QueueRuntimeFrameRefresh();
        }

        if (shouldCaptureRuntimeCurrentFrameAutoFitReference)
        {
            CaptureRuntimeCurrentFrameAutoFitReferenceIfNeeded();
        }

        var shouldInvalidate = SyncRenderFrame() || texturePagesChanged;
        if (shouldRefreshBackgroundMode)
        {
            RefreshBackgroundMode();
            shouldInvalidate = true;
        }

        if (shouldUpdateBindings)
        {
            QueueBindingsUpdate();
        }

        if (ShouldRefreshSpineBackgroundActionToolTip(ViewModel, shouldUpdateBindings))
        {
            RefreshSpineBackgroundActionToolTip();
        }

        if (shouldInvalidate)
        {
            InvalidatePreviewSurface();
        }
    }

    private void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_isPreviewRunning)
        {
            return;
        }

        var wasShowingSetupPose = ViewModel?.IsShowingSetupPose == true;
        ViewModel?.TogglePlayback();
        if (wasShowingSetupPose && ViewModel?.IsShowingSetupPose == false)
        {
            ResetAutoFitReferenceCapture();
            RefreshPrewarmFrames();
            QueueRuntimeFrameRefresh();
        }

        UpdateBindingsNow();
    }

    private void OnPlaybackSpeedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPreviewRunning ||
            ViewModel is null ||
            PlaybackSpeedComboBox.SelectedItem is not double playbackSpeed)
        {
            return;
        }

        if (EqualityComparer<double>.Default.Equals(ViewModel.PlaybackSpeed, playbackSpeed))
        {
            return;
        }

        ViewModel.PlaybackSpeed = playbackSpeed;
        LogInteraction("speed-changed");
    }

    private void OnRestartButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_isPreviewRunning)
        {
            return;
        }

        ViewModel?.Restart();
        ResetAutoFitReferenceCapture();
        RefreshPrewarmFrames();
        LogInteraction("restart");
        QueueRuntimeFrameRefresh();
        SyncRenderFrame();
        InvalidatePreviewSurface();
    }

    private void OnSetupPoseButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_isPreviewRunning)
        {
            return;
        }

        ViewModel?.ShowSetupPose();
        ResetAutoFitReferenceCapture();
        RefreshPrewarmFrames();
        LogInteraction("setup-pose");
        QueueRuntimeFrameRefresh();
        SyncRenderFrame();
        InvalidatePreviewSurface();
    }

    private void OnStepPreviousButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_isPreviewRunning)
        {
            return;
        }

        ViewModel?.StepPreviousFrame();
        ResetAutoFitReferenceCapture();
        RefreshPrewarmFrames();
        LogInteraction("step-previous");
        QueueRuntimeFrameRefresh();
        SyncRenderFrame();
        InvalidatePreviewSurface();
    }

    private void OnStepNextButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_isPreviewRunning)
        {
            return;
        }

        ViewModel?.StepNextFrame();
        ResetAutoFitReferenceCapture();
        RefreshPrewarmFrames();
        LogInteraction("step-next");
        QueueRuntimeFrameRefresh();
        SyncRenderFrame();
        InvalidatePreviewSurface();
    }

    private void RefreshTexturePages()
    {
        if (ViewModel?.TexturePageContents is { Count: > 0 } texturePageContents)
        {
            _renderBackend.LoadTexturePages(texturePageContents);
            return;
        }

        _renderBackend.ClearTexturePages();
    }

    private void RefreshPrewarmFrames()
    {
        _renderBackend.PrewarmFrames(ResolveAutoFitReferenceFrames(
            ViewModel,
            _runtimeCurrentFrameAutoFitReferenceFrame));
    }

    private bool SyncRenderFrame()
    {
        var frame = ViewModel?.CurrentFrame;
        if (ReferenceEquals(_lastSyncedFrame, frame))
        {
            return false;
        }

        _lastSyncedFrame = frame;
        _renderBackend.SetCurrentFrame(frame);
        return true;
    }

    private void InvalidatePreviewSurface()
    {
        _renderBackend.Invalidate();
    }

    private void LogInteractionForProperty(string? propertyName)
    {
        if (propertyName is nameof(InspectorPreviewSpineAnimationViewModel.SelectedAnimationName))
        {
            LogInteraction("animation-changed");
            return;
        }

        if (propertyName is nameof(InspectorPreviewSpineAnimationViewModel.SelectedSkinName))
        {
            LogInteraction("skin-changed");
            return;
        }

        if (propertyName is nameof(InspectorPreviewSpineAnimationViewModel.IsPlaying))
        {
            LogInteraction(ViewModel?.IsPlaying == true ? "playback-resumed" : "playback-paused");
        }
    }

    private void LogInteraction(string eventName)
    {
        if (_diagnostics is null || ViewModel is null)
        {
            return;
        }

        _diagnostics.PlaybackInteraction(
            eventName,
            ViewModel.SelectedAnimationName,
            ViewModel.SelectedSkinName,
            ViewModel.IsPlaying,
            ViewModel.PlaybackTime,
            ViewModel.PlaybackSpeed);
    }

    private static D3D11SpineRenderDiagnostics? CreateDiagnostics() =>
        SpineAnimationPreviewRenderBackendSelector.ResolveD3D11DiagnosticsEnabled()
            ? new D3D11SpineRenderDiagnostics(static message => Debug.WriteLine(message))
            : null;
}
