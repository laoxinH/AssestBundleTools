namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示独立资源编辑窗口的轻量恢复状态。
/// </summary>
/// <remarks>
/// 该状态只记录窗口位置、资产文档标签顺序与页面级预览浏览选项，
/// 不保存真实资源详情、预览载荷、替换结果或编辑草稿内容。
/// 如果后续编辑器存在未应用草稿，这里只允许记录轻量草稿引用。
/// </remarks>
public sealed record WorkspaceAssetEditorWindowStateDto(
    int WindowStateId,
    WorkspaceAssetEditorWindowLayoutDto Layout,
    IReadOnlyList<WorkspaceAssetEditorWindowAssetTabDto> OpenAssetTabs,
    string? ActiveBundleId,
    string? ActiveAssetId,
    IReadOnlyList<WorkspaceAssetEditorWindowDocumentTabDto>? OpenDocumentTabs = null,
    string? ActiveDocumentKind = null,
    string? ActiveSpineGroupId = null);

/// <summary>
/// 表示独立资源编辑窗口的轻量布局状态。
/// </summary>
public sealed record WorkspaceAssetEditorWindowLayoutDto(
    int X,
    int Y,
    int Width,
    int Height);

/// <summary>
/// 表示独立资源编辑窗口中的统一文档标签恢复状态。
/// </summary>
public sealed record WorkspaceAssetEditorWindowDocumentTabDto(
    string Kind,
    WorkspaceAssetEditorWindowAssetTabDto? AssetTab = null,
    WorkspaceAssetEditorWindowSpineGroupTabDto? SpineGroupTab = null)
{
    public const string AssetKind = "asset";
    public const string SpineGroupKind = "spineGroup";
}

/// <summary>
/// 表示独立资源编辑窗口中的单个资产文档标签恢复状态。
/// </summary>
public sealed record WorkspaceAssetEditorWindowAssetTabDto(
    string BundleId,
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
    double? PreviewImagePanY = null,
    WorkspaceAssetEditDraftReferenceDto? EditDraftReference = null,
    WorkspaceInspectorScrollStateDto? InspectorScrollState = null);

/// <summary>
/// 表示独立资源编辑窗口中的单个 Spine 资源组文档标签恢复状态。
/// </summary>
public sealed record WorkspaceAssetEditorWindowSpineGroupTabDto(
    string BundleId,
    string GroupId,
    string DisplayName,
    string? GroupKey = null,
    WorkspaceSpinePreviewStateDto? PreviewState = null,
    WorkspaceInspectorScrollStateDto? InspectorScrollState = null);
