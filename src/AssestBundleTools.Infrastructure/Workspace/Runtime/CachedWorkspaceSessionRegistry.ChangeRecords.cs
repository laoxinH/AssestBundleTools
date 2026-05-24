using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
    private readonly WorkspaceReplacementStateMaterializer _replacementStateMaterializer = new();
    private readonly WorkspaceSpriteEditStateMaterializer _spriteEditStateMaterializer = new();

    private IReadOnlyList<AssetChangeRecordDto> BuildAssetChangeRecords(
        WorkspaceSessionState workspaceState,
        CachedBundleState bundleState,
        CachedAssetState assetState)
    {
        var persistedRecords = LoadPersistedAssetChangeRecords(
                workspaceState.Summary,
                bundleState.BundleId,
                assetState.AssetId)
            .ToArray();
        return persistedRecords.Length > 0
            ? persistedRecords
            : BuildRuntimeAssetChangeRecordFallback(assetState);
    }

    private IEnumerable<AssetChangeRecordDto> LoadPersistedAssetChangeRecords(
        WorkspaceSummaryDto workspaceSummary,
        BundleId bundleId,
        AssetId assetId)
    {
        if (string.IsNullOrWhiteSpace(workspaceSummary.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(workspaceSummary.WorkspaceDataRootPath))
        {
            yield break;
        }

        WorkspaceChangeSetDto? changeSet;
        try
        {
            var changeStore = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(
                workspaceSummary.WorkspaceDataRootPath.Trim());
            changeSet = changeStore.Load(workspaceSummary.WorkspaceKey.Trim());
        }
        catch (NotSupportedException)
        {
            yield break;
        }

        var operations = (changeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .ToArray();
        var effectiveReplacementOperationIds = _replacementStateMaterializer
            .Materialize(changeSet)
            .EffectiveReplacements
            .Select(static replacement => replacement.OperationId)
            .ToHashSet(StringComparer.Ordinal);
        var effectiveSpriteEditOperationId = _spriteEditStateMaterializer.FindLatestEffectiveSpriteEditOperationId(
            changeSet,
            bundleId.Value,
            assetId.Value);

        foreach (var operation in operations
            .Where(static operation => operation.OperationKind is
                WorkspaceOperationKind.ExtractAsset or
                WorkspaceOperationKind.ReplaceFromFile or
                WorkspaceOperationKind.ReplaceFromWorkspace or
                WorkspaceOperationKind.EditDraftApplied)
            .Where(operation => OperationConcernsAssetChangeRecord(operation, bundleId, assetId))
            .OrderByDescending(static operation => operation.Sequence))
        {
            var isRelatedOperation = !OperationTargetsAsset(operation, bundleId, assetId);
            var record = AssetChangeRecordDto.FromOperation(
                operation,
                effectiveReplacementOperationIds.Contains(operation.OperationId) ||
                string.Equals(operation.OperationId, effectiveSpriteEditOperationId, StringComparison.Ordinal),
                isRelatedOperation);
            if (record.Kind != AssetChangeRecordKind.Unknown)
            {
                yield return record;
            }
        }
    }

    private static IReadOnlyList<AssetChangeRecordDto> BuildRuntimeAssetChangeRecordFallback(CachedAssetState assetState)
    {
        var records = new List<AssetChangeRecordDto>();
        if (assetState.AppliedFileReplacement is { } fileReplacement)
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.FileReplacement,
                CreatedAtUtc: fileReplacement.AppliedAtUtc,
                AppliedAtUtc: fileReplacement.AppliedAtUtc,
                SourceFileName: fileReplacement.SourceFileName,
                SourceFilePath: fileReplacement.SourceFilePath,
                IsEffectiveReplacement: true));
        }

        if (assetState.AppliedWorkspaceReplacement is { } workspaceReplacement)
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.WorkspaceReplacement,
                CreatedAtUtc: workspaceReplacement.AppliedAtUtc,
                AppliedAtUtc: workspaceReplacement.AppliedAtUtc,
                SourceBundleDisplayName: workspaceReplacement.SourceBundleDisplayName,
                SourceAssetName: workspaceReplacement.SourceAssetName,
                IsEffectiveReplacement: true));
        }

        if (assetState.LastExtractedAtUtc is { } extractedAtUtc)
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.Extraction,
                CreatedAtUtc: extractedAtUtc,
                AppliedAtUtc: extractedAtUtc));
        }

        if (assetState.AppliedSpriteEdit is { } spriteEdit)
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.SpriteEdit,
                CreatedAtUtc: spriteEdit.AppliedAtUtc,
                AppliedAtUtc: spriteEdit.AppliedAtUtc,
                DraftId: spriteEdit.DraftId,
                DraftMode: spriteEdit.DraftMode,
                IsEffectiveEdit: true));
        }

        return records
            .OrderByDescending(static record => record.DisplayTimestampUtc)
            .ToArray();
    }

    private static bool OperationTargetsAsset(
        WorkspaceOperationRecordDto operation,
        BundleId bundleId,
        AssetId assetId) =>
        string.Equals(operation.Target?.BundleId, bundleId.Value, StringComparison.Ordinal) &&
        string.Equals(operation.Target?.AssetId, assetId.Value, StringComparison.Ordinal);

    private static bool OperationConcernsAssetChangeRecord(
        WorkspaceOperationRecordDto operation,
        BundleId bundleId,
        AssetId assetId)
    {
        if (OperationTargetsAsset(operation, bundleId, assetId))
        {
            return true;
        }

        return operation.OperationKind is WorkspaceOperationKind.ReplaceFromFile or WorkspaceOperationKind.ReplaceFromWorkspace &&
               WorkspaceOperationRelationMatcher.OperationConcernsTarget(
                   operation,
                   bundleId.Value,
                   assetId.Value);
    }
}
