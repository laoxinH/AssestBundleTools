using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示某个资源的最终预览结果。
/// </summary>
/// <remarks>
/// 该 DTO 已经被整理成 Presentation 可消费的读模型。
/// 图片和音频预览保留当前可展示的二进制内容；文本展示必须使用有界的 <see cref="TextContent"/>。
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
    /// 文本展示预算元数据；仅当 <see cref="TextContent"/> 来自文本预览策略时存在。
    /// </summary>
    public AssetPreviewTextDisplayDto? TextDisplay { get; init; }

    /// <summary>
    /// 完整预览内容引用；Presentation 只能把它传回 Application 查询，不能自行解析缓存路径。
    /// </summary>
    public AssetPreviewFullContentReferenceDto? FullContentReference { get; init; }

    /// <summary>
    /// 完整文本内容，供 Application 结构化解析过渡使用；Presentation 不得直接绑定该字段。
    /// </summary>
    public string? FullTextContent { get; init; }
}
