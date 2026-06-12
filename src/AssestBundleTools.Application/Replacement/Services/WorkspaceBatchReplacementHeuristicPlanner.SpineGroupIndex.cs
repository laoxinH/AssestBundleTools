using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Services;

internal static partial class WorkspaceBatchReplacementHeuristicPlanner
{
    private static async Task<SpineGroupHeuristicIndex> BuildSpineGroupIndexAsync(
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? loadSpineAssetGroupsHandler,
        WorkspaceId workspaceId,
        WorkspaceHeuristicIndex workspaceIndex,
        CancellationToken cancellationToken)
    {
        if (loadSpineAssetGroupsHandler is null ||
            workspaceIndex.Bundles.Count == 0)
        {
            return SpineGroupHeuristicIndex.Empty;
        }

        var entries = new List<SpineGroupTargetEntry>();
        foreach (var bundle in workspaceIndex.Bundles)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var result = await loadSpineAssetGroupsHandler.HandleAsync(
                new LoadSpineAssetGroupsQuery(workspaceId, bundle.BundleId),
                cancellationToken);
            if (!result.IsSuccess ||
                result.Value is null)
            {
                continue;
            }

            foreach (var group in result.Value.Items)
            {
                AddGroupParts(entries, workspaceIndex, group, group.SkeletonAssets, "Skeleton");
                AddGroupParts(entries, workspaceIndex, group, group.AtlasAssets, "Atlas");
                AddGroupParts(entries, workspaceIndex, group, group.TexturePageAssets, "TexturePage");
            }
        }

        return entries.Count == 0
            ? SpineGroupHeuristicIndex.Empty
            : new SpineGroupHeuristicIndex(entries);
    }

    private static void AddGroupParts(
        List<SpineGroupTargetEntry> entries,
        WorkspaceHeuristicIndex workspaceIndex,
        SpineAssetGroupDto group,
        IReadOnlyList<SpineAssetGroupPartDto> parts,
        string outputRole)
    {
        var groupKeys = CreateSpineGroupMatchKeys(group);
        foreach (var part in parts)
        {
            var target = workspaceIndex.Assets.FirstOrDefault(asset =>
                asset.Bundle.BundleId.Equals(part.BundleId) &&
                asset.AssetId.Equals(part.AssetId));
            if (target is null)
            {
                continue;
            }

            entries.Add(new SpineGroupTargetEntry(
                target,
                groupKeys,
                outputRole));
        }
    }

    private static IReadOnlySet<string> CreateSpineGroupMatchKeys(SpineAssetGroupDto group)
    {
        var keys = new HashSet<string>(OrdinalIgnoreCase);
        AddSpineGroupKey(keys, group.DisplayName);
        AddSpineGroupKey(keys, group.GroupKey);
        AddSpineGroupKey(keys, Path.GetFileName(group.GroupKey));
        return keys;
    }

    private static void AddSpineGroupKey(HashSet<string> keys, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = NormalizeName(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            keys.Add(normalized);
        }
    }

    private static bool TryResolveSpineGroupTarget(
        SourceFileAnalysis source,
        IReadOnlyList<BundleIndexEntry> bundleMatches,
        SpineGroupHeuristicIndex spineGroupIndex,
        List<string> diagnostics,
        out AssetIndexEntry target)
    {
        target = default!;
        if (!source.IsSpineContext ||
            string.IsNullOrWhiteSpace(source.SpineGroupName) ||
            spineGroupIndex.Entries.Count == 0)
        {
            return false;
        }

        var sourceGroupKey = NormalizeName(source.SpineGroupName);
        var sourceGroupKeyWithoutOrdinal = StripOrdinalSuffix(sourceGroupKey);
        var restrictToBundle = bundleMatches.Count == 1;
        var candidates = spineGroupIndex.Entries
            .Where(entry => !restrictToBundle || entry.Target.Bundle.BundleId.Equals(bundleMatches[0].BundleId))
            .Where(entry => string.Equals(entry.OutputRole, source.OutputRole, StringComparison.OrdinalIgnoreCase))
            .Where(entry => entry.GroupMatchKeys.Contains(sourceGroupKey))
            .ToArray();

        if (candidates.Length == 0)
        {
            candidates = spineGroupIndex.Entries
                .Where(entry => !restrictToBundle || entry.Target.Bundle.BundleId.Equals(bundleMatches[0].BundleId))
                .Where(entry => string.Equals(entry.OutputRole, source.OutputRole, StringComparison.OrdinalIgnoreCase))
                .Where(entry => entry.GroupMatchKeys.Select(StripOrdinalSuffix).Contains(sourceGroupKeyWithoutOrdinal, OrdinalIgnoreCase))
                .ToArray();
        }

        var exactNameKey = NormalizeName(source.NameWithoutExtension);
        var exactNameMatches = candidates
            .Where(entry => NormalizeName(entry.Target.Name) == exactNameKey ||
                            NormalizeName(Path.GetFileNameWithoutExtension(entry.Target.AssetPath)) == exactNameKey)
            .ToArray();
        if (exactNameMatches.Length > 0)
        {
            candidates = exactNameMatches;
        }

        if (candidates.Length != 1)
        {
            return false;
        }

        var candidate = candidates[0];
        diagnostics.Add("SpineGroupCatalogMatched");
        target = candidate.Target;
        return true;
    }
}
