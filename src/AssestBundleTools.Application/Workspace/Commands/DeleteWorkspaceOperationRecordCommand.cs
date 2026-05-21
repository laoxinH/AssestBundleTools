using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 请求删除一条已经持久化的工作区操作记录。
/// </summary>
public sealed record DeleteWorkspaceOperationRecordCommand(
    WorkspaceId WorkspaceId,
    string OperationId,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null);
