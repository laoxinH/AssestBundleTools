using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Extraction.Queries;

/// <summary>
/// 为批处理页提供当前工作区的批量提取预览摘要。
/// </summary>
public sealed class GetWorkspaceBatchExtractionPreviewHandler : IQueryHandler<GetWorkspaceBatchExtractionPreviewQuery, Result<WorkspaceBatchExtractionPreviewDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? _loadSpineAssetGroupsHandler;

    public GetWorkspaceBatchExtractionPreviewHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? loadSpineAssetGroupsHandler = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _loadSpineAssetGroupsHandler = loadSpineAssetGroupsHandler;
    }

    public async Task<Result<WorkspaceBatchExtractionPreviewDto>> HandleAsync(
        GetWorkspaceBatchExtractionPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceBatchExtractionPreviewDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Result<WorkspaceBatchExtractionPreviewDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Result<WorkspaceBatchExtractionPreviewDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId));
        }

        var plan = WorkspaceBatchExtractionPlanner.BuildPlan(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            query.AssetTypeFilter,
            query.OutputDirectory,
            query.SelectedBundleIds,
            query.SelectedCategories);
        IReadOnlyList<PlannedSpineGroupExtraction> allSpineGroups;
        try
        {
            allSpineGroups = await WorkspaceBatchExtractionSpinePlanner.BuildSpineGroupsAsync(
                _loadSpineAssetGroupsHandler,
                query.WorkspaceId,
                plan.SuggestedOutputDirectory,
                plan.BundleScopes.Select(static scope => (scope.BundleId, scope.BundleDisplayName)),
                new[] { WorkspaceBatchExtractionCategoryDto.SpineGroup },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceBatchExtractionPreviewDto>.Cancelled();
        }

        var spineGroups = WorkspaceBatchExtractionPlanner.IncludesSpineGroups(plan.SelectedCategories)
            ? allSpineGroups
            : Array.Empty<PlannedSpineGroupExtraction>();
        plan = plan with { SpineGroups = spineGroups };

        var workspaceDisplayName = plan.WorkspaceDisplayName;
        if (_workspaceSessionRegistry.TryGetWorkspaceChangeSummary(query.WorkspaceId, out var changeSummary) &&
            changeSummary is not null)
        {
            workspaceDisplayName = changeSummary.WorkspaceDisplayName;
        }

        var bundlePlans = plan.Assets
            .GroupBy(
                asset => new { asset.BundleId, asset.BundleDisplayName },
                static asset => asset)
            .OrderBy(group => group.Key.BundleDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new WorkspaceBatchExtractionBundlePlanDto(
                BundleId: group.Key.BundleId,
                BundleDisplayName: group.Key.BundleDisplayName,
                AssetCount: group.Count(),
                TotalSizeInBytes: group.Sum(static asset => asset.SizeInBytes)))
            .ToDictionary(static plan => plan.BundleId.Value, static plan => plan, StringComparer.Ordinal);

        foreach (var group in plan.SpineGroups.GroupBy(static group => new { group.BundleId, group.BundleDisplayName }))
        {
            if (bundlePlans.TryGetValue(group.Key.BundleId.Value, out var existingPlan))
            {
                bundlePlans[group.Key.BundleId.Value] = existingPlan with
                {
                    SpineGroupCount = group.Count()
                };
                continue;
            }

            bundlePlans[group.Key.BundleId.Value] = new WorkspaceBatchExtractionBundlePlanDto(
                group.Key.BundleId,
                group.Key.BundleDisplayName,
                AssetCount: 0,
                TotalSizeInBytes: 0,
                SpineGroupCount: group.Count());
        }

        var orderedBundlePlans = bundlePlans.Values
            .OrderBy(static plan => plan.BundleDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var categoryPlans = WorkspaceBatchExtractionPlanner.BuildCategoryPlans(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            query.SelectedBundleIds,
            allSpineGroups);

        return Result<WorkspaceBatchExtractionPreviewDto>.Success(
            new WorkspaceBatchExtractionPreviewDto(
                WorkspaceId: query.WorkspaceId,
                WorkspaceDisplayName: workspaceDisplayName,
                AssetTypeFilter: query.AssetTypeFilter,
                SuggestedOutputDirectory: plan.SuggestedOutputDirectory,
                CandidateAssetCount: plan.Assets.Count,
                CandidateBundleCount: orderedBundlePlans.Length,
                TotalSizeInBytes: plan.Assets.Sum(static asset => asset.SizeInBytes),
                BundlePlans: orderedBundlePlans,
                SelectedCategories: plan.SelectedCategories,
                CandidateSpineGroupCount: plan.SpineGroups.Count,
                CategoryPlans: categoryPlans));
    }
}
