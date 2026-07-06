using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Media.Playback;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorPreviewSurfaceView : UserControl
{
    private readonly DispatcherTimer _audioTransportTimer;
    private MediaPlayer? _attachedAudioPlayer;
    private InspectorPreviewViewModel? _observedPreviewViewModel;
    private bool _isUpdatingAudioSliderValue;
    private bool _isBindingsUpdateQueued;
    private object? _lastAppliedAudioSource;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPanelViewModel),
            typeof(InspectorPreviewSurfaceView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public InspectorPreviewSurfaceView()
    {
        InitializeComponent();
        _audioTransportTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _audioTransportTimer.Tick += OnAudioTransportTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyLocalizedText();
    }

    public InspectorPanelViewModel? ViewModel
    {
        get => (InspectorPanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler? OpenPreviewLocationRequested;

    public event EventHandler? SpriteTextureReplacementRequested;

    public event EventHandler? LoadMoreTextRequested;

    public event EventHandler<ResolvedAssetOpenRequestedEventArgs>? ResolvedAssetOpenRequested;

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorPreviewSurfaceView view)
        {
            return;
        }

        view.DetachObservedPreviewViewModel((args.OldValue as InspectorPanelViewModel)?.Preview);
        view.AttachObservedPreviewViewModel((args.NewValue as InspectorPanelViewModel)?.Preview);
        view.UpdateBindingsNow();
        view.RefreshAudioPreviewSourceBinding();
    }

    private void ApplyLocalizedText()
    {
        ToolTipService.SetToolTip(OpenPreviewLocationButton, UiText.Get(ResourceKeys.InspectorPreviewOpenLocationToolTip));
        ToolTipService.SetToolTip(AudioTransportPlayPauseButton, UiText.Get(ResourceKeys.InspectorAudioTransportPlayToolTip));
        ToolTipService.SetToolTip(AudioTransportResetButton, UiText.Get(ResourceKeys.InspectorAudioTransportResetToolTip));
        ToolTipService.SetToolTip(ImageZoomInButton, UiText.Get(ResourceKeys.InspectorPreviewImageZoomInToolTip));
        ToolTipService.SetToolTip(ImageZoomOutButton, UiText.Get(ResourceKeys.InspectorPreviewImageZoomOutToolTip));
        ToolTipService.SetToolTip(ImageResetButton, UiText.Get(ResourceKeys.InspectorPreviewImageResetToolTip));
        ApplyButtonAssistiveText(SpriteEditResetOriginalButton, UiText.Get(ResourceKeys.InspectorSpriteEditResetOriginalToolTip));
        ApplyButtonAssistiveText(SpriteEditFullSizeButton, UiText.Get(ResourceKeys.InspectorSpriteEditFullSizeToolTip));
        ApplyButtonAssistiveText(SpriteEditReplaceTextureButton, UiText.Get(ResourceKeys.InspectorSpriteEditReplaceTextureToolTip));
        ToolTipService.SetToolTip(MeshZoomInButton, UiText.Get(ResourceKeys.InspectorMeshPreviewZoomInToolTip));
        ToolTipService.SetToolTip(MeshZoomOutButton, UiText.Get(ResourceKeys.InspectorMeshPreviewZoomOutToolTip));
        ToolTipService.SetToolTip(MeshResetButton, UiText.Get(ResourceKeys.InspectorMeshPreviewResetToolTip));
        RefreshImageActionToolTips();
        RefreshMeshActionToolTips();
        RefreshAudioTransportState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachAudioPlayer(AudioPreviewPlayer.MediaPlayer);
        AttachObservedPreviewViewModel(ViewModel?.Preview);
        UpdateBindingsNow();
        RefreshAudioPreviewSourceBinding();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _audioTransportTimer.Stop();
        _lastAppliedAudioSource = null;
        DetachObservedPreviewViewModel(_observedPreviewViewModel);
        DetachAudioPlayer();
    }

    private void AttachObservedPreviewViewModel(InspectorPreviewViewModel? previewViewModel)
    {
        if (previewViewModel is null || ReferenceEquals(_observedPreviewViewModel, previewViewModel))
        {
            return;
        }

        _observedPreviewViewModel = previewViewModel;
        _observedPreviewViewModel.PropertyChanged += OnObservedPreviewViewModelPropertyChanged;
        _observedPreviewViewModel.Image.DisplayOptions.PropertyChanged += OnObservedPreviewImageDisplayOptionsPropertyChanged;
        _observedPreviewViewModel.Mesh.PropertyChanged += OnObservedPreviewMeshPropertyChanged;
        RefreshImageActionToolTips();
        RefreshMeshActionToolTips();
    }

    private void DetachObservedPreviewViewModel(InspectorPreviewViewModel? previewViewModel)
    {
        if (previewViewModel is null || !ReferenceEquals(_observedPreviewViewModel, previewViewModel))
        {
            return;
        }

        _observedPreviewViewModel.PropertyChanged -= OnObservedPreviewViewModelPropertyChanged;
        _observedPreviewViewModel.Image.DisplayOptions.PropertyChanged -= OnObservedPreviewImageDisplayOptionsPropertyChanged;
        _observedPreviewViewModel.Mesh.PropertyChanged -= OnObservedPreviewMeshPropertyChanged;
        _observedPreviewViewModel = null;
    }

    private void OnObservedPreviewViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            HandleObservedPreviewPropertyChanged(e.PropertyName);
            return;
        }

        DispatcherQueue.TryEnqueue(() => HandleObservedPreviewPropertyChanged(e.PropertyName));
    }

    private void HandleObservedPreviewPropertyChanged(string? propertyName)
    {
        QueueBindingsUpdate();

        if (propertyName is
            nameof(InspectorPreviewViewModel.PreviewAudioSource) or
            nameof(InspectorPreviewViewModel.PreviewAudioVisibility))
        {
            RefreshAudioPreviewSourceBinding();
        }

        if (propertyName is
            nameof(InspectorPreviewViewModel.UsesFixedPreviewSurfaceHeight) or
            nameof(InspectorPreviewViewModel.PreviewSurfaceHeight) or
            nameof(InspectorPreviewViewModel.PreviewSurfaceMinHeight))
        {
            RefreshPreviewSurfaceHeightBinding();
        }
    }

    private void OnObservedPreviewImageDisplayOptionsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(InspectorPreviewImageDisplayOptionsViewModel.BackgroundModeGlyph) or
            nameof(InspectorPreviewImageDisplayOptionsViewModel.SpriteDisplayModeGlyph)))
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            QueueBindingsUpdate();
            RefreshImageActionToolTips();
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            QueueBindingsUpdate();
            RefreshImageActionToolTips();
        });
    }

    private void OnObservedPreviewMeshPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(InspectorPreviewMeshViewModel.DisplayMode) or
            nameof(InspectorPreviewMeshViewModel.DisplayModeGlyph) or
            nameof(InspectorPreviewMeshViewModel.IsTextureOverlaySelected) or
            nameof(InspectorPreviewMeshViewModel.TextureOverlayGlyph) or
            nameof(InspectorPreviewMeshViewModel.IsMeshGridSelected) or
            nameof(InspectorPreviewMeshViewModel.MeshGridGlyph)))
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            QueueBindingsUpdate();
            RefreshMeshActionToolTips();
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            QueueBindingsUpdate();
            RefreshMeshActionToolTips();
        });
    }

    private void UpdateBindingsNow()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(UpdateBindingsNow);
            return;
        }

        _isBindingsUpdateQueued = false;
        Bindings.Update();
    }

    private void QueueBindingsUpdate()
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(QueueBindingsUpdate);
            return;
        }

        if (_isBindingsUpdateQueued)
        {
            return;
        }

        _isBindingsUpdateQueued = true;
        if (!DispatcherQueue.TryEnqueue(UpdateBindingsNow))
        {
            _isBindingsUpdateQueued = false;
        }
    }

    private void OnAudioTransportTimerTick(object? sender, object e)
    {
        RefreshAudioTransportState();
    }

    private void AttachAudioPlayer(MediaPlayer? mediaPlayer)
    {
        if (ReferenceEquals(_attachedAudioPlayer, mediaPlayer))
        {
            return;
        }

        DetachAudioPlayer();
        _attachedAudioPlayer = mediaPlayer;
        if (_attachedAudioPlayer is null)
        {
            return;
        }

        _attachedAudioPlayer.CurrentStateChanged += OnAudioPlayerStateChanged;
        _attachedAudioPlayer.MediaOpened += OnAudioPlayerStateChanged;
        _attachedAudioPlayer.MediaEnded += OnAudioPlayerStateChanged;
        _attachedAudioPlayer.MediaFailed += OnAudioPlayerStateChanged;
    }

    private void DetachAudioPlayer()
    {
        if (_attachedAudioPlayer is null)
        {
            return;
        }

        _attachedAudioPlayer.CurrentStateChanged -= OnAudioPlayerStateChanged;
        _attachedAudioPlayer.MediaOpened -= OnAudioPlayerStateChanged;
        _attachedAudioPlayer.MediaEnded -= OnAudioPlayerStateChanged;
        _attachedAudioPlayer.MediaFailed -= OnAudioPlayerStateChanged;
        _attachedAudioPlayer = null;
    }

    private void OnAudioPlayerStateChanged(MediaPlayer sender, object args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshAudioTransportState();
            return;
        }

        DispatcherQueue.TryEnqueue(RefreshAudioTransportState);
    }

    private void RefreshAudioPreviewSourceBinding()
    {
        AttachAudioPlayer(AudioPreviewPlayer.MediaPlayer);
        if (!IsPreviewActive)
        {
            if (!ReferenceEquals(_lastAppliedAudioSource, null))
            {
                _lastAppliedAudioSource = null;
                AudioPreviewPlayer.Source = null;
            }

            _audioTransportTimer.Stop();
            ApplyAudioTransportState(
                isEnabled: false,
                isPlaying: false,
                currentPosition: TimeSpan.Zero,
                naturalDuration: TimeSpan.Zero);
            return;
        }

        var nextSource = ViewModel?.Preview.PreviewAudioSource;
        if (!ReferenceEquals(_lastAppliedAudioSource, nextSource))
        {
            _lastAppliedAudioSource = nextSource;
            AudioPreviewPlayer.Source = null;
            if (_attachedAudioPlayer?.PlaybackSession is not null)
            {
                _attachedAudioPlayer.Pause();
                _attachedAudioPlayer.PlaybackSession.Position = TimeSpan.Zero;
            }

            AudioPreviewPlayer.Source = nextSource;
        }

        RefreshAudioTransportState();
        DispatcherQueue.TryEnqueue(RefreshAudioTransportState);
    }

    private void OnAudioTransportPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        AttachAudioPlayer(AudioPreviewPlayer.MediaPlayer);
        if (_attachedAudioPlayer is null || ViewModel?.Preview.PreviewAudioSource is null)
        {
            return;
        }

        if (_attachedAudioPlayer.CurrentState == MediaPlayerState.Playing)
        {
            _attachedAudioPlayer.Pause();
        }
        else
        {
            _attachedAudioPlayer.Play();
        }

        RefreshAudioTransportState();
    }

    private void OnAudioTransportResetButtonClick(object sender, RoutedEventArgs e)
    {
        if (_attachedAudioPlayer?.PlaybackSession is null)
        {
            return;
        }

        _attachedAudioPlayer.Pause();
        _attachedAudioPlayer.PlaybackSession.Position = TimeSpan.Zero;
        RefreshAudioTransportState();
    }

    private void OnAudioTransportPositionSliderValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingAudioSliderValue || _attachedAudioPlayer?.PlaybackSession is null)
        {
            return;
        }

        var targetPosition = TimeSpan.FromSeconds(Math.Max(0d, e.NewValue));
        if (Math.Abs((_attachedAudioPlayer.PlaybackSession.Position - targetPosition).TotalMilliseconds) < 200d)
        {
            return;
        }

        _attachedAudioPlayer.PlaybackSession.Position = targetPosition;
        RefreshAudioTransportState();
    }

    /// <summary>
    /// 自定义音频控制条直接消费 MediaPlayer 运行时状态，
    /// 这样既能规避默认传输控件在窄宽度下的布局问题，也能把通用预览动作保留在预览面板顶部。
    /// </summary>
    private void RefreshAudioTransportState()
    {
        AttachAudioPlayer(AudioPreviewPlayer.MediaPlayer);

        var hasAudioPreview = ViewModel?.Preview.PreviewAudioSource is not null;
        if (_attachedAudioPlayer?.PlaybackSession is null || !hasAudioPreview)
        {
            _audioTransportTimer.Stop();
            ApplyAudioTransportState(
                isEnabled: false,
                isPlaying: false,
                currentPosition: TimeSpan.Zero,
                naturalDuration: TimeSpan.Zero);
            return;
        }

        var playbackSession = _attachedAudioPlayer.PlaybackSession;
        var naturalDuration = playbackSession.NaturalDuration;
        var currentPosition = playbackSession.Position;
        var isPlaying = _attachedAudioPlayer.CurrentState == MediaPlayerState.Playing;
        if (!_audioTransportTimer.IsEnabled)
        {
            _audioTransportTimer.Start();
        }

        ApplyAudioTransportState(
            isEnabled: true,
            isPlaying: isPlaying,
            currentPosition: currentPosition,
            naturalDuration: naturalDuration);
    }

    private void ApplyAudioTransportState(
        bool isEnabled,
        bool isPlaying,
        TimeSpan currentPosition,
        TimeSpan naturalDuration)
    {
        AudioTransportPlayPauseButton.IsEnabled = isEnabled;
        AudioTransportResetButton.IsEnabled = isEnabled;
        AudioTransportPlayPauseIcon.Glyph = isPlaying ? "\uE769" : "\uE768";
        ToolTipService.SetToolTip(
            AudioTransportPlayPauseButton,
            UiText.Get(isPlaying
                ? ResourceKeys.InspectorAudioTransportPauseToolTip
                : ResourceKeys.InspectorAudioTransportPlayToolTip));

        var totalDuration = naturalDuration > TimeSpan.Zero
            ? naturalDuration
            : TimeSpan.Zero;
        var maximumValue = totalDuration > TimeSpan.Zero
            ? totalDuration.TotalSeconds
            : 1d;
        var currentValue = Math.Clamp(currentPosition.TotalSeconds, 0d, maximumValue);

        _isUpdatingAudioSliderValue = true;
        try
        {
            AudioTransportPositionSlider.Maximum = maximumValue;
            AudioTransportPositionSlider.IsEnabled = isEnabled && totalDuration > TimeSpan.Zero;
            AudioTransportPositionSlider.Value = currentValue;
        }
        finally
        {
            _isUpdatingAudioSliderValue = false;
        }

        AudioTransportTimeText.Text = $"{FormatDuration(currentPosition)} / {FormatDuration(totalDuration)}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }
}
