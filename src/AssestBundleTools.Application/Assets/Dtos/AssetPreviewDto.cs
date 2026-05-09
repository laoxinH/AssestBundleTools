using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示某个资源的最终预览结果。
/// </summary>
/// <remarks>
/// 该 DTO 已经被整理成 Presentation 可直接消费的读模型，
/// 图片和音频预览保留二进制内容，文本与摘要预览则直接携带解码后的字符串。
/// </remarks>
public sealed record AssetPreviewDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    AssetPreviewKind Kind,
    string ContentType,
    byte[]? BinaryContent,
    string? TextContent,
    string? PreviewPayloadFilePath,
    IReadOnlyList<AssetPreviewInfoItemDto>? PreviewInfoItems,
    AssetPreviewMessageDto? PreviewMessage,
    long? PreferredDisplaySizeInBytes,
    bool IsFromCache,
    DateTimeOffset GeneratedAtUtc,
    AssetPreviewDiagnosticDto Diagnostic,
    AssetPreviewImageOverlayDto? ImageOverlay = null,
    AssetPreviewImageFocusDto? ImageFocus = null,
    AssetPreviewMeshGeometryDto? MeshGeometry = null,
    AssetPreviewMaterialDto? Material = null,
    AssetPreviewAnimationClipDto? AnimationClip = null,
    AssetPreviewFontDto? Font = null,
    AssetPreviewGameObjectDto? GameObject = null,
    AssetPreviewMonoBehaviourDto? MonoBehaviour = null,
    AssetPreviewAnimatorControllerDto? AnimatorController = null)
{
    /// <summary>
    /// 完整文本内容，供 Application 结构化解析使用；Presentation 显示仍应优先使用有界的 <see cref="TextContent"/>。
    /// </summary>
    public string? FullTextContent { get; init; }
}
