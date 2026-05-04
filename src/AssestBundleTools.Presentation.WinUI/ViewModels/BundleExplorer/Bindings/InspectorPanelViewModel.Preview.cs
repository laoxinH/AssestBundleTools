using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPanelViewModel
{
    public void SetPreviewLoading(string statusText)
    {
        Preview.SetPreviewLoading(statusText);
        RebuildInformationCards();
    }

    public void ShowPreviewMessage(string statusText)
    {
        Preview.ShowPreviewMessage(statusText);
        RebuildInformationCards();
    }

    /// <summary>
    /// 预览诊断是预览 DTO 的结构化补充信息，
    /// 由 Presentation 层在本地化后统一写入，避免把 UI 文案拼装逻辑散落到 XAML。
    /// </summary>
    public void ApplyPreviewDiagnostic(
        string cacheStatusText,
        string fallbackReasonText,
        string diagnosticDetailText)
    {
        Preview.ApplyPreviewDiagnostic(cacheStatusText, fallbackReasonText, diagnosticDetailText);
        RebuildInformationCards();
    }

    public void OverrideSizeText(string sizeText)
    {
        SizeText = string.IsNullOrWhiteSpace(sizeText)
            ? string.Empty
            : sizeText.Trim();
        RebuildInformationCards();
    }

    public void ApplyImagePreview(
        ImageSource imageSource,
        int imagePixelWidth,
        int imagePixelHeight,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null,
        SpritePreviewOverlayDisplayState? spritePreviewOverlayDisplayState = null,
        SpriteFocusPreviewDisplayState? spriteFocusPreviewDisplayState = null,
        ImagePreviewInfoDisplayState? imagePreviewInfoDisplayState = null)
    {
        Preview.ApplyImagePreview(
            imageSource,
            imagePixelWidth,
            imagePixelHeight,
            previewInfoRows,
            previewPayloadFilePath,
            spritePreviewOverlayDisplayState,
            spriteFocusPreviewDisplayState,
            imagePreviewInfoDisplayState);
        RebuildInformationCards();
    }

    public void UpdatePreviewImageViewport(double viewportWidth, double viewportHeight)
    {
        Preview.UpdatePreviewImageViewport(viewportWidth, viewportHeight);
    }

    public void ApplyAudioPreview(
        MediaSource audioSource,
        IRandomAccessStream audioStream,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        AudioPreviewDisplayState? audioPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        Preview.ApplyAudioPreview(
            audioSource,
            audioStream,
            previewInfoRows,
            audioPreviewDisplayState,
            previewPayloadFilePath);
        RebuildInformationCards();
    }

    public void ApplyMeshPreview(
        AssetPreviewMeshGeometryDto meshGeometry,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        MeshPreviewDisplayState? meshPreviewDisplayState = null,
        string? previewPayloadFilePath = null,
        byte[]? textureOverlayContent = null)
    {
        Preview.ApplyMeshPreview(
            meshGeometry,
            previewInfoRows,
            meshPreviewDisplayState,
            previewPayloadFilePath,
            textureOverlayContent);
        RebuildInformationCards();
    }

    public void ApplyMaterialPreview(
        AssetPreviewMaterialDto material,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        MaterialPreviewDisplayState? materialPreviewDisplayState = null,
        string? previewPayloadFilePath = null,
        IReadOnlyDictionary<string, ImageSource>? materialTextureThumbnailSources = null)
    {
        Preview.ApplyMaterialPreview(
            material,
            previewInfoRows,
            materialPreviewDisplayState,
            previewPayloadFilePath,
            materialTextureThumbnailSources);
        RebuildInformationCards();
    }

    public void ApplyAnimationClipPreview(
        AssetPreviewAnimationClipDto animationClip,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        AnimationClipPreviewDisplayState? animationClipPreviewDisplayState = null,
        string? previewPayloadFilePath = null)
    {
        Preview.ApplyAnimationClipPreview(
            animationClip,
            previewInfoRows,
            animationClipPreviewDisplayState,
            previewPayloadFilePath);
        RebuildInformationCards();
    }

    public void ApplyTextPreview(
        string textContent,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null)
    {
        Preview.ApplyTextPreview(textContent, previewInfoRows, previewPayloadFilePath);
        RebuildInformationCards();
    }

    public void ApplySummaryPreview(
        string summaryText,
        IReadOnlyList<InspectorInfoRowViewModel>? previewInfoRows = null,
        string? previewPayloadFilePath = null)
    {
        Preview.ApplySummaryPreview(summaryText, previewInfoRows, previewPayloadFilePath);
        RebuildInformationCards();
    }
}
