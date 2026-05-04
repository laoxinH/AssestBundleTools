using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载检查器中的预览子状态，避免把图片/音频/文本/覆盖层细节继续堆回父级检查器宿主。
/// </summary>
public sealed class InspectorPreviewViewModel : ObservableObject
{
    private string _previewStatusText = string.Empty;
    private string _previewText = string.Empty;
    private string _previewPayloadFilePath = string.Empty;
    private string _previewCacheStatusText = string.Empty;
    private string _previewFallbackReasonText = string.Empty;
    private string _previewDiagnosticDetailText = string.Empty;
    private string _audioPreviewSizeSummaryText = string.Empty;
    private string _audioPreviewContainerBadgeText = string.Empty;
    private string _audioPreviewDurationBadgeText = string.Empty;
    private string _audioPreviewChannelBadgeText = string.Empty;
    private string _audioPreviewFrequencyBadgeText = string.Empty;
    private MediaSource? _previewAudioSource;
    private IRandomAccessStream? _previewAudioStream;
    private AssetPreviewKind _previewKind;
    private bool _isPreviewLoading;
    private bool _hasSelection;
    private IReadOnlyList<InspectorInfoRowViewModel> _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();

    public InspectorPreviewImageViewModel Image { get; } = new();

    public InspectorPreviewMeshViewModel Mesh { get; } = new();

    public string PreviewStatusText
    {
        get => _previewStatusText;
        private set => SetProperty(ref _previewStatusText, value);
    }

    public string PreviewText
    {
        get => _previewText;
        private set => SetProperty(ref _previewText, value);
    }

