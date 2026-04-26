namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 调整工作区目录项的固定状态。
/// </summary>
public sealed record SetWorkspaceCatalogEntryPinnedStateCommand(
    string WorkspaceKey,
    bool IsPinned);
