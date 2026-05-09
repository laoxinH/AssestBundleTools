using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView : UserControl
{
    private readonly DispatcherTimer _playbackTimer;
    private readonly ISpineAnimationPreviewRenderBackend _renderBackend;
    private InspectorPreviewSpineAnimationViewModel? _observedViewModel;
    private DateTimeOffset _lastTickUtc;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPreviewSpineAnimationViewModel),
            typeof(InspectorSpineAnimationPreviewSurfaceView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public InspectorSpineAnimationPreviewSurfaceView()
    {
        InitializeComponent();
        _renderBackend = SpineAnimationPreviewRenderBackendFactory.Create(
            SpineCanvas,
            SpineSwapChainPanel);
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _playbackTimer.Tick += OnPlaybackTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyLocalizedText();
    }

    public InspectorPreviewSpineAnimationViewModel? ViewModel
    {
        get => (InspectorPreviewSpineAnimationViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorSpineAnimationPreviewSurfaceView view)
        {
            return;
        }

        view.DetachObservedViewModel(args.OldValue as InspectorPreviewSpineAnimationViewModel);
        view.AttachObservedViewModel(args.NewValue as InspectorPreviewSpineAnimationViewModel);
        view.RefreshTexturePages();
        view.SyncRenderFrame();
        view.UpdateBindingsNow();
        view.InvalidatePreviewSurface();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachObservedViewModel(ViewModel);
        RefreshTexturePages();
        SyncRenderFrame();
        _lastTickUtc = DateTimeOffset.UtcNow;
        _playbackTimer.Start();
        UpdateBindingsNow();
        InvalidatePreviewSurface();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _playbackTimer.Stop();
        DetachObservedViewModel(_observedViewModel);
        _renderBackend.SetCurrentFrame(null);
        _renderBackend.ClearTexturePages();
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
        if (e.PropertyName is nameof(InspectorPreviewSpineAnimationViewModel.TexturePageContents))
        {
            RefreshTexturePages();
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            SyncRenderFrame();
            UpdateBindingsNow();
            InvalidatePreviewSurface();
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            SyncRenderFrame();
            UpdateBindingsNow();
            InvalidatePreviewSurface();
        });
    }

    private void OnPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.TogglePlayback();
        UpdateBindingsNow();
    }

    private void OnPlaybackSpeedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is null ||
            PlaybackSpeedComboBox.SelectedItem is not double playbackSpeed)
        {
            return;
        }

        ViewModel.PlaybackSpeed = playbackSpeed;
    }

    private void OnRestartButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Restart();
        SyncRenderFrame();
        InvalidatePreviewSurface();
    }

    private void OnSetupPoseButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.ShowSetupPose();
        SyncRenderFrame();
        InvalidatePreviewSurface();
    }

    private void OnStepPreviousButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.StepPreviousFrame();
        SyncRenderFrame();
        InvalidatePreviewSurface();
    }

    private void OnStepNextButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.StepNextFrame();
        SyncRenderFrame();
        InvalidatePreviewSurface();
    }

    private void OnPlaybackTimerTick(object? sender, object e)
    {
        var now = DateTimeOffset.UtcNow;
        var elapsed = now - _lastTickUtc;
        _lastTickUtc = now;
        ViewModel?.Advance(elapsed);
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

    private void SyncRenderFrame()
    {
        _renderBackend.SetCurrentFrame(ViewModel?.CurrentFrame);
    }

    private void UpdateBindingsNow()
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            Bindings.Update();
            return;
        }

        DispatcherQueue.TryEnqueue(UpdateBindingsNow);
    }

    private void InvalidatePreviewSurface()
    {
        _renderBackend.Invalidate();
    }
}
