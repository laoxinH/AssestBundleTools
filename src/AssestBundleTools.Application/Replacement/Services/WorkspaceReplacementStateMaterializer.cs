using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 把替换、撤销和重做操作记录物化为当前有效替换与目标级历史栈。
/// </summary>
public sealed class WorkspaceReplacementStateMaterializer
{
    public WorkspaceReplacementMaterializedStateDto Materialize(WorkspaceChangeSetDto? changeSet)
    {
        var statesByTarget = new Dictionary<ReplacementTargetKey, TargetReplacementState>();

        foreach (var operation in NormalizeReplacementOperations(changeSet))
        {
            if (!TryCreateTargetKey(operation.Target, out var targetKey))
            {
                continue;
            }

            var state = GetOrCreateState(statesByTarget, targetKey);
            switch (operation.OperationKind)
            {
                case WorkspaceOperationKind.ReplaceFromFile:
                case WorkspaceOperationKind.ReplaceFromWorkspace:
                    ApplyReplacement(state, operation);
                    break;
                case WorkspaceOperationKind.UndoReplacement:
                    ApplyUndo(state);
                    break;
                case WorkspaceOperationKind.RedoReplacement:
                    ApplyRedo(state);
                    break;
            }
        }

        var effectiveReplacements = statesByTarget.Values
            .Where(static state => state.Current is not null)
            .Select(static state => state.Current!)
            .OrderBy(static replacement => replacement.Sequence)
            .ToArray();

        return new WorkspaceReplacementMaterializedStateDto(effectiveReplacements);
    }

    public string? FindLatestEffectiveReplacementOperationId(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId)
    {
        if (!TryCreateTargetKey(bundleId, assetId, out var targetKey))
        {
            return null;
        }

        return Materialize(changeSet)
            .EffectiveReplacements
            .Where(replacement => TargetMatches(replacement.Target, targetKey))
            .OrderByDescending(static replacement => replacement.Sequence)
            .Select(static replacement => replacement.OperationId)
            .FirstOrDefault();
    }

    public string? FindLatestUndoOperationId(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId)
    {
        if (!TryCreateTargetKey(bundleId, assetId, out var targetKey))
        {
            return null;
        }

        return NormalizeReplacementOperations(changeSet)
            .Where(static operation => operation.OperationKind == WorkspaceOperationKind.UndoReplacement)
            .Where(operation => TargetMatches(operation.Target, targetKey))
            .OrderByDescending(static operation => operation.Sequence)
            .Select(static operation => operation.OperationId)
            .FirstOrDefault();
    }

    private static IReadOnlyList<WorkspaceOperationRecordDto> NormalizeReplacementOperations(
        WorkspaceChangeSetDto? changeSet) =>
        (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind is
                WorkspaceOperationKind.ReplaceFromFile or
                WorkspaceOperationKind.ReplaceFromWorkspace or
                WorkspaceOperationKind.UndoReplacement or
                WorkspaceOperationKind.RedoReplacement)
            .OrderBy(static operation => operation.Sequence)
            .ToArray();

    private static TargetReplacementState GetOrCreateState(
        IDictionary<ReplacementTargetKey, TargetReplacementState> statesByTarget,
        ReplacementTargetKey targetKey)
    {
        if (!statesByTarget.TryGetValue(targetKey, out var state))
        {
            state = new TargetReplacementState();
            statesByTarget[targetKey] = state;
        }

        return state;
    }

    private static void ApplyReplacement(
        TargetReplacementState state,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.Target is null || operation.Source is null)
        {
            return;
        }

        state.UndoStack.Add(state.Current);
        state.RedoStack.Clear();
        state.Current = new WorkspaceEffectiveReplacementDto(
            operation.OperationId,
            operation.Sequence,
            operation.Target,
            operation.Source,
            operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc);
    }

    private static void ApplyUndo(TargetReplacementState state)
    {
        if (state.Current is null)
        {
            return;
        }

        state.RedoStack.Add(state.Current);
        state.Current = PopOrDefault(state.UndoStack);
    }

    private static void ApplyRedo(TargetReplacementState state)
    {
        var redoReplacement = PopOrDefault(state.RedoStack);
        if (redoReplacement is null)
        {
            return;
        }

        state.UndoStack.Add(state.Current);
        state.Current = redoReplacement;
    }

    private static WorkspaceEffectiveReplacementDto? PopOrDefault(
        IList<WorkspaceEffectiveReplacementDto?> stack)
    {
        if (stack.Count == 0)
        {
            return null;
        }

        var value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }

    private static bool TargetMatches(
        WorkspaceOperationTargetDto? target,
        ReplacementTargetKey targetKey) =>
        target is not null &&
        string.Equals(target.BundleId, targetKey.BundleId, StringComparison.Ordinal) &&
        string.Equals(target.AssetId, targetKey.AssetId, StringComparison.Ordinal);

    private static bool TryCreateTargetKey(
        WorkspaceOperationTargetDto? target,
        out ReplacementTargetKey targetKey)
    {
        if (target is null)
        {
            targetKey = default;
            return false;
        }

        return TryCreateTargetKey(target.BundleId, target.AssetId, out targetKey);
    }

    private static bool TryCreateTargetKey(
        string? bundleId,
        string? assetId,
        out ReplacementTargetKey targetKey)
    {
        if (string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(assetId))
        {
            targetKey = default;
            return false;
        }

        targetKey = new ReplacementTargetKey(bundleId.Trim(), assetId.Trim());
        return true;
    }

    private readonly record struct ReplacementTargetKey(string BundleId, string AssetId);

    private sealed class TargetReplacementState
    {
        public WorkspaceEffectiveReplacementDto? Current { get; set; }

        public List<WorkspaceEffectiveReplacementDto?> UndoStack { get; } = [];

        public List<WorkspaceEffectiveReplacementDto?> RedoStack { get; } = [];
    }
}

public sealed record WorkspaceReplacementMaterializedStateDto(
    IReadOnlyList<WorkspaceEffectiveReplacementDto> EffectiveReplacements);
