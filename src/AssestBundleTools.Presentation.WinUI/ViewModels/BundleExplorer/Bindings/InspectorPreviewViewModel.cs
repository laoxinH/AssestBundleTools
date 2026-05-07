using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载检查器中的预览子状态，避免把图片/音频/文本/覆盖层细节继续堆回父级检查器宿主。
/// </summary>
public sealed partial class InspectorPreviewViewModel : ObservableObject
{
    private string _previewStatusText = string.Empty;
    private string _previewText = string.Empty;
    private string _previewPayloadFilePath = string.Empty;
    private string _previewCacheStatusText = string.Empty;
    private string _previewFallbackReasonText = string.Empty;
    private string _previewDiagnosticDetailText = string.Empty;
    private AssetPreviewKind _previewKind;
    private bool _isPreviewLoading;
    private bool _hasSelection;
    private IReadOnlyList<InspectorInfoRowViewModel> _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();
    private IReadOnlyList<InspectorInfoCardViewModel> _additionalInformationCards = Array.Empty<InspectorInfoCardViewModel>();

    public InspectorPreviewImageViewModel Image { get; } = new();

    public InspectorPreviewMeshViewModel Mesh { get; } = new();

    public InspectorPreviewMaterialViewModel Material { get; } = new();

    public InspectorPreviewAnimationClipViewModel AnimationClip { get; } = new();

    public InspectorPreviewFontViewModel Font { get; } = new();

    public InspectorPreviewGameObjectViewModel GameObject { get; } = new();

    public InspectorPreviewMonoBehaviourViewModel MonoBehaviour { get; } = new();

    public InspectorPreviewAnimatorControllerViewModel AnimatorController { get; } = new();

