using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewViewModel
{
    private string _audioPreviewSizeSummaryText = string.Empty;
    private string _audioPreviewContainerBadgeText = string.Empty;
    private string _audioPreviewDurationBadgeText = string.Empty;
    private string _audioPreviewChannelBadgeText = string.Empty;
    private string _audioPreviewFrequencyBadgeText = string.Empty;
    private MediaSource? _previewAudioSource;
    private IRandomAccessStream? _previewAudioStream;

    public MediaSource? PreviewAudioSource
    {
        get => _previewAudioSource;
        private set => SetProperty(ref _previewAudioSource, value);
    }

    public string AudioPreviewSizeSummaryText
    {
        get => _audioPreviewSizeSummaryText;
        private set
        {
            if (SetProperty(ref _audioPreviewSizeSummaryText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewSizeSummaryVisibility));
            }
        }
    }

    public string AudioPreviewContainerBadgeText
    {
        get => _audioPreviewContainerBadgeText;
        private set
        {
            if (SetProperty(ref _audioPreviewContainerBadgeText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewContainerBadgeVisibility));
            }
        }
    }

    public string AudioPreviewDurationBadgeText
    {
        get => _audioPreviewDurationBadgeText;
        private set
        {
            if (SetProperty(ref _audioPreviewDurationBadgeText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewDurationBadgeVisibility));
            }
        }
    }

    public string AudioPreviewChannelBadgeText
    {
        get => _audioPreviewChannelBadgeText;
        private set
        {
            if (SetProperty(ref _audioPreviewChannelBadgeText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewChannelBadgeVisibility));
            }
        }
    }

    public string AudioPreviewFrequencyBadgeText
    {
        get => _audioPreviewFrequencyBadgeText;
        private set
        {
            if (SetProperty(ref _audioPreviewFrequencyBadgeText, value))
            {
                OnPropertyChanged(nameof(AudioPreviewFrequencyBadgeVisibility));
            }
        }
    }

    public Visibility AudioPreviewSizeSummaryVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewSizeSummaryText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AudioPreviewContainerBadgeVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewContainerBadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AudioPreviewDurationBadgeVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewDurationBadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AudioPreviewChannelBadgeVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewChannelBadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AudioPreviewFrequencyBadgeVisibility =>
        string.IsNullOrWhiteSpace(AudioPreviewFrequencyBadgeText)
            ? Visibility.Collapsed
            : Visibility.Visible;

    /// <summary>
    /// 音频流需要在预览存活期间保持底层随机访问流不被回收，
    /// 因此这里同时保存 <see cref="MediaSource"/> 和对应的源流句柄。
    /// </summary>
    internal void ApplyAudioPreview(
        MediaSource audioSource,
        IRandomAccessStream audioStream,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        AudioPreviewDisplayState? audioPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(audioSource);
        ArgumentNullException.ThrowIfNull(audioStream);

        ResetState();
        _previewKind = AssetPreviewKind.Audio;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        _previewAudioStream = audioStream;
        PreviewAudioSource = audioSource;
        ApplyAudioPreviewDisplayState(audioPreviewDisplayState);
        NotifyPreviewStateChanged();
    }

    private void ClearAudioPreviewPresentation()
    {
        AudioPreviewSizeSummaryText = string.Empty;
        AudioPreviewContainerBadgeText = string.Empty;
        AudioPreviewDurationBadgeText = string.Empty;
        AudioPreviewChannelBadgeText = string.Empty;
        AudioPreviewFrequencyBadgeText = string.Empty;
    }

    private void NotifyAudioPreviewPresentationChanged()
    {
        OnPropertyChanged(nameof(AudioPreviewSizeSummaryVisibility));
        OnPropertyChanged(nameof(AudioPreviewContainerBadgeVisibility));
        OnPropertyChanged(nameof(AudioPreviewDurationBadgeVisibility));
        OnPropertyChanged(nameof(AudioPreviewChannelBadgeVisibility));
        OnPropertyChanged(nameof(AudioPreviewFrequencyBadgeVisibility));
    }

    private void ApplyAudioPreviewDisplayState(AudioPreviewDisplayState? audioPreviewDisplayState)
    {
        AudioPreviewSizeSummaryText = audioPreviewDisplayState?.SizeSummaryText ?? string.Empty;
        AudioPreviewContainerBadgeText = audioPreviewDisplayState?.ContainerBadgeText ?? string.Empty;
        AudioPreviewDurationBadgeText = audioPreviewDisplayState?.DurationBadgeText ?? string.Empty;
        AudioPreviewChannelBadgeText = audioPreviewDisplayState?.ChannelBadgeText ?? string.Empty;
        AudioPreviewFrequencyBadgeText = audioPreviewDisplayState?.FrequencyBadgeText ?? string.Empty;
    }
}
