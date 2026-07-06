using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 以操作事实流顺序物化 TextAsset 的当前有效内容来源。
/// </summary>
public sealed class WorkspaceTextAssetContentStateMaterializer
{
    public WorkspaceTextAssetContentMaterializedStateDto Materialize(WorkspaceChangeSetDto? changeSet)
    {
        var statesByTarget = new Dictionary<TextAssetContentTargetKey, TargetTextAssetContentState>();
        foreach (var operation in NormalizeContentOperations(changeSet))
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
                    ApplyUndoReplacement(state);
                    break;
                case WorkspaceOperationKind.RedoReplacement:
                    ApplyRedoReplacement(state, operation);
                    break;
                case WorkspaceOperationKind.EditDraftApplied:
                    ApplyTextEdit(state, operation);
                    break;
            }
        }

        return new WorkspaceTextAssetContentMaterializedStateDto(
            statesByTarget.Values
                .Select(static state => state.Current)
                .OfType<WorkspaceEffectiveTextAssetContentDto>()
                .OrderBy(static content => content.Sequence)
                .ToArray());
    }

    public WorkspaceEffectiveTextAssetContentDto? FindEffectiveContent(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId)
    {
        if (!TryCreateTargetKey(bundleId, assetId, out var targetKey))
        {
            return null;
        }

        return Materialize(changeSet)
            .EffectiveContents
            .Where(content => TargetMatches(content.Target, targetKey))
            .OrderByDescending(static content => content.Sequence)
            .FirstOrDefault();
    }

    public string? FindLatestEffectiveReplacementOperationId(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId) =>
        FindEffectiveContent(changeSet, bundleId, assetId) is
        {
            Kind: WorkspaceTextAssetContentKind.FileReplacement or WorkspaceTextAssetContentKind.WorkspaceReplacement
        } effectiveContent
            ? effectiveContent.OperationId
            : null;

    public string? FindLatestEffectiveTextEditOperationId(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId) =>
        FindEffectiveContent(changeSet, bundleId, assetId) is
        {
            Kind: WorkspaceTextAssetContentKind.TextEdit
        } effectiveContent
            ? effectiveContent.OperationId
            : null;

    public bool ContainsTextAssetContentOperation(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId)
    {
        if (!TryCreateTargetKey(bundleId, assetId, out var targetKey))
        {
            return false;
        }

        return NormalizeContentOperations(changeSet)
            .Any(operation => TargetMatches(operation.Target, targetKey));
    }

    private static IReadOnlyList<WorkspaceOperationRecordDto> NormalizeContentOperations(
        WorkspaceChangeSetDto? changeSet) =>
        (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind is
                WorkspaceOperationKind.ReplaceFromFile or
                WorkspaceOperationKind.ReplaceFromWorkspace or
                WorkspaceOperationKind.UndoReplacement or
                WorkspaceOperationKind.RedoReplacement or
                WorkspaceOperationKind.EditDraftApplied)
            .Where(static operation =>
                operation.OperationKind != WorkspaceOperationKind.EditDraftApplied ||
                WorkspaceTextEditStateMaterializer.IsTextContentDraftOperation(operation))
            .OrderBy(static operation => operation.Sequence)
            .ToArray();

    private static TargetTextAssetContentState GetOrCreateState(
        IDictionary<TextAssetContentTargetKey, TargetTextAssetContentState> statesByTarget,
        TextAssetContentTargetKey targetKey)
    {
        if (!statesByTarget.TryGetValue(targetKey, out var state))
        {
            state = new TargetTextAssetContentState();
            statesByTarget[targetKey] = state;
        }

        return state;
    }

    private static void ApplyReplacement(
        TargetTextAssetContentState state,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.Target is null || operation.Source is null)
        {
            return;
        }

        state.UndoStack.Add(state.Current);
        state.RedoStack.Clear();
        state.Current = new WorkspaceEffectiveTextAssetContentDto(
            OperationId: operation.OperationId,
            Sequence: operation.Sequence,
            Target: operation.Target,
            Kind: operation.OperationKind == WorkspaceOperationKind.ReplaceFromFile
                ? WorkspaceTextAssetContentKind.FileReplacement
                : WorkspaceTextAssetContentKind.WorkspaceReplacement,
            AppliedAtUtc: operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc,
            ReplacementSource: operation.Source);
    }

    private static void ApplyUndoReplacement(TargetTextAssetContentState state)
    {
        if (state.Current?.Kind is not (WorkspaceTextAssetContentKind.FileReplacement or WorkspaceTextAssetContentKind.WorkspaceReplacement))
        {
            return;
        }

        state.RedoStack.Add(state.Current);
        state.Current = PopOrDefault(state.UndoStack);
    }

    private static void ApplyRedoReplacement(
        TargetTextAssetContentState state,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.Target is not null &&
            operation.Source is not null &&
            !string.IsNullOrWhiteSpace(operation.SupersedesOperationId))
        {
            state.UndoStack.Add(state.Current);
            state.RedoStack.Clear();
            state.Current = new WorkspaceEffectiveTextAssetContentDto(
                OperationId: operation.SupersedesOperationId.Trim(),
                Sequence: operation.Sequence,
                Target: operation.Target,
                Kind: string.Equals(operation.Source.SourceKind?.Trim(), "workspace", StringComparison.OrdinalIgnoreCase)
                    ? WorkspaceTextAssetContentKind.WorkspaceReplacement
                    : WorkspaceTextAssetContentKind.FileReplacement,
                AppliedAtUtc: operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc,
                ReplacementSource: operation.Source);
            return;
        }

        var redoContent = PopOrDefault(state.RedoStack);
        if (redoContent is null)
        {
            return;
        }

        state.UndoStack.Add(state.Current);
        state.Current = redoContent with
        {
            Sequence = operation.Sequence,
            AppliedAtUtc = operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc
        };
    }

    private static void ApplyTextEdit(
        TargetTextAssetContentState state,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.Target is null)
        {
            return;
        }

        var draftId = NormalizeOptional(operation.Source?.DraftId) ??
                      NormalizeOptional(operation.Result?.DraftId);
        var draftMode = NormalizeOptional(operation.Source?.DraftMode) ??
                        WorkspaceTextEditOperationRecorder.TextDraftMode;
        if (draftId is null)
        {
            return;
        }

        state.UndoStack.Clear();
        state.RedoStack.Clear();
        state.Current = new WorkspaceEffectiveTextAssetContentDto(
            OperationId: operation.OperationId,
            Sequence: operation.Sequence,
            Target: operation.Target,
            Kind: WorkspaceTextAssetContentKind.TextEdit,
            AppliedAtUtc: operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc,
            DraftId: draftId,
            DraftMode: draftMode,
            DraftRevision: operation.Result?.DraftRevision);
    }

    private static WorkspaceEffectiveTextAssetContentDto? PopOrDefault(
        IList<WorkspaceEffectiveTextAssetContentDto?> stack)
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
        TextAssetContentTargetKey targetKey) =>
        target is not null &&
        string.Equals(target.BundleId, targetKey.BundleId, StringComparison.Ordinal) &&
        string.Equals(target.AssetId, targetKey.AssetId, StringComparison.Ordinal);

    private static bool TryCreateTargetKey(
        WorkspaceOperationTargetDto? target,
        out TextAssetContentTargetKey targetKey)
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
        out TextAssetContentTargetKey targetKey)
    {
        if (string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(assetId))
        {
            targetKey = default;
            return false;
        }

        targetKey = new TextAssetContentTargetKey(bundleId.Trim(), assetId.Trim());
        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private readonly record struct TextAssetContentTargetKey(string BundleId, string AssetId);

    private sealed class TargetTextAssetContentState
    {
        public WorkspaceEffectiveTextAssetContentDto? Current { get; set; }

        public List<WorkspaceEffectiveTextAssetContentDto?> UndoStack { get; } = [];

        public List<WorkspaceEffectiveTextAssetContentDto?> RedoStack { get; } = [];
    }
}

public sealed record WorkspaceTextAssetContentMaterializedStateDto(
    IReadOnlyList<WorkspaceEffectiveTextAssetContentDto> EffectiveContents);

public sealed record WorkspaceEffectiveTextAssetContentDto(
    string OperationId,
    long Sequence,
    WorkspaceOperationTargetDto Target,
    WorkspaceTextAssetContentKind Kind,
    DateTimeOffset AppliedAtUtc,
    WorkspaceOperationSourceDto? ReplacementSource = null,
    string? DraftId = null,
    string? DraftMode = null,
    int? DraftRevision = null);

public enum WorkspaceTextAssetContentKind
{
    FileReplacement,
    WorkspaceReplacement,
    TextEdit
}