    public InspectorPreviewSpineAnimationViewModel SpineAnimation { get; } = new();

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

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;
        private set => SetProperty(ref _isPreviewLoading, value);
    }

    /// <summary>
    /// 只有真实渲染面保持固定高度；摘要卡片预览交给外层布局按内容自适应。
    /// </summary>
    public bool UsesFixedPreviewSurfaceHeight => UsesFixedPreviewSurfaceHeightFor(_previewKind);

    public double PreviewSurfaceHeight => UsesFixedPreviewSurfaceHeight ? 320d : double.NaN;

    public double PreviewSurfaceMinHeight => UsesFixedPreviewSurfaceHeight ? 320d : 0d;

    internal IReadOnlyList<InspectorInfoRowViewModel> PreviewInfoRows => _previewInfoRows;

    internal IReadOnlyList<InspectorInfoCardViewModel> AdditionalInformationCards => _additionalInformationCards;

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
        ImagePreviewInfoDisplayState? imagePreviewInfoDisplayState = null,
        ImageRegionOverlayDisplayState? imageRegionOverlayDisplayState = null)
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
            imagePreviewInfoDisplayState,
            imageRegionOverlayDisplayState);
        NotifyPreviewStateChanged();
    }

    internal void UpdatePreviewImageViewport(double viewportWidth, double viewportHeight)
    {
        Image.UpdateViewport(viewportWidth, viewportHeight);
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

    internal void ApplyMaterialPreview(
        AssetPreviewMaterialDto material,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        MaterialPreviewDisplayState? materialPreviewDisplayState = null,
        string? previewPayloadFilePath = null,
        IReadOnlyDictionary<string, ImageSource>? materialTextureThumbnailSources = null)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(materialPreviewDisplayState);

        ResetState();
        _previewKind = AssetPreviewKind.Material;
        _previewInfoRows = CloneRows(previewInfoRows);
        _additionalInformationCards = CloneCards(materialPreviewDisplayState.DetailCards ?? Array.Empty<InspectorInfoCardViewModel>());
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        Material.Apply(material, materialPreviewDisplayState, materialTextureThumbnailSources);
        NotifyPreviewStateChanged();
    }

    internal void ApplyAnimationClipPreview(
        AssetPreviewAnimationClipDto animationClip,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        AnimationClipPreviewDisplayState? animationClipPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(animationClip);
        ArgumentNullException.ThrowIfNull(animationClipPreviewDisplayState);

        ResetState();
        _previewKind = AssetPreviewKind.AnimationClip;
        _previewInfoRows = CloneRows(previewInfoRows);
        _additionalInformationCards = CloneCards(animationClipPreviewDisplayState.DetailCards);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        AnimationClip.Apply(animationClip, animationClipPreviewDisplayState);
        NotifyPreviewStateChanged();
    }

    internal void ApplyFontPreview(
        AssetPreviewFontDto font,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        FontPreviewDisplayState? fontPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(fontPreviewDisplayState);

        ResetState();
        _previewKind = AssetPreviewKind.Font;
        _previewInfoRows = CloneRows(previewInfoRows);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        Font.Apply(font, fontPreviewDisplayState);
        NotifyPreviewStateChanged();
    }

    internal void ApplyGameObjectPreview(
        AssetPreviewGameObjectDto gameObject,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        GameObjectPreviewDisplayState? gameObjectPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        ArgumentNullException.ThrowIfNull(gameObjectPreviewDisplayState);

        ResetState();
        _previewKind = AssetPreviewKind.GameObject;
        _previewInfoRows = CloneRows(previewInfoRows);
        _additionalInformationCards = CloneCards(gameObjectPreviewDisplayState.DetailCards);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        GameObject.Apply(gameObject, gameObjectPreviewDisplayState);
        NotifyPreviewStateChanged();
    }

    internal void ApplyMonoBehaviourPreview(
        AssetPreviewMonoBehaviourDto monoBehaviour,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        MonoBehaviourPreviewDisplayState? monoBehaviourPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(monoBehaviour);
        ArgumentNullException.ThrowIfNull(monoBehaviourPreviewDisplayState);

        ResetState();
        _previewKind = AssetPreviewKind.MonoBehaviour;
        _previewInfoRows = CloneRows(previewInfoRows);
        _additionalInformationCards = CloneCards(monoBehaviourPreviewDisplayState.DetailCards);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        MonoBehaviour.Apply(monoBehaviour, monoBehaviourPreviewDisplayState);
        NotifyPreviewStateChanged();
    }

    internal void ApplyAnimatorControllerPreview(
        AssetPreviewAnimatorControllerDto animatorController,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        AnimatorControllerPreviewDisplayState? animatorControllerPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(animatorController);
        ArgumentNullException.ThrowIfNull(animatorControllerPreviewDisplayState);

        ResetState();
        _previewKind = AssetPreviewKind.AnimatorController;
        _previewInfoRows = CloneRows(previewInfoRows);
        _additionalInformationCards = CloneCards(animatorControllerPreviewDisplayState.DetailCards);
        PreviewPayloadFilePath = NormalizePreviewPayloadFilePath(previewPayloadFilePath);
        AnimatorController.Apply(animatorController, animatorControllerPreviewDisplayState);
        NotifyPreviewStateChanged();
    }

    internal void ApplySpineAnimationPreview(
        SpineAnimationPreviewDto animationPreview,
        IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> texturePreviews,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null)
    {
        ArgumentNullException.ThrowIfNull(animationPreview);
        ArgumentNullException.ThrowIfNull(texturePreviews);

        ResetState();
        _previewKind = AssetPreviewKind.SpineAnimation;
        _previewInfoRows = CloneRows(previewInfoRows);
        SpineAnimation.Apply(animationPreview, texturePreviews);
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
        Material.Clear();
        AnimationClip.Clear();
        Font.Clear();
        GameObject.Clear();
        MonoBehaviour.Clear();
        AnimatorController.Clear();
        SpineAnimation.Clear();
        PreviewAudioSource = null;
        _previewAudioStream?.Dispose();
        _previewAudioStream = null;
        _previewInfoRows = Array.Empty<InspectorInfoRowViewModel>();
        _additionalInformationCards = Array.Empty<InspectorInfoCardViewModel>();
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
        OnPropertyChanged(nameof(PreviewMaterialVisibility));
        OnPropertyChanged(nameof(PreviewAnimationClipVisibility));
        OnPropertyChanged(nameof(PreviewFontVisibility));
        OnPropertyChanged(nameof(PreviewGameObjectVisibility));
        OnPropertyChanged(nameof(PreviewMonoBehaviourVisibility));
        OnPropertyChanged(nameof(PreviewAnimatorControllerVisibility));
        OnPropertyChanged(nameof(PreviewSpineAnimationVisibility));
        OnPropertyChanged(nameof(PreviewSummaryVisibility));
        OnPropertyChanged(nameof(PreviewMessageVisibility));
        OnPropertyChanged(nameof(PreviewSurfaceHeight));
        OnPropertyChanged(nameof(PreviewSurfaceMinHeight));
        OnPropertyChanged(nameof(UsesFixedPreviewSurfaceHeight));
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

    private static IReadOnlyList<InspectorInfoRowViewModel> CloneRows(
        IReadOnlyList<InspectorInfoRowViewModel>? rows)
    {
        return rows is { Count: > 0 }
            ? rows.ToArray()
            : Array.Empty<InspectorInfoRowViewModel>();
    }

    private static IReadOnlyList<InspectorInfoCardViewModel> CloneCards(
        IReadOnlyList<InspectorInfoCardViewModel>? cards)
    {
        return cards is { Count: > 0 }
            ? cards.ToArray()
            : Array.Empty<InspectorInfoCardViewModel>();
    }

    private static bool UsesFixedPreviewSurfaceHeightFor(AssetPreviewKind previewKind)
    {
        return previewKind is
            AssetPreviewKind.Image or
            AssetPreviewKind.Mesh or
            AssetPreviewKind.Text or
            AssetPreviewKind.SpineAnimation;
    }
}
