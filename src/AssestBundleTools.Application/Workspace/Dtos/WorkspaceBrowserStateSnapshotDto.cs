namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区浏览器页需要保存和恢复的轻量状态快照。
/// </summary>
/// <remarks>
/// 该快照只包含页面级浏览偏好与标签页信息，
/// 不包含运行时工作区里的真实资源数据或变更结果。
/// </remarks>
public sealed record WorkspaceBrowserStateSnapshotDto(
    string BundleSearchKeyword,
    string AssetSearchKeyword,
    string? AssetTypeFilter,
    bool ShowModifiedOnly,
    bool ShowPreviewableOnly,
    IReadOnlyList<WorkspaceMetadataBundleTabDto> OpenBundleTabs,
    string? ActiveBundleTabId,
    string? LastBundleId,
    string? LastAssetId);
