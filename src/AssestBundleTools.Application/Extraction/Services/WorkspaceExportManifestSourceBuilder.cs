using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 根据批量提取计划和真实输出结果，生成导出 manifest 所需的稳定身份源项。
/// </summary>
internal static class WorkspaceExportManifestSourceBuilder
{
    public static IReadOnlyList<WorkspaceExportManifestSourceItemDto> Build(
        IReadOnlyList<BatchExtractionOutcome?> outcomes)
    {
        var items = new List<WorkspaceExportManifestSourceItemDto>();
        foreach (var outcome in outcomes)
        {
            if (outcome is null || outcome.IsCancelled || outcome.Errors is { Count: > 0 } || outcome.WorkItem is null)
            {
                continue;
            }

            if (outcome.WorkItem.Kind == BatchExtractionWorkItemKind.SpineGroup)
            {
                AddSpineManifestItems(items, outcome.WorkItem.SpineGroup!, outcome.SpineGroupResult);
                continue;
            }

            AddAssetManifestItems(items, outcome.WorkItem.Asset!, outcome.AssetResults);
        }

        return items
            .OrderBy(static item => item.OutputPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddAssetManifestItems(
        ICollection<WorkspaceExportManifestSourceItemDto> items,
        PlannedAssetExtraction plannedAsset,
        IReadOnlyList<AssetExtractionResultDto>? assetResults)
    {
        var result = assetResults?.FirstOrDefault();
        if (result is null || string.IsNullOrWhiteSpace(result.OutputPath))
        {
            return;
        }

        items.Add(new WorkspaceExportManifestSourceItemDto(
            OutputPath: result.OutputPath!,
            BundleId: plannedAsset.BundleId,
            BundleDisplayName: plannedAsset.BundleDisplayName,
            AssetId: plannedAsset.AssetId,
            PathId: plannedAsset.PathId,
            AssetType: plannedAsset.AssetType.ToString(),
            AssetTypeDisplayName: plannedAsset.AssetTypeDisplayName,
            OriginalName: plannedAsset.AssetName,
            DisambiguatedName: plannedAsset.DisambiguatedName,
            ContainerPath: plannedAsset.AssetPath,
            OutputRole: WorkspaceExportManifestOutputRole.Asset));
    }

    private static void AddSpineManifestItems(
        ICollection<WorkspaceExportManifestSourceItemDto> items,
        PlannedSpineGroupExtraction plannedGroup,
        SpineAssetGroupExtractionResultDto? groupResult)
    {
        if (groupResult is null)
        {
            return;
        }

        var partsByAssetId = groupResult.PlannedParts
            .GroupBy(static part => part.AssetId.Value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        foreach (var result in groupResult.AssetResults)
        {
            if (string.IsNullOrWhiteSpace(result.OutputPath) ||
                !partsByAssetId.TryGetValue(result.AssetId.Value, out var part))
            {
                continue;
            }

            items.Add(new WorkspaceExportManifestSourceItemDto(
                OutputPath: result.OutputPath!,
                BundleId: result.BundleId,
                BundleDisplayName: part.BundleDisplayName,
                AssetId: result.AssetId,
                PathId: 0,
                AssetType: part.AssetType.ToString(),
                AssetTypeDisplayName: part.AssetTypeDisplayName,
                OriginalName: part.Name,
                DisambiguatedName: Path.GetFileNameWithoutExtension(result.OutputPath),
                ContainerPath: part.AssetPath,
                OutputRole: ResolveSpineOutputRole(part.Role),
                SpineGroupId: plannedGroup.GroupId,
                SpineGroupDisplayName: plannedGroup.GroupDisplayName));
        }
    }

    private static WorkspaceExportManifestOutputRole ResolveSpineOutputRole(SpineAssetGroupPartRole role) =>
        role switch
        {
            SpineAssetGroupPartRole.Skeleton => WorkspaceExportManifestOutputRole.SpineSkeleton,
            SpineAssetGroupPartRole.Atlas => WorkspaceExportManifestOutputRole.SpineAtlas,
            SpineAssetGroupPartRole.TexturePage => WorkspaceExportManifestOutputRole.SpineTexturePage,
            _ => WorkspaceExportManifestOutputRole.SpineTexturePage
        };
}
