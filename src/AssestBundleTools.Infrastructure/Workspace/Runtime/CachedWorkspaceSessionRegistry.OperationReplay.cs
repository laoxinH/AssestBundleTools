using System.IO;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
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
            ApplyPersistedSpriteEditOperation(workspaceState, operation);
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
            assetState.AppliedSpriteEdit = null;
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

    private static void ApplyPersistedSpriteEditOperation(
        WorkspaceSessionState workspaceState,
        WorkspaceOperationRecordDto operation)
    {
        if (operation.OperationKind != WorkspaceOperationKind.EditDraftApplied ||
            operation.Result?.SpriteEditGeometry is null ||
            string.IsNullOrWhiteSpace(operation.Target?.BundleId) ||
            string.IsNullOrWhiteSpace(operation.Target.AssetId) ||
            !workspaceState.BundlesById.TryGetValue(operation.Target.BundleId, out var targetBundleState))
        {
            return;
        }

        var targetAssetState = targetBundleState.Assets.FirstOrDefault(asset =>
            string.Equals(asset.AssetId.Value, operation.Target.AssetId, StringComparison.Ordinal));
        if (targetAssetState is null || targetAssetState.AssetType != AssetType.Sprite)
        {
            return;
        }

        ApplySpriteEditState(
            targetAssetState,
            new AppliedSpriteEditState(
                operation.Source?.DraftId ?? operation.Result.DraftId ?? operation.OperationId,
                operation.Source?.DraftMode ?? "sprite-geometry",
                operation.Result.SpriteEditGeometry,
                operation.AppliedAtUtc ?? operation.CompletedAtUtc ?? operation.CreatedAtUtc));
    }
}
