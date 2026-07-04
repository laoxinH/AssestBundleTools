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
    private readonly WorkspaceTextEditStateMaterializer _textEditStateMaterializer = new();

    private IReadOnlyList<AssetChangeRecordDto> BuildAssetChangeRecords(
        WorkspaceSessionState workspaceState,
        CachedBundleState bundleState,
        CachedAssetState assetState)
    {
        var persistedRecords = LoadPersistedAssetChangeRecords(
                workspaceState,
                bundleState.BundleId,
                assetState)
            .ToArray();
        return persistedRecords.Length > 0
            ? persistedRecords
            : BuildRuntimeAssetChangeRecordFallback(assetState);
    }

    private IEnumerable<AssetChangeRecordDto> LoadPersistedAssetChangeRecords(
        WorkspaceSessionState workspaceState,
        BundleId bundleId,
        CachedAssetState assetState)
    {
        var workspaceSummary = workspaceState.Summary;
        var assetId = assetState.AssetId;
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
        var effectiveTextEditOperationId = _textEditStateMaterializer.FindLatestEffectiveTextEditOperationId(
            changeSet,
            bundleId.Value,
            assetId.Value);
        var implicitDependencyAssetKeys = GetImplicitDependencyAssetKeys(workspaceState, bundleId, assetState);

        foreach (var operation in operations
            .Where(static operation => operation.OperationKind is
                WorkspaceOperationKind.ExtractAsset or
                WorkspaceOperationKind.ReplaceFromFile or
                WorkspaceOperationKind.ReplaceFromWorkspace or
                WorkspaceOperationKind.EditDraftApplied)
            .Where(operation => OperationConcernsAssetChangeRecord(operation, bundleId, assetId, implicitDependencyAssetKeys))
            .OrderByDescending(static operation => operation.Sequence))
        {
            var isRelatedOperation = !OperationTargetsAsset(operation, bundleId, assetId);
            var record = AssetChangeRecordDto.FromOperation(
                operation,
                effectiveReplacementOperationIds.Contains(operation.OperationId) ||
                string.Equals(operation.OperationId, effectiveSpriteEditOperationId, StringComparison.Ordinal) ||
                string.Equals(operation.OperationId, effectiveTextEditOperationId, StringComparison.Ordinal),
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

        if (assetState.AppliedTextEdit is { } textEdit)
        {
            records.Add(new AssetChangeRecordDto(
                Kind: AssetChangeRecordKind.TextEdit,
                CreatedAtUtc: textEdit.AppliedAtUtc,
                AppliedAtUtc: textEdit.AppliedAtUtc,
                DraftId: textEdit.DraftId,
                DraftMode: textEdit.DraftMode,
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
        AssetId assetId,
        ISet<string> implicitDependencyAssetKeys)
    {
        if (OperationTargetsAsset(operation, bundleId, assetId))
        {
            return true;
        }

        if (operation.OperationKind is not (WorkspaceOperationKind.ReplaceFromFile or WorkspaceOperationKind.ReplaceFromWorkspace))
        {
            return false;
        }

        if (WorkspaceOperationRelationMatcher.OperationConcernsTarget(
                operation,
                bundleId.Value,
                assetId.Value))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(operation.Target?.BundleId) &&
               !string.IsNullOrWhiteSpace(operation.Target.AssetId) &&
               implicitDependencyAssetKeys.Contains(CreateAssetKey(operation.Target.BundleId, operation.Target.AssetId));
    }

    private static ISet<string> GetImplicitDependencyAssetKeys(
        WorkspaceSessionState workspaceState,
        BundleId bundleId,
        CachedAssetState assetState)
    {
        var assetKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dependency in assetState.Dependencies)
        {
            if (!string.IsNullOrWhiteSpace(dependency.BundleId.Value) &&
                !string.IsNullOrWhiteSpace(dependency.AssetId.Value))
            {
                assetKeys.Add(CreateAssetKey(dependency.BundleId.Value, dependency.AssetId.Value));
            }
        }

        if (workspaceState.TryGetTextureAssociations(bundleId, assetState.AssetId, out var textureAssociations))
        {
            foreach (var textureAssociation in textureAssociations)
            {
                if (!string.IsNullOrWhiteSpace(textureAssociation.BundleId.Value) &&
                    !string.IsNullOrWhiteSpace(textureAssociation.AssetId.Value))
                {
                    assetKeys.Add(CreateAssetKey(textureAssociation.BundleId.Value, textureAssociation.AssetId.Value));
                }
            }
        }

        return assetKeys;
    }

}
