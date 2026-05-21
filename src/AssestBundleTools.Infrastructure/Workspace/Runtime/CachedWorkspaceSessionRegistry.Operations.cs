using System.IO;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
    public bool TryGetWorkspaceChangeSummary(
        WorkspaceId workspaceId,
        out WorkspaceChangeSummaryDto? changeSummary)
    {
        lock (_syncRoot)
        {
            if (!TryGetWorkspaceStateUnsafe(workspaceId, out var workspaceState))
            {
                changeSummary = null;
                return false;
            }

            var items = workspaceState.BundlesById.Values
                .SelectMany(bundleState => bundleState.Assets
                    .Select(assetState => BuildWorkspaceChangeItem(bundleState, assetState)))
                .Where(static item => item is not null)
                .Select(static item => item!)
                .OrderByDescending(static item => item.LastChangedAtUtc)
                .ThenBy(item => item.BundleDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.AssetName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            changeSummary = new WorkspaceChangeSummaryDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                WorkspaceDisplayName: workspaceState.Summary.DisplayName,
                SourcePath: workspaceState.Summary.SourcePath,
                BundleCount: workspaceState.Summary.BundleCount,
                AssetCount: workspaceState.Summary.AssetCount,
                TotalChangeCount: items.Length,
                ModifiedAssetCount: items.Count(static item => item.IsModified),
                ExtractedAssetCount: items.Count(static item => item.LastExtractedAtUtc.HasValue),
                WorkspaceReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName)),
                FileReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName)),
                Items: items);

            return true;
        }
    }

    public bool TryExtractAsset(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out AssetExtractionResultDto? extractionResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState) ||
                !assetState.CanExtract)
            {
                extractionResult = null;
                return false;
            }

            var extractedAtUtc = DateTimeOffset.UtcNow;
            assetState.LastExtractedAtUtc = extractedAtUtc;

            extractionResult = new AssetExtractionResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                BundleId: bundleState.BundleId,
                AssetId: assetState.AssetId,
                AssetName: assetState.Name,
                SuggestedOutputPath: BuildSuggestedOutputPath(bundleState.DisplayName, assetState.Name, assetState.AssetType),
                ExtractedAtUtc: extractedAtUtc);

            return true;
        }
    }

    public bool TryGetWorkspaceReplacementCandidates(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        AssetId targetAssetId,
        out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, targetBundleId, targetAssetId, out var targetAssetState, out _, out var workspaceState) ||
                !targetAssetState.CanReplace)
            {
                candidates = Array.Empty<WorkspaceReplacementCandidateDto>();
                return false;
            }

            candidates = workspaceState.BundlesById.Values
                .SelectMany(bundle => bundle.Assets.Select(asset => (Bundle: bundle, Asset: asset)))
                .Where(item => !item.Asset.AssetId.Equals(targetAssetId))
                .Where(item => item.Asset.AssetType == targetAssetState.AssetType)
                .Where(item => item.Asset.CanReplace)
                .OrderBy(item => item.Bundle.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Asset.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new WorkspaceReplacementCandidateDto(
                    SourceBundleId: item.Bundle.BundleId,
                    SourceAssetId: item.Asset.AssetId,
                    SourceBundleDisplayName: item.Bundle.DisplayName,
                    SourceAssetName: item.Asset.Name,
                    SourceAssetType: item.Asset.AssetType))
                .ToArray();

            return true;
        }
    }

    public bool TryReplaceAssetFromFile(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        AssetId targetAssetId,
        string sourceFilePath,
        AssetType sourceAssetType,
        out AssetFileReplacementResultDto? replacementResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, targetBundleId, targetAssetId, out var targetAssetState, out var targetBundleState, out var workspaceState) ||
                !targetAssetState.CanReplace ||
                targetAssetState.AssetType != sourceAssetType)
            {
                replacementResult = null;
                return false;
            }

            var appliedAtUtc = DateTimeOffset.UtcNow;
            var normalizedSourcePath = sourceFilePath.Trim();
            var sourceFileName = Path.GetFileName(normalizedSourcePath);
            if (string.IsNullOrWhiteSpace(sourceFileName))
            {
                sourceFileName = normalizedSourcePath;
            }

            ApplyReplacementSnapshot(
                targetAssetState,
                new AppliedReplacementSnapshot(
                    WorkspaceReplacement: null,
                    FileReplacement: new AppliedFileReplacementState(
                        normalizedSourcePath,
                        sourceFileName,
                        sourceAssetType,
                        appliedAtUtc)));

            replacementResult = new AssetFileReplacementResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                TargetBundleId: targetBundleState.BundleId,
                TargetAssetId: targetAssetState.AssetId,
                TargetAssetName: targetAssetState.Name,
                SourceFilePath: normalizedSourcePath,
                SourceFileName: sourceFileName,
                SourceAssetType: sourceAssetType,
                ReplacedAtUtc: appliedAtUtc);

            return true;
        }
    }

    public bool TryReplaceAssetFromWorkspace(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        AssetId targetAssetId,
        BundleId sourceBundleId,
        AssetId sourceAssetId,
        out AssetReplacementResultDto? replacementResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, targetBundleId, targetAssetId, out var targetAssetState, out var targetBundleState, out var workspaceState) ||
                !TryGetAssetStateUnsafe(workspaceId, sourceBundleId, sourceAssetId, out var sourceAssetState, out var sourceBundleState, out _) ||
                !targetAssetState.CanReplace ||
                targetAssetState.AssetType != sourceAssetState.AssetType)
            {
                replacementResult = null;
                return false;
            }

            var replacedAtUtc = DateTimeOffset.UtcNow;
            ApplyReplacementSnapshot(
                targetAssetState,
                new AppliedReplacementSnapshot(
                    WorkspaceReplacement: new AppliedWorkspaceReplacementState(
                        sourceBundleState.BundleId,
                        sourceAssetState.AssetId,
                        sourceBundleState.DisplayName,
                        sourceAssetState.Name,
                        sourceAssetState.AssetType,
                        replacedAtUtc),
                    FileReplacement: null));

            replacementResult = new AssetReplacementResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                TargetBundleId: targetBundleState.BundleId,
                TargetAssetId: targetAssetState.AssetId,
                TargetAssetName: targetAssetState.Name,
                SourceBundleId: sourceBundleState.BundleId,
                SourceAssetId: sourceAssetState.AssetId,
                SourceBundleDisplayName: sourceBundleState.DisplayName,
                SourceAssetName: sourceAssetState.Name,
                SourceAssetType: sourceAssetState.AssetType,
                ReplacedAtUtc: replacedAtUtc);

            return true;
        }
    }

    public bool TryUndoAssetReplacement(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out AssetReplacementUndoResultDto? undoResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState) ||
                (assetState.AppliedWorkspaceReplacement is null && assetState.AppliedFileReplacement is null))
            {
                undoResult = null;
                return false;
            }

            UndoReplacementSnapshot(assetState);

            undoResult = new AssetReplacementUndoResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                TargetBundleId: bundleState.BundleId,
                TargetAssetId: assetState.AssetId,
                TargetAssetName: assetState.Name);

            return true;
        }
    }

    public bool TryRedoAssetReplacement(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out AssetReplacementRedoResultDto? redoResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState) ||
                assetState.ReplacementRedoStack.Count == 0)
            {
                redoResult = null;
                return false;
            }

            RedoReplacementSnapshot(assetState);

            redoResult = new AssetReplacementRedoResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                TargetBundleId: bundleState.BundleId,
                TargetAssetId: assetState.AssetId,
                TargetAssetName: assetState.Name,
                RedoneAtUtc: DateTimeOffset.UtcNow);

            return true;
        }
    }

    public bool TryReplayWorkspaceOperations(
        WorkspaceId workspaceId,
        WorkspaceChangeSetDto? changeSet)
    {
        lock (_syncRoot)
        {
            if (!TryGetWorkspaceStateUnsafe(workspaceId, out var workspaceState))
            {
                return false;
            }

            ResetOperationBackedAssetState(workspaceState);
            ApplyOperationsToWorkspaceState(workspaceState, changeSet?.Operations);
            return true;
        }
    }

    private void ApplyPersistedOperations(
        WorkspaceSessionOpenRequestDto request,
        WorkspaceSessionState workspaceState)
    {
        if (_operationLogService is null ||
            string.IsNullOrWhiteSpace(request.WorkspaceKey))
        {
            return;
        }

        var changeStore = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(request.WorkspaceDataRootPath);
        var changeSet = changeStore.Load(request.WorkspaceKey);
        ApplyOperationsToWorkspaceState(workspaceState, changeSet?.Operations);
    }

    private static void ApplyOperationsToWorkspaceState(
        WorkspaceSessionState workspaceState,
        IReadOnlyList<WorkspaceOperationRecordDto>? operations)
    {
        foreach (var operation in (operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Where(static operation => operation is not null)
            .Where(static operation => operation.Status == WorkspaceOperationStatus.Applied)
            .OrderBy(static operation => operation.Sequence))
        {
            ApplyPersistedExtractionOperation(workspaceState, operation);
            ApplyPersistedReplacementOperation(workspaceState, operation);
            ApplyPersistedUndoReplacementOperation(workspaceState, operation);
            ApplyPersistedRedoReplacementOperation(workspaceState, operation);
        }
    }

    private static void ResetOperationBackedAssetState(WorkspaceSessionState workspaceState)
    {
        foreach (var assetState in workspaceState.BundlesById.Values.SelectMany(static bundle => bundle.Assets))
        {
            assetState.IsModified = false;
            assetState.LastExtractedAtUtc = null;
            assetState.AppliedWorkspaceReplacement = null;
            assetState.AppliedFileReplacement = null;
            assetState.ReplacementUndoStack.Clear();
            assetState.ReplacementRedoStack.Clear();
        }
    }

    private static void ApplyPersistedExtractionOperation(
        WorkspaceSessionState workspaceState,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.OperationKind != WorkspaceOperationKind.ExtractAsset ||
            string.IsNullOrWhiteSpace(operation.Target?.BundleId) ||
            string.IsNullOrWhiteSpace(operation.Target.AssetId) ||
            !workspaceState.BundlesById.TryGetValue(operation.Target.BundleId, out var targetBundleState))
        {
            return;
        }

        var targetAssetState = targetBundleState.Assets.FirstOrDefault(asset =>
            string.Equals(asset.AssetId.Value, operation.Target.AssetId, StringComparison.Ordinal));
        if (targetAssetState is null || !targetAssetState.CanExtract)
        {
            return;
        }

        targetAssetState.LastExtractedAtUtc = operation.AppliedAtUtc ??
                                             operation.CompletedAtUtc ??
                                             operation.CreatedAtUtc;
    }

    private static void ApplyPersistedReplacementOperation(
        WorkspaceSessionState workspaceState,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.OperationKind is not (WorkspaceOperationKind.ReplaceFromFile or WorkspaceOperationKind.ReplaceFromWorkspace) ||
            string.IsNullOrWhiteSpace(operation.Target?.BundleId) ||
            string.IsNullOrWhiteSpace(operation.Target.AssetId) ||
            !workspaceState.BundlesById.TryGetValue(operation.Target.BundleId, out var targetBundleState))
        {
            return;
        }

        var targetAssetState = targetBundleState.Assets.FirstOrDefault(asset =>
            string.Equals(asset.AssetId.Value, operation.Target.AssetId, StringComparison.Ordinal));
        if (targetAssetState is null || !targetAssetState.CanReplace)
        {
            return;
        }

        if (operation.OperationKind == WorkspaceOperationKind.ReplaceFromFile)
        {
            ApplyPersistedFileReplacement(targetAssetState, operation);
            return;
        }

        ApplyPersistedWorkspaceReplacement(workspaceState, targetAssetState, operation);
    }

    private static void ApplyPersistedFileReplacement(
        CachedAssetState targetAssetState,
        WorkspaceOperationRecordDto operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Source?.SourceFilePath))
        {
            return;
        }

        var sourceFilePath = operation.Source.SourceFilePath.Trim();
        var sourceFileName = string.IsNullOrWhiteSpace(operation.Source.SourceFileName)
            ? Path.GetFileName(sourceFilePath)
            : operation.Source.SourceFileName.Trim();
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            sourceFileName = sourceFilePath;
        }

        ApplyReplacementSnapshot(
            targetAssetState,
            new AppliedReplacementSnapshot(
                WorkspaceReplacement: null,
                FileReplacement: new AppliedFileReplacementState(
                    sourceFilePath,
                    sourceFileName,
                    ParseAssetType(operation.Source.SourceAssetType ?? targetAssetState.AssetType.ToString()),
                    operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc)));
    }

    private static void ApplyPersistedWorkspaceReplacement(
        WorkspaceSessionState workspaceState,
        CachedAssetState targetAssetState,
        WorkspaceOperationRecordDto operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Source?.SourceBundleId) ||
            string.IsNullOrWhiteSpace(operation.Source.SourceAssetId) ||
            !workspaceState.BundlesById.TryGetValue(operation.Source.SourceBundleId, out var sourceBundleState))
        {
            return;
        }

        var sourceAssetState = sourceBundleState.Assets.FirstOrDefault(asset =>
            string.Equals(asset.AssetId.Value, operation.Source.SourceAssetId, StringComparison.Ordinal));
        if (sourceAssetState is null ||
            sourceAssetState.AssetType != targetAssetState.AssetType)
        {
            return;
        }

        ApplyReplacementSnapshot(
            targetAssetState,
            new AppliedReplacementSnapshot(
                WorkspaceReplacement: new AppliedWorkspaceReplacementState(
                    sourceBundleState.BundleId,
                    sourceAssetState.AssetId,
                    sourceBundleState.DisplayName,
                    sourceAssetState.Name,
                    sourceAssetState.AssetType,
                    operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc),
                FileReplacement: null));
    }

    private static void ApplyPersistedUndoReplacementOperation(
        WorkspaceSessionState workspaceState,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.OperationKind != WorkspaceOperationKind.UndoReplacement ||
            string.IsNullOrWhiteSpace(operation.Target?.BundleId) ||
            string.IsNullOrWhiteSpace(operation.Target.AssetId) ||
            !workspaceState.BundlesById.TryGetValue(operation.Target.BundleId, out var targetBundleState))
        {
            return;
        }

        var targetAssetState = targetBundleState.Assets.FirstOrDefault(asset =>
            string.Equals(asset.AssetId.Value, operation.Target.AssetId, StringComparison.Ordinal));
        if (targetAssetState is null || !targetAssetState.CanReplace)
        {
            return;
        }

        UndoReplacementSnapshot(targetAssetState);
    }

    private static void ApplyPersistedRedoReplacementOperation(
        WorkspaceSessionState workspaceState,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.OperationKind != WorkspaceOperationKind.RedoReplacement ||
            string.IsNullOrWhiteSpace(operation.Target?.BundleId) ||
            string.IsNullOrWhiteSpace(operation.Target.AssetId) ||
            !workspaceState.BundlesById.TryGetValue(operation.Target.BundleId, out var targetBundleState))
        {
            return;
        }

        var targetAssetState = targetBundleState.Assets.FirstOrDefault(asset =>
            string.Equals(asset.AssetId.Value, operation.Target.AssetId, StringComparison.Ordinal));
        if (targetAssetState is null || !targetAssetState.CanReplace)
        {
            return;
        }

        if (operation.Source is not null)
        {
            ApplyPersistedExplicitRedoReplacement(workspaceState, targetAssetState, operation);
            return;
        }

        RedoReplacementSnapshot(targetAssetState);
    }

    private static void ApplyPersistedExplicitRedoReplacement(
        WorkspaceSessionState workspaceState,
        CachedAssetState targetAssetState,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.Source?.SourceFilePath is { } sourceFilePath &&
            !string.IsNullOrWhiteSpace(sourceFilePath))
        {
            ApplyPersistedFileReplacement(targetAssetState, operation);
            return;
        }

        ApplyPersistedWorkspaceReplacement(workspaceState, targetAssetState, operation);
    }
}
