using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 根据设置的操作记录上限裁剪历史，同时保护当前可恢复状态所需的记录。
/// </summary>
internal sealed class WorkspaceOperationRecordLimitPolicy
{
    private readonly Func<int>? _operationRecordLimitProvider;

    public WorkspaceOperationRecordLimitPolicy(Func<int>? operationRecordLimitProvider)
    {
        _operationRecordLimitProvider = operationRecordLimitProvider;
    }

    public IReadOnlyList<WorkspaceOperationRecordDto> Apply(
        IReadOnlyList<WorkspaceOperationRecordDto> operations)
    {
        var limit = _operationRecordLimitProvider?.Invoke() ?? 0;
        if (limit <= 0 || operations.Count <= limit)
        {
            return operations;
        }

        return PruneOperationsForLimit(operations, limit);
    }

    private static IReadOnlyList<WorkspaceOperationRecordDto> PruneOperationsForLimit(
        IReadOnlyList<WorkspaceOperationRecordDto> operations,
        int limit)
    {
        var normalizedOperations = NormalizeOperations(operations);
        var protectedOperationIds = FindProtectedOperationIds(normalizedOperations);
        var selectedOperations = new List<WorkspaceOperationRecordDto>(Math.Min(normalizedOperations.Count, Math.Max(1, limit)));
        var selectedOperationIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var operation in normalizedOperations.OrderByDescending(static operation => operation.Sequence))
        {
            var shouldKeep = protectedOperationIds.Contains(operation.OperationId) ||
                             selectedOperations.Count < limit;
            if (!shouldKeep ||
                !selectedOperationIds.Add(operation.OperationId))
            {
                continue;
            }

            selectedOperations.Add(operation);
        }

