using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 请求处理某个工作区已经登记的导入来源，并刷新缓存索引。
/// </summary>
public sealed record ProcessWorkspaceImportsCommand(
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    IProgress<WorkspaceImportProgressDto>? Progress = null);
