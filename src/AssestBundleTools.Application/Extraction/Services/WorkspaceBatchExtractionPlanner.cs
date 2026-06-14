using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 负责把工作区中的可提取资源整理成批量提取计划。
/// </summary>
/// <remarks>
/// 这里不做真实导出，只负责基于现有工作区读模型把范围、输出目录和候选项聚合出来，
/// 供预览查询和执行流程复用，避免相同的遍历逻辑在多个 Handler 中重复出现。
/// </remarks>
internal static class WorkspaceBatchExtractionPlanner
{
    public static WorkspaceBatchExtractionPlan BuildPlan(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        AssetType? assetTypeFilter,
        string? outputDirectory,
        IReadOnlyList<BundleId>? selectedBundleIds = null,
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto>? selectedCategories = null,
        IReadOnlyList<AssetId>? selectedAssetIds = null)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);

        var normalizedCategories = NormalizeCategories(assetTypeFilter, selectedCategories);
        var includesAllUnityAssetTypes = IncludesAll(normalizedCategories) || normalizedCategories.Count == 0;
        var selectedBundleIdSet = CreateBundleIdSet(selectedBundleIds);
        var selectedAssetIdSet = CreateAssetIdSet(selectedAssetIds);

        if (!workspaceSessionRegistry.TryGetBundleTree(workspaceId, out var bundleTree))
        {
            return new WorkspaceBatchExtractionPlan(
                WorkspaceDisplayName: string.Empty,
                SuggestedOutputDirectory: string.Empty,
                AssetTypeFilter: assetTypeFilter,
                SelectedCategories: normalizedCategories,
                BundleScopes: Array.Empty<PlannedBundleExtractionScope>(),
                Assets: Array.Empty<PlannedAssetExtraction>(),
                SpineGroups: Array.Empty<PlannedSpineGroupExtraction>());
        }

        var bundleNodes = EnumerateBundleNodes(bundleTree)
            .Where(node => selectedBundleIdSet is null || selectedBundleIdSet.Contains(node.BundleId.Value))
            .ToArray();
        var plannedAssets = new List<PlannedAssetExtraction>(capacity: 64);
        var resolvedOutputDirectory = ResolveOutputDirectory(outputDirectory, workspaceId, assetTypeFilter, normalizedCategories);

        foreach (var bundleNode in bundleNodes)
        {
            if (!workspaceSessionRegistry.TryGetAssetList(
                    workspaceId,
                    bundleNode.BundleId,
                    searchKeyword: null,
                    assetTypeFilter: ResolveSingleAssetTypeFilter(assetTypeFilter, normalizedCategories),
                    assetTypeDisplayNameFilter: ResolveSingleAssetTypeDisplayNameFilter(normalizedCategories),
                    out var assetListResult))
            {
                continue;
            }

            var projectedItems = WorkspaceAssetExportNameProjection.ApplyToAssetList(
                assetListResult!.Items
                    .Where(item => MatchesSelectedUnityCategory(item, normalizedCategories, includesAllUnityAssetTypes))
                    .Where(item => selectedAssetIdSet is null || selectedAssetIdSet.Contains(item.AssetId.Value))
                    .ToArray());
            foreach (var item in projectedItems)
            {
                plannedAssets.Add(new PlannedAssetExtraction(
                    BundleId: item.BundleId,
                    AssetId: item.AssetId,
                    BundleDisplayName: assetListResult.BundleDisplayName,
                    AssetName: item.Name,
                    AssetPath: item.AssetPath,
                    AssetType: item.AssetType,
                    AssetTypeDisplayName: item.AssetTypeDisplayName,
                    PathId: item.PathId,
                    DisambiguatedName: string.IsNullOrWhiteSpace(item.DisplayName) ? item.Name : item.DisplayName,
                    SizeInBytes: item.SizeInBytes,
                    SuggestedOutputPath: BuildSuggestedOutputPath(
                        resolvedOutputDirectory,
                        assetListResult.BundleDisplayName,
                        item.Name,
                        item.AssetType,
                        item.AssetTypeDisplayName,
                        item.ExportFileName)));
            }
        }

        var workspaceDisplayName = ResolveWorkspaceDisplayName(bundleNodes);
        return new WorkspaceBatchExtractionPlan(
            WorkspaceDisplayName: workspaceDisplayName,
            SuggestedOutputDirectory: resolvedOutputDirectory,
            AssetTypeFilter: assetTypeFilter,
            SelectedCategories: normalizedCategories,
            BundleScopes: bundleNodes
                .Select(static node => new PlannedBundleExtractionScope(node.BundleId, node.DisplayName))
                .ToArray(),
            Assets: plannedAssets,
            SpineGroups: Array.Empty<PlannedSpineGroupExtraction>());
    }

    private static IEnumerable<BundleTreeNodeDto> EnumerateBundleNodes(IEnumerable<BundleTreeNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsVirtualFolder)
            {
                foreach (var child in EnumerateBundleNodes(node.Children))
                {
                    yield return child;
                }

                continue;
            }

            yield return node;
        }
    }

    private static string ResolveWorkspaceDisplayName(IEnumerable<BundleTreeNodeDto> bundleNodes)
    {
        // 当前假数据工作区不直接在树节点里携带工作区名，这里保持兜底，避免预览模型出现空引用。
        return bundleNodes.Any() ? "CurrentWorkspace" : string.Empty;
    }

    public static string ResolveOutputDirectory(
        string? outputDirectory,
        WorkspaceId workspaceId,
        AssetType? assetTypeFilter,
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto>? selectedCategories = null)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            return outputDirectory.Trim();
        }

        var normalizedCategories = NormalizeCategories(assetTypeFilter, selectedCategories);
        var scopeFolder = ResolveOutputScopeFolder(assetTypeFilter, normalizedCategories);
        return Path.Combine("Exports", workspaceId.Value, scopeFolder);
    }

    public static string BuildSuggestedOutputPath(
        string outputDirectory,
        string bundleDisplayName,
        string assetName,
        AssetType assetType,
        string? assetTypeDisplayName = null,
        string? exportFileName = null)
    {
        var fileName = string.IsNullOrWhiteSpace(exportFileName)
            ? $"{WorkspaceAssetExportNameProjection.SanitizeFileName(assetName)}{WorkspaceAssetExportNameProjection.GetAssetExtension(assetType)}"
            : exportFileName.Trim();

        return Path.Combine(
            outputDirectory,
            WorkspaceAssetExportNameProjection.SanitizePathSegment(bundleDisplayName),
            WorkspaceAssetExportNameProjection.SanitizePathSegment(
                WorkspaceAssetExportNameProjection.ResolveAssetTypeFolderName(assetTypeDisplayName, assetType)),
            fileName);
    }

    public static string BuildBundleOutputDirectory(
        string outputDirectory,
        string bundleDisplayName) =>
        Path.Combine(outputDirectory, WorkspaceAssetExportNameProjection.SanitizePathSegment(bundleDisplayName));

    public static IReadOnlyList<WorkspaceBatchExtractionCategoryPlanDto> BuildCategoryPlans(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        IReadOnlyList<BundleId>? selectedBundleIds,
        IReadOnlyList<PlannedSpineGroupExtraction>? spineGroups)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);

        if (!workspaceSessionRegistry.TryGetBundleTree(workspaceId, out var bundleTree))
        {
            return Array.Empty<WorkspaceBatchExtractionCategoryPlanDto>();
        }

        var selectedBundleIdSet = CreateBundleIdSet(selectedBundleIds);
        var bundleNodes = EnumerateBundleNodes(bundleTree)
            .Where(node => selectedBundleIdSet is null || selectedBundleIdSet.Contains(node.BundleId.Value))
            .ToArray();
        var categoryCounts = new Dictionary<WorkspaceBatchExtractionCategoryDto, CategoryCount>();

        foreach (var bundleNode in bundleNodes)
        {
            if (!workspaceSessionRegistry.TryGetAssetList(
                    workspaceId,
                    bundleNode.BundleId,
                    searchKeyword: null,
                    assetTypeFilter: null,
                    assetTypeDisplayNameFilter: null,
                    out var assetListResult))
            {
                continue;
            }

            foreach (var item in assetListResult!.Items)
            {
                AddCategoryCount(
                    categoryCounts,
                    CreateCategoryFromAssetListItem(item),
                    item);
            }
        }

        var plannedSpineGroupCount = spineGroups?.Count ?? 0;
        var allCandidateCount = categoryCounts.Values.Sum(static count => count.CandidateCount) + plannedSpineGroupCount;
        var totalSizeInBytes = categoryCounts.Values.Sum(static count => count.TotalSizeInBytes);
        if (allCandidateCount == 0)
        {
            return Array.Empty<WorkspaceBatchExtractionCategoryPlanDto>();
        }

        var plans = new List<WorkspaceBatchExtractionCategoryPlanDto>
        {
            new(
                WorkspaceBatchExtractionCategoryDto.All,
                allCandidateCount,
                totalSizeInBytes)
        };

        plans.AddRange(categoryCounts
            .OrderBy(static pair => ResolveCategorySortOrder(pair.Key))
            .ThenBy(static pair => ResolveCategorySortName(pair.Key), StringComparer.OrdinalIgnoreCase)
            .Select(static pair => new WorkspaceBatchExtractionCategoryPlanDto(
                pair.Key,
                pair.Value.CandidateCount,
                pair.Value.TotalSizeInBytes)));

        if (plannedSpineGroupCount > 0)
        {
            plans.Add(new WorkspaceBatchExtractionCategoryPlanDto(
                WorkspaceBatchExtractionCategoryDto.SpineGroup,
                plannedSpineGroupCount,
                TotalSizeInBytes: 0));
        }

        return plans;
    }

    public static bool IncludesSpineGroups(IReadOnlyList<WorkspaceBatchExtractionCategoryDto> categories) =>
        IncludesAll(categories) ||
        categories.Any(static category => category.Kind == WorkspaceBatchExtractionCategoryKind.SpineGroup);

    public static IReadOnlyList<WorkspaceBatchExtractionCategoryDto> NormalizeCategories(
        AssetType? assetTypeFilter,
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto>? selectedCategories)
    {
        if (selectedCategories is { Count: > 0 })
        {
            return selectedCategories
                .Select(static category => NormalizeCategory(category))
                .Where(static category => category is not null)
                .Select(static category => category!)
                .Distinct()
                .ToArray();
        }

        return assetTypeFilter is { } assetType
            ? new[] { WorkspaceBatchExtractionCategoryDto.FromAssetType(assetType) }
            : Array.Empty<WorkspaceBatchExtractionCategoryDto>();
    }

    public static IReadOnlyList<WorkspaceBatchExtractionCategoryDto> NormalizeForResult(
        AssetType? assetTypeFilter,
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto>? selectedCategories) =>
        NormalizeCategories(assetTypeFilter, selectedCategories);

    public static bool IsExplicitCategorySelection(
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto>? selectedCategories) =>
        selectedCategories is { Count: > 0 };

    private static AssetType? ResolveSingleAssetTypeFilter(
        AssetType? assetTypeFilter,
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto> normalizedCategories)
    {
        if (normalizedCategories.Count == 0)
        {
            return assetTypeFilter;
        }

        if (IncludesAll(normalizedCategories))
        {
            return null;
        }

        var assetTypes = normalizedCategories
            .Where(static category => category.Kind == WorkspaceBatchExtractionCategoryKind.UnityAssetType)
            .Select(static category => category.AssetType)
            .OfType<AssetType>()
            .Distinct()
            .ToArray();
        return assetTypes.Length == 1 ? assetTypes[0] : null;
    }

    private static string? ResolveSingleAssetTypeDisplayNameFilter(
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto> normalizedCategories)
    {
        if (normalizedCategories.Count == 0 || IncludesAll(normalizedCategories))
        {
            return null;
        }

        var unityCategories = normalizedCategories
            .Where(static category => category.Kind == WorkspaceBatchExtractionCategoryKind.UnityAssetType)
            .ToArray();
        return unityCategories.Length == 1
            ? unityCategories[0].AssetTypeDisplayName
            : null;
    }

    private static HashSet<string>? CreateBundleIdSet(IReadOnlyList<BundleId>? selectedBundleIds)
    {
        if (selectedBundleIds is null || selectedBundleIds.Count == 0)
        {
            return null;
        }

        return selectedBundleIds
            .Select(static id => id.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string>? CreateAssetIdSet(IReadOnlyList<AssetId>? selectedAssetIds)
    {
        if (selectedAssetIds is null || selectedAssetIds.Count == 0)
        {
            return null;
        }

        return selectedAssetIds
            .Select(static id => id.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool IncludesAll(IReadOnlyList<WorkspaceBatchExtractionCategoryDto> categories) =>
        categories.Any(static category => category.Kind == WorkspaceBatchExtractionCategoryKind.All);

    private static string ResolveOutputScopeFolder(
        AssetType? assetTypeFilter,
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto> categories)
    {
        if (categories.Count == 0)
        {
            return assetTypeFilter?.ToString() ?? "AllAssets";
        }

        if (IncludesAll(categories))
        {
            return "AllAssets";
        }

        if (categories.Count == 1)
        {
            var category = categories[0];
            return category.Kind == WorkspaceBatchExtractionCategoryKind.SpineGroup
                ? "Spine"
                : category.AssetTypeDisplayName ?? category.AssetType?.ToString() ?? "SelectedAssets";
        }

        return "SelectedAssets";
    }

    private static WorkspaceBatchExtractionCategoryDto? NormalizeCategory(
        WorkspaceBatchExtractionCategoryDto category)
    {
        return category.Kind switch
        {
            WorkspaceBatchExtractionCategoryKind.All => WorkspaceBatchExtractionCategoryDto.All,
            WorkspaceBatchExtractionCategoryKind.SpineGroup => WorkspaceBatchExtractionCategoryDto.SpineGroup,
            WorkspaceBatchExtractionCategoryKind.UnityAssetType when category.AssetType is { } assetType =>
                WorkspaceBatchExtractionCategoryDto.FromAssetType(assetType, category.AssetTypeDisplayName),
            _ => null
        };
    }

    private static bool MatchesSelectedUnityCategory(
        AssetListItemDto item,
        IReadOnlyList<WorkspaceBatchExtractionCategoryDto> categories,
        bool includesAllUnityAssetTypes)
    {
        if (includesAllUnityAssetTypes)
        {
            return true;
        }

        return categories.Any(category =>
            category.Kind == WorkspaceBatchExtractionCategoryKind.UnityAssetType &&
            category.AssetType == item.AssetType &&
            MatchesAssetTypeDisplayName(item.AssetTypeDisplayName, category.AssetTypeDisplayName));
    }

    private static WorkspaceBatchExtractionCategoryDto CreateCategoryFromAssetListItem(
        AssetListItemDto item) =>
        WorkspaceBatchExtractionCategoryDto.FromAssetType(
            item.AssetType,
            item.AssetType == AssetType.Unknown
                ? NormalizeAssetTypeDisplayName(item.AssetTypeDisplayName)
                : null);

    private static bool MatchesAssetTypeDisplayName(
        string? itemDisplayName,
        string? categoryDisplayName) =>
        string.IsNullOrWhiteSpace(categoryDisplayName) ||
        string.Equals(
            NormalizeAssetTypeDisplayName(itemDisplayName),
            categoryDisplayName,
            StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeAssetTypeDisplayName(string? assetTypeDisplayName) =>
        string.IsNullOrWhiteSpace(assetTypeDisplayName)
            ? null
            : assetTypeDisplayName.Trim();

    private static void AddCategoryCount(
        IDictionary<WorkspaceBatchExtractionCategoryDto, CategoryCount> categoryCounts,
        WorkspaceBatchExtractionCategoryDto category,
        AssetListItemDto item)
    {
        categoryCounts.TryGetValue(category, out var existing);
        categoryCounts[category] = new CategoryCount(
            existing.CandidateCount + 1,
            existing.TotalSizeInBytes + item.SizeInBytes);
    }

    private static int ResolveCategorySortOrder(WorkspaceBatchExtractionCategoryDto category)
    {
        if (category.Kind != WorkspaceBatchExtractionCategoryKind.UnityAssetType)
        {
            return category.Kind == WorkspaceBatchExtractionCategoryKind.All ? 0 : 10_000;
        }

        return category.AssetType switch
        {
            AssetType.Texture2D => 10,
            AssetType.Sprite => 20,
            AssetType.TextAsset => 30,
            AssetType.AudioClip => 40,
            AssetType.Mesh => 50,
            AssetType.Material => 60,
            AssetType.Unknown => 1_000,
            _ => 1_500 + (int)(category.AssetType ?? AssetType.Unknown)
        };
    }

    private static string ResolveCategorySortName(WorkspaceBatchExtractionCategoryDto category) =>
        category.AssetTypeDisplayName ?? category.AssetType?.ToString() ?? category.Kind.ToString();

    private readonly record struct CategoryCount(
        int CandidateCount,
        long TotalSizeInBytes);
}

/// <summary>
/// 承载批量提取骨架规划结果的内部模型。
/// </summary>
internal sealed record WorkspaceBatchExtractionPlan(
    string WorkspaceDisplayName,
    string SuggestedOutputDirectory,
    AssetType? AssetTypeFilter,
    IReadOnlyList<WorkspaceBatchExtractionCategoryDto> SelectedCategories,
    IReadOnlyList<PlannedBundleExtractionScope> BundleScopes,
    IReadOnlyList<PlannedAssetExtraction> Assets,
    IReadOnlyList<PlannedSpineGroupExtraction> SpineGroups);

internal sealed record PlannedBundleExtractionScope(
    BundleId BundleId,
    string BundleDisplayName);

/// <summary>
/// 表示单个即将参与批量提取的资源。
/// </summary>
internal sealed record PlannedAssetExtraction(
    BundleId BundleId,
    AssetId AssetId,
    string BundleDisplayName,
    string AssetName,
    string AssetPath,
    AssetType AssetType,
    string? AssetTypeDisplayName,
    long PathId,
    string DisambiguatedName,
    long SizeInBytes,
    string SuggestedOutputPath);

internal sealed record PlannedSpineGroupExtraction(
    BundleId BundleId,
    string BundleDisplayName,
    string GroupId,
    string GroupDisplayName,
    string SuggestedOutputDirectory);
