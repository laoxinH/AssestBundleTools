using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 将一次导入入口收集到的来源路径登记到工作区元数据中。
/// </summary>
public sealed record RegisterWorkspaceImportCommand(
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    string? WorkspaceDisplayName,
    string? WorkspaceSourcePath,
    WorkspaceImportOriginKind ImportOrigin,
    IReadOnlyList<string> Paths);
