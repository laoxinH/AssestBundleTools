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
                    .Where(static asset => asset.IncludeInAssetList)
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

            targetAssetState.IsModified = true;
            targetAssetState.AppliedWorkspaceReplacement = null;
            targetAssetState.AppliedFileReplacement = new AppliedFileReplacementState(
                normalizedSourcePath,
                sourceFileName,
                sourceAssetType,
                appliedAtUtc);

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
            targetAssetState.IsModified = true;
            targetAssetState.AppliedFileReplacement = null;
            targetAssetState.AppliedWorkspaceReplacement = new AppliedWorkspaceReplacementState(
                sourceBundleState.BundleId,
                sourceAssetState.AssetId,
                sourceBundleState.DisplayName,
                sourceAssetState.Name,
                sourceAssetState.AssetType,
                replacedAtUtc);

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

            assetState.AppliedWorkspaceReplacement = null;
            assetState.AppliedFileReplacement = null;
            assetState.IsModified = false;

            undoResult = new AssetReplacementUndoResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                TargetBundleId: bundleState.BundleId,
                TargetAssetId: assetState.AssetId,
                TargetAssetName: assetState.Name);

            return true;
        }
    }
}
