namespace AssestBundleTools.Application.Workspace.Commands;

public sealed record ClearWorkspaceCacheCommand(
    string? WorkspaceDataRootPath,
    string WorkspaceKey);
