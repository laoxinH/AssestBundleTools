namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 从应用级工作区目录中移除某个工作区条目。
/// </summary>
public sealed record RemoveWorkspaceCatalogEntryCommand(string WorkspaceKey);
