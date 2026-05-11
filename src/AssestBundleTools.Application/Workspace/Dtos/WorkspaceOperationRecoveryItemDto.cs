namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示启动或打开工作区时需要用户处理的一条恢复项。
/// </summary>
public sealed record WorkspaceOperationRecoveryItemDto(
    string OperationId,
    long Sequence,
    WorkspaceOperationKind OperationKind,
    WorkspaceOperationStatus Status,
    WorkspaceOperationRecoveryIssueKind IssueKind,
    WorkspaceOperationRecoveryActionKind RecommendedAction,
    WorkspaceOperationTargetDto? Target,
    WorkspaceOperationSourceDto? Source,
    IReadOnlyList<WorkspaceOperationDiagnosticDto>? Diagnostics);
