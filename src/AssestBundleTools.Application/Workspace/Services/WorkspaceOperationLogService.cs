using System.Globalization;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 维护工作区可恢复操作记录的顺序、状态和恢复视图。
/// </summary>
public sealed class WorkspaceOperationLogService : IWorkspaceOperationLogService
{
    private readonly WorkspaceOperationRecordLimitPolicy _recordLimitPolicy;

    public WorkspaceOperationLogService(Func<int>? operationRecordLimitProvider = null)
    {
        _recordLimitPolicy = new WorkspaceOperationRecordLimitPolicy(operationRecordLimitProvider);
    }

    public WorkspaceChangeSetDto AppendOperation(
        WorkspaceChangeSetDto? changeSet,
        WorkspaceOperationAppendRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workspaceKey = NormalizeRequired(request.WorkspaceKey, nameof(request.WorkspaceKey));
        if (changeSet is not null)
        {
            var changeSetWorkspaceKey = NormalizeRequired(changeSet.WorkspaceKey, nameof(changeSet.WorkspaceKey));
            if (!string.Equals(changeSetWorkspaceKey, workspaceKey, StringComparison.Ordinal))
            {
                throw new ArgumentException("Operation workspace key must match the change set workspace key.", nameof(request));
            }
        }

        if (request.OperationKind == WorkspaceOperationKind.Unknown)
        {
            throw new ArgumentException("Operation kind must be specified.", nameof(request));
        }

        var existingOperations = NormalizeOperations(changeSet?.Operations);
        var createdAtUtc = request.CreatedAtUtc ?? DateTimeOffset.UtcNow;
        var nextSequence = Math.Max(
            changeSet?.OperationSequence ?? 0,
            existingOperations.Count == 0 ? 0 : existingOperations.Max(static operation => operation.Sequence)) + 1;

        var operation = new WorkspaceOperationRecordDto(
            OperationId: NormalizeOperationId(request.OperationId, createdAtUtc),
            Sequence: nextSequence,
            OperationKind: request.OperationKind,
            Status: request.Status,
            WorkspaceKey: workspaceKey,
            Target: request.Target,
            Source: request.Source,
            PreviousState: request.PreviousState,
            Result: request.Result,
            CreatedAtUtc: createdAtUtc,
            AppliedAtUtc: ResolveAppliedAtUtc(request.Status, request.AppliedAtUtc, createdAtUtc),
            UndoneAtUtc: ResolveUndoneAtUtc(request.Status, request.UndoneAtUtc, createdAtUtc),
            CompletedAtUtc: ResolveCompletedAtUtc(request.Status, request.CompletedAtUtc, createdAtUtc),
            CheckpointId: NormalizeOptional(request.CheckpointId),
            BatchId: NormalizeOptional(request.BatchId),
            SupersedesOperationId: NormalizeOptional(request.SupersedesOperationId),
            Diagnostics: NormalizeDiagnostics(request.Diagnostics));

        return NormalizeChangeSet(changeSet, workspaceKey) with
        {
            UpdatedAtUtc = createdAtUtc,
            SchemaVersion = WorkspaceChangeSetDto.CurrentSchemaVersion,
            OperationSequence = nextSequence,
            Operations = _recordLimitPolicy.Apply(existingOperations.Append(operation).ToArray())
        };
    }

    public WorkspaceChangeSetDto UpdateOperationStatus(
        WorkspaceChangeSetDto changeSet,
        string operationId,
        WorkspaceOperationStatus status,
        DateTimeOffset? changedAtUtc = null,
        string? checkpointId = null,
        IReadOnlyList<WorkspaceOperationDiagnosticDto>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(changeSet);

        var normalizedOperationId = NormalizeRequired(operationId, nameof(operationId));
        var timestamp = changedAtUtc ?? DateTimeOffset.UtcNow;
        var found = false;
        var operations = NormalizeOperations(changeSet.Operations)
            .Select(operation =>
            {
                if (!string.Equals(operation.OperationId, normalizedOperationId, StringComparison.Ordinal))
                {
                    return operation;
                }

                found = true;
                return operation with
                {
                    Status = status,
                    AppliedAtUtc = ResolveAppliedAtUtc(status, operation.AppliedAtUtc, timestamp),
                    UndoneAtUtc = ResolveUndoneAtUtc(status, operation.UndoneAtUtc, timestamp),
                    CompletedAtUtc = ResolveCompletedAtUtc(status, operation.CompletedAtUtc, timestamp),
                    CheckpointId = NormalizeOptional(checkpointId) ?? operation.CheckpointId,
                    Diagnostics = diagnostics is null
                        ? operation.Diagnostics
                        : NormalizeDiagnostics(diagnostics)
                };
            })
            .ToArray();

        if (!found)
        {
            throw new ArgumentException("Operation id was not found in the change set.", nameof(operationId));
        }

        return NormalizeChangeSet(changeSet, changeSet.WorkspaceKey) with
        {
            UpdatedAtUtc = timestamp,
            SchemaVersion = WorkspaceChangeSetDto.CurrentSchemaVersion,
            OperationSequence = Math.Max(changeSet.OperationSequence, operations.Max(static operation => operation.Sequence)),
            Operations = operations
        };
    }

