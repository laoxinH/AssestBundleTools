namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示单个资产详情标签页的轻量恢复状态。
/// </summary>
/// <remarks>
/// 这里只保存页面级标签标题与当前页签位置等浏览信息，
/// 不保存真正的详情内容或预览缓存，避免工作区元数据膨胀。
/// </remarks>
public sealed record WorkspaceMetadataAssetTabDto(
    string AssetId,
    string AssetName,
    string? SelectedInspectorSection = null,
    string? PreviewImageBackgroundMode = null,
    string? SpritePreviewDisplayMode = null,
    string? MeshPreviewDisplayMode = null,
    bool? MeshPreviewTextureOverlaySelected = null,
    bool? MeshPreviewGridSelected = null,
    double? MeshPreviewZoom = null,
    double? MeshPreviewRotationX = null,
    double? MeshPreviewRotationY = null,
    double? PreviewImageZoom = null,
    double? PreviewImagePanX = null,
    double? PreviewImagePanY = null);
