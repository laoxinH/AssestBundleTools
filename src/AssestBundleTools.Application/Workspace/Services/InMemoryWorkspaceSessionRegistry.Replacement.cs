using System.IO;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Services;

public sealed partial class InMemoryWorkspaceSessionRegistry
{
    public bool TryGetWorkspaceReplacementCandidates(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        AssetId targetAssetId,
        out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, targetBundleId, targetAssetId, out var targetAssetState, out _, out var workspaceState))
            {
                candidates = Array.Empty<WorkspaceReplacementCandidateDto>();
                return false;
            }

            candidates = workspaceState.BundlesById.Values
                .SelectMany(bundle => bundle.Assets.Select(asset => (Bundle: bundle, Asset: asset)))
                .Where(item => !item.Asset.AssetId.Equals(targetAssetId))
                .Where(item => item.Asset.AssetType == targetAssetState.AssetType)
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
            if (!TryGetAssetStateUnsafe(workspaceId, targetBundleId, targetAssetId, out var targetAssetState, out var targetBundleState, out var workspaceState))
            {
                replacementResult = null;
                return false;
            }

            if (targetAssetState.AssetType != sourceAssetType)
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
                !TryGetAssetStateUnsafe(workspaceId, sourceBundleId, sourceAssetId, out var sourceAssetState, out var sourceBundleState, out _))
            {
                replacementResult = null;
                return false;
            }

            if (targetAssetState.AssetType != sourceAssetState.AssetType)
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

    public bool TryApplySpriteEdit(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        string draftId,
        string draftMode,
        WorkspaceSpriteEditGeometryDto geometry,
        out AssetSpriteEditAppliedResultDto? editResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState) ||
                assetState.AssetType != AssetType.Sprite)
            {
                editResult = null;
                return false;
            }

            var appliedAtUtc = DateTimeOffset.UtcNow;
            var normalizedDraftId = string.IsNullOrWhiteSpace(draftId)
                ? Guid.NewGuid().ToString("N")
                : draftId.Trim();
            var normalizedDraftMode = string.IsNullOrWhiteSpace(draftMode)
                ? "sprite-geometry"
                : draftMode.Trim();
            ApplySpriteEditState(
                assetState,
                new AppliedSpriteEditState(
                    normalizedDraftId,
                    normalizedDraftMode,
                    geometry,
                    appliedAtUtc));

            editResult = new AssetSpriteEditAppliedResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                BundleId: bundleState.BundleId,
                AssetId: assetState.AssetId,
                AssetName: assetState.Name,
                DraftId: normalizedDraftId,
                DraftMode: normalizedDraftMode,
                Geometry: geometry,
                AppliedAtUtc: appliedAtUtc);

            return true;
        }
    }
}
