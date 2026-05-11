using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责维护工作区可恢复操作记录事实流。
/// </summary>
public interface IWorkspaceOperationLogService
{
    WorkspaceChangeSetDto AppendOperation(
        WorkspaceChangeSetDto? changeSet,
        WorkspaceOperationAppendRequestDto request);

    WorkspaceChangeSetDto UpdateOperationStatus(
        WorkspaceChangeSetDto changeSet,
        string operationId,
        WorkspaceOperationStatus status,
        DateTimeOffset? changedAtUtc = null,
        string? checkpointId = null,
        IReadOnlyList<WorkspaceOperationDiagnosticDto>? diagnostics = null);

    IReadOnlyList<WorkspaceOperationRecordDto> GetActiveOperations(WorkspaceChangeSetDto? changeSet);

    WorkspaceOperationRecoveryPlanDto CreateRecoveryPlan(WorkspaceChangeSetDto? changeSet);
}