        return selectedOperations
            .OrderBy(static operation => operation.Sequence)
            .ToArray();
    }

    private static ISet<string> FindProtectedOperationIds(
        IReadOnlyList<WorkspaceOperationRecordDto> operations)
    {
        var protectedOperationIds = operations
            .Where(static operation => operation.Status is
                WorkspaceOperationStatus.Pending or
                WorkspaceOperationStatus.Interrupted or
                WorkspaceOperationStatus.Failed or
                WorkspaceOperationStatus.Invalid)
            .Select(static operation => operation.OperationId)
            .ToHashSet(StringComparer.Ordinal);

        ProtectLatestExtractionOperations(operations, protectedOperationIds);
        ProtectEffectiveReplacementOperations(operations, protectedOperationIds);
        ProtectLatestSpriteEditOperations(operations, protectedOperationIds);
        return protectedOperationIds;
    }

    private static void ProtectLatestExtractionOperations(
        IReadOnlyList<WorkspaceOperationRecordDto> operations,
        ISet<string> protectedOperationIds)
    {
        foreach (var latestExtraction in operations
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind == WorkspaceOperationKind.ExtractAsset)
            .Where(static operation => TryCreateTargetKey(operation.Target, out _))
            .GroupBy(static operation => CreateTargetKey(operation.Target!), StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(operation => operation.Sequence).First()))
        {
            protectedOperationIds.Add(latestExtraction.OperationId);
        }
    }

    private static void ProtectEffectiveReplacementOperations(
        IReadOnlyList<WorkspaceOperationRecordDto> operations,
        ISet<string> protectedOperationIds)
    {
        var replacementOperations = operations
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind is
                WorkspaceOperationKind.ReplaceFromFile or
                WorkspaceOperationKind.ReplaceFromWorkspace or
                WorkspaceOperationKind.UndoReplacement or
                WorkspaceOperationKind.RedoReplacement)
            .Where(static operation => TryCreateTargetKey(operation.Target, out _))
            .OrderBy(static operation => operation.Sequence)
            .ToArray();

        var statesByTarget = new Dictionary<string, ReplacementRetentionState>(StringComparer.Ordinal);
        var operationsById = replacementOperations.ToDictionary(static operation => operation.OperationId, StringComparer.Ordinal);
        foreach (var operation in replacementOperations)
        {
            var targetKey = CreateTargetKey(operation.Target!);
            if (!statesByTarget.TryGetValue(targetKey, out var state))
            {
                state = new ReplacementRetentionState();
                statesByTarget[targetKey] = state;
            }

            ApplyReplacementRetentionOperation(state, operation);
        }

        foreach (var item in statesByTarget)
        {
            var currentOperationId = item.Value.CurrentOperationId;
            if (string.IsNullOrWhiteSpace(currentOperationId) ||
                !operationsById.TryGetValue(currentOperationId, out var currentOperation))
            {
                continue;
            }

            foreach (var operation in replacementOperations
                .Where(operation => string.Equals(CreateTargetKey(operation.Target!), item.Key, StringComparison.Ordinal))
                .Where(operation => operation.Sequence >= currentOperation.Sequence))
            {
                protectedOperationIds.Add(operation.OperationId);
            }

            protectedOperationIds.Add(currentOperation.OperationId);
        }
    }

    private static void ProtectLatestSpriteEditOperations(
        IReadOnlyList<WorkspaceOperationRecordDto> operations,
        ISet<string> protectedOperationIds)
    {
        foreach (var latestEdit in operations
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind == WorkspaceOperationKind.EditDraftApplied)
            .Where(static operation => operation.Result?.SpriteEditGeometry is not null)
            .Where(static operation => TryCreateTargetKey(operation.Target, out _))
            .GroupBy(static operation => CreateTargetKey(operation.Target!), StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(operation => operation.Sequence).First()))
        {
            protectedOperationIds.Add(latestEdit.OperationId);
        }
    }

    private static void ApplyReplacementRetentionOperation(
        ReplacementRetentionState state,
        WorkspaceOperationRecordDto operation)
    {
        switch (operation.OperationKind)
        {
            case WorkspaceOperationKind.ReplaceFromFile:
            case WorkspaceOperationKind.ReplaceFromWorkspace:
                state.UndoStack.Add(state.CurrentOperationId);
                state.RedoStack.Clear();
                state.CurrentOperationId = operation.OperationId;
                break;
            case WorkspaceOperationKind.UndoReplacement:
                if (state.CurrentOperationId is null)
                {
                    break;
                }

                state.RedoStack.Add(state.CurrentOperationId);
                state.CurrentOperationId = PopOrDefault(state.UndoStack);
                break;
            case WorkspaceOperationKind.RedoReplacement:
                if (operation.Source is not null &&
                    !string.IsNullOrWhiteSpace(operation.SupersedesOperationId))
                {
                    state.UndoStack.Add(state.CurrentOperationId);
                    state.RedoStack.Clear();
                    state.CurrentOperationId = operation.SupersedesOperationId.Trim();
                    break;
                }

                var redoOperationId = PopOrDefault(state.RedoStack);
                if (redoOperationId is null)
                {
                    break;
                }

                state.UndoStack.Add(state.CurrentOperationId);
                state.CurrentOperationId = redoOperationId;
                break;
        }
    }

    private static string? PopOrDefault(IList<string?> stack)
    {
        if (stack.Count == 0)
        {
            return null;
        }

        var value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }

    private static IReadOnlyList<WorkspaceOperationRecordDto> NormalizeOperations(
        IReadOnlyList<WorkspaceOperationRecordDto>? operations) =>
        operations is null
            ? Array.Empty<WorkspaceOperationRecordDto>()
            : operations
                .Where(static operation => operation is not null)
                .OrderBy(static operation => operation.Sequence)
                .ToArray();

    private static bool TryCreateTargetKey(
        WorkspaceOperationTargetDto? target,
        out string targetKey)
    {
        if (string.IsNullOrWhiteSpace(target?.BundleId) ||
            string.IsNullOrWhiteSpace(target.AssetId))
        {
            targetKey = string.Empty;
            return false;
        }

        targetKey = CreateTargetKey(target);
        return true;
    }

    private static string CreateTargetKey(WorkspaceOperationTargetDto target) =>
        string.Concat(target.BundleId!.Trim(), "\n", target.AssetId!.Trim());

    private sealed class ReplacementRetentionState
    {
        public string? CurrentOperationId { get; set; }

        public List<string?> UndoStack { get; } = [];

        public List<string?> RedoStack { get; } = [];
    }
}
