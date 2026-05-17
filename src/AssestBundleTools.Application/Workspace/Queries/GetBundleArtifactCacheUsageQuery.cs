namespace AssestBundleTools.Application.Workspace.Queries;

/// <summary>
/// 查询标准化资源包 artifact 缓存占用。
/// </summary>
public sealed record GetBundleArtifactCacheUsageQuery(
    string? WorkspaceDataRootPath,
    string? WorkspaceKey);
