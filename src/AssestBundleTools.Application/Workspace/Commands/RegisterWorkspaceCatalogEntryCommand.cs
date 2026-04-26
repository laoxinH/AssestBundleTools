using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 将某个工作区登记到应用级工作区目录中。
/// </summary>
public sealed record RegisterWorkspaceCatalogEntryCommand(
    string SourcePath,
    string? DisplayName,
    string WorkspaceDataRootPath,
    WorkspaceMode WorkspaceMode,
    bool IsPinned = false,
    string? LastKnownState = null);
