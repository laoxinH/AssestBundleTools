using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 将文本编辑操作记录物化为当前目标资源的有效草稿引用。
/// </summary>
public sealed class WorkspaceTextEditStateMaterializer
{
    public WorkspaceTextEditMaterializedStateDto Materialize(WorkspaceChangeSetDto? changeSet)
    {
        var editsByTarget = new Dictionary<TextEditTargetKey, WorkspaceEffectiveTextEditDto>();
        foreach (var operation in NormalizeTextEditOperations(changeSet))
        {
            if (!TryCreateTargetKey(operation.Target, out var targetKey))
            {
                continue;
            }

            var draftId = NormalizeOptional(operation.Source?.DraftId) ??
                          NormalizeOptional(operation.Result?.DraftId);
            var draftMode = NormalizeOptional(operation.Source?.DraftMode);
            if (draftId is null || draftMode is null)
            {
                continue;
            }

            editsByTarget[targetKey] = new WorkspaceEffectiveTextEditDto(
                operation.OperationId,
                operation.Sequence,
                operation.Target!,
                draftId,
                draftMode,
                operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc,
                operation.Result?.DraftRevision);
        }

        return new WorkspaceTextEditMaterializedStateDto(
            editsByTarget.Values
                .OrderBy(static edit => edit.Sequence)
                .ToArray());
    }

    public string? FindLatestEffectiveTextEditOperationId(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId)
    {
        if (!TryCreateTargetKey(bundleId, assetId, out var targetKey))
        {
            return null;
        }

        return Materialize(changeSet)
            .EffectiveEdits
            .Where(edit => TargetMatches(edit.Target, targetKey))
            .OrderByDescending(static edit => edit.Sequence)
            .Select(static edit => edit.OperationId)
            .FirstOrDefault();
    }

    public WorkspaceEffectiveTextEditDto? FindEffectiveTextEdit(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId)
    {
        if (!TryCreateTargetKey(bundleId, assetId, out var targetKey))
        {
            return null;
        }

        return Materialize(changeSet)
            .EffectiveEdits
            .Where(edit => TargetMatches(edit.Target, targetKey))
            .OrderByDescending(static edit => edit.Sequence)
            .FirstOrDefault();
    }

    public bool ContainsTextEditOperation(
        WorkspaceChangeSetDto? changeSet,
        string bundleId,
        string assetId)
    {
        if (!TryCreateTargetKey(bundleId, assetId, out var targetKey))
        {
            return false;
        }

        return (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Any(operation =>
                operation is not null &&
                IsTextEditOperationKind(operation.OperationKind) &&
                TargetMatches(operation.Target, targetKey) &&
                IsTextContentDraftOperation(operation));
    }

    public static bool IsTextContentDraftOperation(WorkspaceOperationRecordDto operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return string.Equals(
            operation.Source?.DraftMode?.Trim(),
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            StringComparison.Ordinal);
    }

    private static IReadOnlyList<WorkspaceOperationRecordDto> NormalizeTextEditOperations(
        WorkspaceChangeSetDto? changeSet) =>
        (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind == WorkspaceOperationKind.EditDraftApplied)
            .Where(IsTextContentDraftOperation)
            .OrderBy(static operation => operation.Sequence)
            .ToArray();

    private static bool TargetMatches(
        WorkspaceOperationTargetDto? target,
        TextEditTargetKey targetKey) =>
        target is not null &&
        string.Equals(target.BundleId, targetKey.BundleId, StringComparison.Ordinal) &&
        string.Equals(target.AssetId, targetKey.AssetId, StringComparison.Ordinal);

    private static bool TryCreateTargetKey(
        WorkspaceOperationTargetDto? target,
        out TextEditTargetKey targetKey)
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
        out TextEditTargetKey targetKey)
    {
        if (string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(assetId))
        {
            targetKey = default;
            return false;
        }

        targetKey = new TextEditTargetKey(bundleId.Trim(), assetId.Trim());
        return true;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsTextEditOperationKind(WorkspaceOperationKind operationKind) =>
        operationKind is
            WorkspaceOperationKind.EditDraftCreated or
            WorkspaceOperationKind.EditDraftUpdated or
            WorkspaceOperationKind.EditDraftApplied or
            WorkspaceOperationKind.EditDraftDiscarded;

    private readonly record struct TextEditTargetKey(string BundleId, string AssetId);
}

public sealed record WorkspaceTextEditMaterializedStateDto(
    IReadOnlyList<WorkspaceEffectiveTextEditDto> EffectiveEdits);

public sealed record WorkspaceEffectiveTextEditDto(
    string OperationId,
    long Sequence,
    WorkspaceOperationTargetDto Target,
    string DraftId,
    string DraftMode,
    DateTimeOffset AppliedAtUtc,
    int? DraftRevision = null);