    public WorkspaceChangeSetDto DeleteOperationRecord(
        WorkspaceChangeSetDto changeSet,
        string operationId,
        DateTimeOffset? deletedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(changeSet);

        var normalizedOperationId = NormalizeRequired(operationId, nameof(operationId));
        var operations = NormalizeOperations(changeSet.Operations);
        var operationIdsToDelete = CollectOperationIdsToDelete(operations, normalizedOperationId);
        var updatedOperations = operations
            .Where(operation => !operationIdsToDelete.Contains(operation.OperationId))
            .ToArray();
        if (updatedOperations.Length == operations.Count)
        {
            throw new ArgumentException("Operation id was not found in the change set.", nameof(operationId));
        }

        return NormalizeChangeSet(changeSet, changeSet.WorkspaceKey) with
        {
            UpdatedAtUtc = deletedAtUtc ?? DateTimeOffset.UtcNow,
            SchemaVersion = WorkspaceChangeSetDto.CurrentSchemaVersion,
            OperationSequence = changeSet.OperationSequence,
            Operations = updatedOperations
        };
    }

    public IReadOnlyList<WorkspaceOperationRecordDto> GetActiveOperations(WorkspaceChangeSetDto? changeSet)
    {
        var operations = NormalizeOperations(changeSet?.Operations)
            .OrderBy(static operation => operation.Sequence)
            .ToArray();
        var supersededOperationIds = operations
            .Where(static operation => operation is
            {
                OperationKind: WorkspaceOperationKind.UndoReplacement,
                Status: WorkspaceOperationStatus.Applied,
                SupersedesOperationId: not null
            })
            .Select(static operation => operation.SupersedesOperationId!)
            .ToHashSet(StringComparer.Ordinal);

        return operations
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(operation => !supersededOperationIds.Contains(operation.OperationId))
            .Where(static operation => operation.OperationKind is not WorkspaceOperationKind.UndoReplacement and not WorkspaceOperationKind.RedoReplacement)
            .ToArray();
    }

    public WorkspaceOperationRecoveryPlanDto CreateRecoveryPlan(WorkspaceChangeSetDto? changeSet)
    {
        if (changeSet is null)
        {
            return WorkspaceOperationRecoveryPlanDto.Empty(string.Empty);
        }

        var workspaceKey = string.IsNullOrWhiteSpace(changeSet.WorkspaceKey)
            ? string.Empty
            : changeSet.WorkspaceKey.Trim();
        var items = NormalizeOperations(changeSet.Operations)
            .OrderBy(static operation => operation.Sequence)
            .Select(CreateRecoveryItem)
            .Where(static item => item is not null)
            .Select(static item => item!)
            .ToArray();

        return new WorkspaceOperationRecoveryPlanDto(workspaceKey, items);
    }

    private static WorkspaceOperationRecoveryItemDto? CreateRecoveryItem(WorkspaceOperationRecordDto operation)
    {
        return operation.Status switch
        {
            WorkspaceOperationStatus.Pending => CreateRecoveryItem(
                operation,
                WorkspaceOperationStatus.Interrupted,
                WorkspaceOperationRecoveryIssueKind.InterruptedPendingOperation,
                WorkspaceOperationRecoveryActionKind.MarkFailed),
            WorkspaceOperationStatus.Interrupted => CreateRecoveryItem(
                operation,
                WorkspaceOperationStatus.Interrupted,
                WorkspaceOperationRecoveryIssueKind.InterruptedPendingOperation,
                WorkspaceOperationRecoveryActionKind.MarkFailed),
            WorkspaceOperationStatus.Failed => CreateRecoveryItem(
                operation,
                WorkspaceOperationStatus.Failed,
                WorkspaceOperationRecoveryIssueKind.FailedOperation,
                WorkspaceOperationRecoveryActionKind.IgnoreForNow),
            WorkspaceOperationStatus.Invalid => CreateRecoveryItem(
                operation,
                WorkspaceOperationStatus.Invalid,
                WorkspaceOperationRecoveryIssueKind.InvalidOperation,
                WorkspaceOperationRecoveryActionKind.DiscardRecord),
            _ => null
        };
    }

