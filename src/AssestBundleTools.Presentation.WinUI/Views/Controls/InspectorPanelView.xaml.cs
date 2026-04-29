using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Playback;

namespace AssestBundleTools.Presentation.WinUI.Views.Controls;

/// <summary>
/// 右侧详情检查器的可复用视图。
/// </summary>
public sealed partial class InspectorPanelView : UserControl
{
    private readonly DispatcherTimer _audioTransportTimer;
    private MediaPlayer? _attachedAudioPlayer;
    private InspectorPanelViewModel? _observedViewModel;
    private bool _isUpdatingAudioSliderValue;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(InspectorPanelViewModel),
            typeof(InspectorPanelView),
            new PropertyMetadata(null, OnViewModelPropertyChanged));

    public InspectorPanelView()
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

    public event EventHandler? ExtractRequested;

    public event EventHandler? SelectFileReplacementSourceRequested;

    public event EventHandler? SelectReplacementSourceRequested;

    public event EventHandler? ApplyReplacementRequested;

    public event EventHandler? UndoReplacementRequested;

    public event EventHandler? OpenPreviewLocationRequested;

    private void OnExtractButtonClick(object sender, RoutedEventArgs e)
    {
        ExtractRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectFileReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        SelectFileReplacementSourceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        SelectReplacementSourceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnApplyReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        ApplyReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnUndoReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        UndoReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenPreviewLocationButtonClick(object sender, RoutedEventArgs e)
    {
        OpenPreviewLocationRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 内层详情切换保持单选语义，点击后统一回写到同一个 SectionIndex，
    /// 避免控件自己的开关行为把“当前选中页”状态打散。
    /// </summary>
    private void OnSectionRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not RadioButton { Tag: string sectionTag })
        {
            return;
        }

        if (int.TryParse(sectionTag, out var sectionIndex))
        {
            ViewModel.SelectedSectionIndex = sectionIndex;
        }
    }

    private void OnInspectorInfoCardHeaderButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: InspectorInfoCardViewModel card })
        {
            card.ToggleExpanded();
        }
    }

    private void OnInspectorInfoCardCopyButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: InspectorInfoCardViewModel card } ||
            string.IsNullOrWhiteSpace(card.CopyPayloadText))
        {
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(card.CopyPayloadText);
        Clipboard.SetContent(dataPackage);
    }

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorPanelView view)
        {
            return;
        }

        view.DetachObservedViewModel(args.OldValue as InspectorPanelViewModel);
        view.AttachObservedViewModel(args.NewValue as InspectorPanelViewModel);
    }

    private void ApplyLocalizedText()
    {
        DetailsSectionButton.Content = UiText.Get(ResourceKeys.InspectorDetailsTabHeader);
        DependenciesSectionButton.Content = UiText.Get(ResourceKeys.InspectorDependenciesTabHeader);
        ReplacementSectionButton.Content = UiText.Get(ResourceKeys.InspectorReplacementTabHeader);
        ChangesSectionButton.Content = UiText.Get(ResourceKeys.InspectorChangesTabHeader);

        EmptyStateTitleText.Text = UiText.Get(ResourceKeys.InspectorEmptyStateTitle);
        InformationSectionTitleText.Text = UiText.Get(ResourceKeys.InspectorInformationSectionTitle);
        AssetDependenciesTitleText.Text = UiText.Get(ResourceKeys.InspectorAssetDependenciesTitle);
        ReferencedByTitleText.Text = UiText.Get(ResourceKeys.InspectorReferencedByTitle);
        DependencyClosureTitleText.Text = UiText.Get(ResourceKeys.InspectorDependencyClosureTitle);
        BundleExternalDependenciesTitleText.Text = UiText.Get(ResourceKeys.InspectorBundleExternalDependenciesTitle);
        DependenciesEmptyStateText.Text = UiText.Get(ResourceKeys.InspectorDependenciesEmptyStateText);
        ReplacementTargetTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementTargetTitle);
        ReplacementSourceTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementSourceTitle);
        ReplacementCompatibilityTitleText.Text = UiText.Get(ResourceKeys.InspectorReplacementCompatibilityTitle);
        ExtractCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorExtractCapabilityLabel);
        FileReplacementCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorFileReplacementCapabilityLabel);
        WorkspaceReplacementCapabilityLabelText.Text = UiText.Get(ResourceKeys.InspectorWorkspaceReplacementCapabilityLabel);
        ChangeSummaryTitleText.Text = UiText.Get(ResourceKeys.InspectorChangeSummaryTitle);

        ExtractButton.Content = UiText.Get(ResourceKeys.InspectorExtractButton);
        SelectFileReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectFileReplacementButton);
        SelectReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectReplacementButton);
        ApplyReplacementButton.Content = UiText.Get(ResourceKeys.InspectorApplyReplacementButton);
        UndoReplacementButton.Content = UiText.Get(ResourceKeys.InspectorUndoReplacementButton);
        ToolTipService.SetToolTip(OpenPreviewLocationButton, UiText.Get(ResourceKeys.InspectorPreviewOpenLocationToolTip));
        ToolTipService.SetToolTip(AudioTransportPlayPauseButton, UiText.Get(ResourceKeys.InspectorAudioTransportPlayToolTip));
        ToolTipService.SetToolTip(AudioTransportResetButton, UiText.Get(ResourceKeys.InspectorAudioTransportResetToolTip));

        RefreshAudioTransportState();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachAudioPlayer(AudioPreviewPlayer.MediaPlayer);
        RefreshAudioTransportState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _audioTransportTimer.Stop();
        DetachAudioPlayer();
    }

    private void AttachObservedViewModel(InspectorPanelViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
        RefreshAudioTransportState();
    }

    private void DetachObservedViewModel(InspectorPanelViewModel? viewModel)
    {
        if (viewModel is null || !ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        _observedViewModel = null;
    }

    private void OnObservedViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(InspectorPanelViewModel.PreviewAudioSource) or
            nameof(InspectorPanelViewModel.PreviewAudioVisibility) or
            nameof(InspectorPanelViewModel.CanOpenPreviewLocation)))
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshAudioTransportState();
            return;
        }

        DispatcherQueue.TryEnqueue(RefreshAudioTransportState);
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

    private void OnAudioTransportPlayPauseButtonClick(object sender, RoutedEventArgs e)
    {
        AttachAudioPlayer(AudioPreviewPlayer.MediaPlayer);
        if (_attachedAudioPlayer is null || ViewModel?.PreviewAudioSource is null)
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

        var hasAudioPreview = ViewModel?.PreviewAudioSource is not null;
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
