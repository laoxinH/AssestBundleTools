namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示追加工作区操作记录时的结构化输入。
/// </summary>
public sealed record WorkspaceOperationAppendRequestDto(
    string WorkspaceKey,
    WorkspaceOperationKind OperationKind,
    WorkspaceOperationStatus Status,
    WorkspaceOperationTargetDto? Target = null,
    WorkspaceOperationSourceDto? Source = null,
    WorkspaceOperationStateDto? PreviousState = null,
    WorkspaceOperationResultDto? Result = null,
    string? OperationId = null,
    DateTimeOffset? CreatedAtUtc = null,
    DateTimeOffset? AppliedAtUtc = null,
    DateTimeOffset? UndoneAtUtc = null,
    DateTimeOffset? CompletedAtUtc = null,
    string? CheckpointId = null,
    string? BatchId = null,
    string? SupersedesOperationId = null,
    IReadOnlyList<WorkspaceOperationDiagnosticDto>? Diagnostics = null);
