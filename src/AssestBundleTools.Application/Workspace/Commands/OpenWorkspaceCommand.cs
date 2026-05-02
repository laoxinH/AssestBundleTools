using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 请求打开一个工作区，并返回主界面初始化所需的摘要数据。
/// </summary>
public sealed record OpenWorkspaceCommand(
    string WorkspacePath,
    string WorkspaceDataRootPath,
    string? WorkspaceDisplayName = null,
    WorkspaceMode InitialMode = WorkspaceMode.SinglePane,
    IProgress<WorkspaceImportProgressDto>? ImportProgress = null);
