namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区可恢复操作事实流中的单条记录。
/// </summary>
public sealed record WorkspaceOperationRecordDto(
    string OperationId,
    long Sequence,
    WorkspaceOperationKind OperationKind,
    WorkspaceOperationStatus Status,
    string WorkspaceKey,
    WorkspaceOperationTargetDto? Target,
    WorkspaceOperationSourceDto? Source,
    WorkspaceOperationStateDto? PreviousState,
    WorkspaceOperationResultDto? Result,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AppliedAtUtc = null,
    DateTimeOffset? UndoneAtUtc = null,
    DateTimeOffset? CompletedAtUtc = null,
    string? CheckpointId = null,
    string? BatchId = null,
    string? SupersedesOperationId = null,
    IReadOnlyList<WorkspaceOperationDiagnosticDto>? Diagnostics = null);
