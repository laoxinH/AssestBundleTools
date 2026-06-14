using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 负责把 Spine 聚合资源组投影成工作区批量提取计划项。
/// </summary>
internal static class WorkspaceBatchExtractionSpinePlanner
{
    private const string SpineFolderName = "Spine";

    public static async Task<IReadOnlyList<PlannedSpineGroupExtraction>> BuildSpineGroupsAsync(
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? loadSpineAssetGroupsHandler,
        WorkspaceId workspaceId,
        string outputDirectory,
        string? workspaceDataRootPath,
        IEnumerable<(BundleId BundleId, string BundleDisplayName)> bundleScopes,
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto> categories,
        IReadOnlyList<string>? selectedSpineGroupIds,
        CancellationToken cancellationToken)
    {
        if (loadSpineAssetGroupsHandler is null ||
            !WorkspaceBatchExtractionPlanner.IncludesSpineGroups(categories))
        {
            return Array.Empty<PlannedSpineGroupExtraction>();
        }

        var selectedSpineGroupIdSet = CreateSpineGroupIdSet(selectedSpineGroupIds);
        var plannedGroups = new List<PlannedSpineGroupExtraction>();
        foreach (var bundleScope in bundleScopes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await loadSpineAssetGroupsHandler.HandleAsync(
                new LoadSpineAssetGroupsQuery(
                    workspaceId,
                    bundleScope.BundleId,
                    WorkspaceDataRootPath: workspaceDataRootPath),
                cancellationToken);
            if (result.IsCancelled)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (!result.IsSuccess || result.Value is null)
            {
                continue;
            }

            foreach (var group in result.Value.Items)
            {
                if (selectedSpineGroupIdSet is not null &&
                    !selectedSpineGroupIdSet.Contains(group.GroupId))
                {
                    continue;
                }

                plannedGroups.Add(new PlannedSpineGroupExtraction(
                    group.BundleId,
                    result.Value.BundleDisplayName,
                    group.GroupId,
                    group.DisplayName,
                    BuildSpineOutputDirectory(
                        outputDirectory,
                        result.Value.BundleDisplayName)));
            }
        }

        return plannedGroups;
    }

    private static HashSet<string>? CreateSpineGroupIdSet(IReadOnlyList<string>? selectedSpineGroupIds)
    {
        if (selectedSpineGroupIds is null || selectedSpineGroupIds.Count == 0)
        {
            return null;
        }

        var set = selectedSpineGroupIds
            .Where(static groupId => !string.IsNullOrWhiteSpace(groupId))
            .Select(static groupId => groupId.Trim())
            .ToHashSet(StringComparer.Ordinal);

        return set.Count == 0 ? null : set;
    }

    private static string BuildSpineOutputDirectory(
        string outputDirectory,
        string bundleDisplayName) =>
        Path.Combine(
            WorkspaceBatchExtractionPlanner.BuildBundleOutputDirectory(
                outputDirectory,
                bundleDisplayName),
            SpineFolderName);
}