    private static WorkspaceOperationRecoveryItemDto CreateRecoveryItem(
        WorkspaceOperationRecordDto operation,
        WorkspaceOperationStatus status,
        WorkspaceOperationRecoveryIssueKind issueKind,
        WorkspaceOperationRecoveryActionKind recommendedAction) =>
        new(
            operation.OperationId,
            operation.Sequence,
            operation.OperationKind,
            status,
            issueKind,
            recommendedAction,
            operation.Target,
            operation.Source,
            operation.Diagnostics);

    private static WorkspaceChangeSetDto NormalizeChangeSet(WorkspaceChangeSetDto? changeSet, string workspaceKey)
    {
        if (changeSet is null)
        {
            return WorkspaceChangeSetDto.Empty(workspaceKey);
        }

        var operations = NormalizeOperations(changeSet.Operations);
        return changeSet with
        {
            WorkspaceKey = NormalizeRequired(changeSet.WorkspaceKey, nameof(changeSet.WorkspaceKey)),
            Changes = changeSet.Changes ?? Array.Empty<WorkspaceChangeRecordDto>(),
            EditDrafts = changeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
            Operations = operations,
            OperationSequence = Math.Max(
                changeSet.OperationSequence,
                operations.Count == 0
                    ? 0
                    : operations.Max(static operation => operation.Sequence))
        };
    }

    private static IReadOnlyList<WorkspaceOperationRecordDto> NormalizeOperations(
        IReadOnlyList<WorkspaceOperationRecordDto>? operations) =>
        operations is null
            ? Array.Empty<WorkspaceOperationRecordDto>()
            : operations
                .Where(static operation => operation is not null)
                .OrderBy(static operation => operation.Sequence)
                .ToArray();

    private static ISet<string> CollectOperationIdsToDelete(
        IReadOnlyList<WorkspaceOperationRecordDto> operations,
        string rootOperationId)
    {
        var idsToDelete = new HashSet<string>(StringComparer.Ordinal)
        {
            rootOperationId
        };

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var operation in operations)
            {
                if (string.IsNullOrWhiteSpace(operation.SupersedesOperationId) ||
                    !idsToDelete.Contains(operation.SupersedesOperationId))
                {
                    continue;
                }

                changed |= idsToDelete.Add(operation.OperationId);
            }
        }

        return idsToDelete;
    }

    private static IReadOnlyList<WorkspaceOperationDiagnosticDto>? NormalizeDiagnostics(
        IReadOnlyList<WorkspaceOperationDiagnosticDto>? diagnostics)
    {
        if (diagnostics is null)
        {
            return null;
        }

        return diagnostics
            .Where(static diagnostic => diagnostic is not null)
            .Select(static diagnostic => diagnostic with
            {
                Code = string.IsNullOrWhiteSpace(diagnostic.Code) ? "Workspace.Operation.Unknown" : diagnostic.Code.Trim(),
                Severity = string.IsNullOrWhiteSpace(diagnostic.Severity) ? "Warning" : diagnostic.Severity.Trim(),
                MessageToken = NormalizeOptional(diagnostic.MessageToken),
                Detail = NormalizeOptional(diagnostic.Detail),
                SourcePath = NormalizeOptional(diagnostic.SourcePath)
            })
            .ToArray();
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeOperationId(string? operationId, DateTimeOffset createdAtUtc)
    {
        var normalizedOperationId = NormalizeOptional(operationId);
        if (normalizedOperationId is not null)
        {
            return normalizedOperationId;
        }

        var ticks = createdAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"op_{ticks}_{suffix}";
    }

    private static DateTimeOffset? ResolveAppliedAtUtc(
        WorkspaceOperationStatus status,
        DateTimeOffset? existingValue,
        DateTimeOffset timestamp) =>
        status == WorkspaceOperationStatus.Applied
            ? existingValue ?? timestamp
            : existingValue;

    private static DateTimeOffset? ResolveUndoneAtUtc(
        WorkspaceOperationStatus status,
        DateTimeOffset? existingValue,
        DateTimeOffset timestamp) =>
        status == WorkspaceOperationStatus.Undone
            ? existingValue ?? timestamp
            : existingValue;

    private static DateTimeOffset? ResolveCompletedAtUtc(
        WorkspaceOperationStatus status,
        DateTimeOffset? existingValue,
        DateTimeOffset timestamp) =>
        status is WorkspaceOperationStatus.Applied or
            WorkspaceOperationStatus.Undone or
            WorkspaceOperationStatus.Failed or
            WorkspaceOperationStatus.Interrupted or
            WorkspaceOperationStatus.Committed or
            WorkspaceOperationStatus.Invalid
            ? existingValue ?? timestamp
            : existingValue;
}
