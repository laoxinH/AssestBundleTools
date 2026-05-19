namespace AssestBundleTools.Application.Workspace.Queries;

public sealed record GetWorkspaceCacheUsageQuery(
    string? WorkspaceDataRootPath,
    string WorkspaceKey);