    /// <summary>
    /// 该路径只用于“打开预览文件所在位置”这类壳层动作，不参与业务判断。
    /// </summary>
    public string PreviewPayloadFilePath
    {
        get => _previewPayloadFilePath;
        private set
        {
            if (SetProperty(ref _previewPayloadFilePath, value))
            {
                OnPropertyChanged(nameof(CanOpenPreviewLocation));
                OnPropertyChanged(nameof(PreviewOpenLocationVisibility));
            }
        }
    }

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

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        private set => SetProperty(ref _isPreviewLoading, value);
    }

    /// <summary>
    /// 图片预览继续使用固定高度，保证列表与详情页的视觉稳定；
    /// 音频预览改为内容自适应，避免播放器和标签内容被固定高度裁切。
    /// </summary>
    public double PreviewSurfaceHeight => _previewKind == AssetPreviewKind.Audio ? double.NaN : 320d;

    public double PreviewSurfaceMinHeight => _previewKind == AssetPreviewKind.Audio ? 0d : 320d;

    public Visibility PreviewLoadingVisibility => _hasSelection && IsPreviewLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PreviewImageVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Image &&
        Image.ImageSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewTextVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Text &&
        !string.IsNullOrWhiteSpace(PreviewText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewAudioVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Audio &&
        PreviewAudioSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewMeshVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Mesh &&
        Mesh.HasGeometry
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewSummaryVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.Summary &&
        !string.IsNullOrWhiteSpace(PreviewText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PreviewMessageVisibility =>
        _hasSelection &&
        !IsPreviewLoading &&
        _previewKind == AssetPreviewKind.None &&
        !string.IsNullOrWhiteSpace(PreviewStatusText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public bool CanOpenPreviewLocation => !string.IsNullOrWhiteSpace(PreviewPayloadFilePath);

    public Visibility PreviewOpenLocationVisibility =>
        CanOpenPreviewLocation
            ? Visibility.Visible
            : Visibility.Collapsed;

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

    internal IReadOnlyList<InspectorInfoRowViewModel> PreviewInfoRows => _previewInfoRows;

    internal string PreviewCacheStatusText => _previewCacheStatusText;

    internal string PreviewFallbackReasonText => _previewFallbackReasonText;

    internal string PreviewDiagnosticDetailText => _previewDiagnosticDetailText;

    internal void UpdateHasSelection(bool hasSelection)
    {
        if (_hasSelection == hasSelection)
        {
            return;
        }

        _hasSelection = hasSelection;
        NotifyPreviewStateChanged();
    }

    internal void SetPreviewLoading(string statusText)
    {
        ResetState();
        PreviewStatusText = statusText;
        IsPreviewLoading = true;
        NotifyPreviewStateChanged();
    }

    internal void ShowPreviewMessage(string statusText)
    {
        ResetState();
        PreviewStatusText = statusText;
        NotifyPreviewStateChanged();
    }

    internal void ApplyPreviewDiagnostic(
        string cacheStatusText,
        string fallbackReasonText,
        string diagnosticDetailText)
    {
        _previewCacheStatusText = NormalizePreviewInfoText(cacheStatusText);
        _previewFallbackReasonText = NormalizePreviewInfoText(fallbackReasonText);
        _previewDiagnosticDetailText = NormalizePreviewInfoText(diagnosticDetailText);
    }

    internal void ApplyImagePreview(
        ImageSource imageSource,
        int imagePixelWidth,
        int imagePixelHeight,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null,
        SpritePreviewOverlayDisplayState? spritePreviewOverlayDisplayState = null,
        SpriteFocusPreviewDisplayState? spriteFocusPreviewDisplayState = null,
        ImagePreviewInfoDisplayState? imagePreviewInfoDisplayState = null)
    {
        ArgumentNullException.ThrowIfNull(imageSource);

        ResetState();
        _previewKind = AssetPreviewKind.Image;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        Image.Apply(
            imageSource,
            imagePixelWidth,
            imagePixelHeight,
            spritePreviewOverlayDisplayState,
            spriteFocusPreviewDisplayState,
            imagePreviewInfoDisplayState);
        NotifyPreviewStateChanged();
    }

    internal void UpdatePreviewImageViewport(double viewportWidth, double viewportHeight)
    {
        Image.UpdateViewport(viewportWidth, viewportHeight);
    }

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

    internal void ApplyMeshPreview(
        AssetPreviewMeshGeometryDto meshGeometry,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        MeshPreviewDisplayState? meshPreviewDisplayState = null,
        string? previewPayloadFilePath = null,
        byte[]? textureOverlayContent = null)
    {
        ArgumentNullException.ThrowIfNull(meshGeometry);

        ResetState();
        _previewKind = AssetPreviewKind.Mesh;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        Mesh.Apply(meshGeometry, meshPreviewDisplayState);
        Mesh.ApplyTextureOverlay(textureOverlayContent);
        NotifyPreviewStateChanged();
    }

    internal void ApplyTextPreview(
        string textContent,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null)
    {
        ResetState();
        _previewKind = AssetPreviewKind.Text;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        PreviewText = textContent;
        NotifyPreviewStateChanged();
    }

    internal void ApplySummaryPreview(
        string summaryText,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null)
    {
        ResetState();
        _previewKind = AssetPreviewKind.Summary;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        PreviewText = summaryText;
        NotifyPreviewStateChanged();
    }

    internal void ResetState()
    {
        IsPreviewLoading = false;
        PreviewStatusText = string.Empty;
        PreviewText = string.Empty;
        PreviewPayloadFilePath = string.Empty;
        Image.Clear();
        Mesh.Clear();
        PreviewAudioSource = null;
        _previewAudioStream?.Dispose();
        _previewAudioStream = null;
        _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();
        _previewCacheStatusText = string.Empty;
        _previewFallbackReasonText = string.Empty;
        _previewDiagnosticDetailText = string.Empty;
        _previewKind = AssetPreviewKind.None;
        ClearAudioPreviewPresentation();
        NotifyPreviewStateChanged();
    }

    private void NotifyPreviewStateChanged()
    {
        OnPropertyChanged(nameof(PreviewLoadingVisibility));
        OnPropertyChanged(nameof(PreviewImageVisibility));
        OnPropertyChanged(nameof(PreviewTextVisibility));
        OnPropertyChanged(nameof(PreviewAudioVisibility));
        OnPropertyChanged(nameof(PreviewMeshVisibility));
        OnPropertyChanged(nameof(PreviewSummaryVisibility));
        OnPropertyChanged(nameof(PreviewMessageVisibility));
        OnPropertyChanged(nameof(PreviewSurfaceHeight));
        OnPropertyChanged(nameof(PreviewSurfaceMinHeight));
        NotifyAudioPreviewPresentationChanged();
    }

    private static string NormalizePreviewInfoText(string? previewInfoText) =>
        string.IsNullOrWhiteSpace(previewInfoText)
            ? string.Empty
            : previewInfoText.Trim();

    private static string NormalizePreviewPayloadFilePath(string? previewPayloadFilePath) =>
        string.IsNullOrWhiteSpace(previewPayloadFilePath)
            ? string.Empty
            : previewPayloadFilePath.Trim();

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

    private static IReadOnlyList<InspectorInfoRowViewModel> CloneRows(
        IReadOnlyList<InspectorInfoRowViewModel>? rows)
    {
        return rows is { Count: > 0 }
            ? rows.ToArray()
            : Array.Empty<InspectorInfoRowViewModel>();
    }
}
