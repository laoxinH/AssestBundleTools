using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 将 Sprite 编辑操作记录物化为当前目标资源的有效几何。
/// </summary>
public sealed class WorkspaceSpriteEditStateMaterializer
{
    public WorkspaceSpriteEditMaterializedStateDto Materialize(WorkspaceChangeSetDto? changeSet)
    {
        var editsByTarget = new Dictionary<SpriteEditTargetKey, WorkspaceEffectiveSpriteEditDto>();
        foreach (var operation in NormalizeSpriteEditOperations(changeSet))
        {
            if (!TryCreateTargetKey(operation.Target, out var targetKey) ||
                operation.Result?.SpriteEditGeometry is null)
            {
                continue;
            }

            editsByTarget[targetKey] = new WorkspaceEffectiveSpriteEditDto(
                operation.OperationId,
                operation.Sequence,
                operation.Target!,
                operation.Result.SpriteEditGeometry,
                operation.Source?.DraftId ?? operation.Result.DraftId,
                operation.Source?.DraftMode,
                operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc);
        }

        return new WorkspaceSpriteEditMaterializedStateDto(
            editsByTarget.Values
                .OrderBy(static edit => edit.Sequence)
                .ToArray());
    }

    public string? FindLatestEffectiveSpriteEditOperationId(
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

    private static IReadOnlyList<WorkspaceOperationRecordDto> NormalizeSpriteEditOperations(
        WorkspaceChangeSetDto? changeSet) =>
        (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .Where(static operation => operation.OperationKind == WorkspaceOperationKind.EditDraftApplied)
            .Where(static operation => string.Equals(
                operation.Source?.DraftMode,
                WorkspaceSpriteEditOperationRecorder.SpriteGeometryDraftMode,
                StringComparison.Ordinal))
            .OrderBy(static operation => operation.Sequence)
            .ToArray();

    private static bool TargetMatches(
        WorkspaceOperationTargetDto? target,
        SpriteEditTargetKey targetKey) =>
        target is not null &&
        string.Equals(target.BundleId, targetKey.BundleId, StringComparison.Ordinal) &&
        string.Equals(target.AssetId, targetKey.AssetId, StringComparison.Ordinal);

    private static bool TryCreateTargetKey(
        WorkspaceOperationTargetDto? target,
        out SpriteEditTargetKey targetKey)
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
        out SpriteEditTargetKey targetKey)
    {
        if (string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(assetId))
        {
            targetKey = default;
            return false;
        }

        targetKey = new SpriteEditTargetKey(bundleId.Trim(), assetId.Trim());
        return true;
    }

    private readonly record struct SpriteEditTargetKey(string BundleId, string AssetId);
}

public sealed record WorkspaceSpriteEditMaterializedStateDto(
    IReadOnlyList<WorkspaceEffectiveSpriteEditDto> EffectiveEdits);

public sealed record WorkspaceEffectiveSpriteEditDto(
    string OperationId,
    long Sequence,
    WorkspaceOperationTargetDto Target,
    WorkspaceSpriteEditGeometryDto Geometry,
    string? DraftId,
    string? DraftMode,
    DateTimeOffset AppliedAtUtc);
