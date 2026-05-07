namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示单个资源包标签页的轻量恢复状态。
/// </summary>
/// <remarks>
/// 当前只保存页面级浏览上下文，
/// 避免把真实工作区运行时数据反向塞回元数据文件。
/// </remarks>
public sealed record WorkspaceMetadataBundleTabDto(
    string BundleId,
    string LogicalPath,
    bool IsPreview,
    string? SelectedAssetId,
    string SearchKeyword,
    string? AssetTypeFilter,
    bool ShowModifiedOnly,
    bool ShowPreviewableOnly,
    IReadOnlyList<WorkspaceMetadataAssetTabDto>? OpenAssetTabs = null,
    string? ActiveAssetTabId = null,
    string? AssetListViewMode = null,
    IReadOnlyList<WorkspaceMetadataSpineGroupTabDto>? OpenSpineGroupTabs = null,
    string? ActiveSpineGroupTabId = null);
