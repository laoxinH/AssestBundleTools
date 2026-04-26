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

    public GetWorkspaceBatchExtractionPreviewHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<WorkspaceBatchExtractionPreviewDto>> HandleAsync(
        GetWorkspaceBatchExtractionPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceBatchExtractionPreviewDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<WorkspaceBatchExtractionPreviewDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<WorkspaceBatchExtractionPreviewDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        var plan = WorkspaceBatchExtractionPlanner.BuildPlan(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            query.AssetTypeFilter,
            query.OutputDirectory);

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
            .ToArray();

        return Task.FromResult(Result<WorkspaceBatchExtractionPreviewDto>.Success(
            new WorkspaceBatchExtractionPreviewDto(
                WorkspaceId: query.WorkspaceId,
                WorkspaceDisplayName: workspaceDisplayName,
                AssetTypeFilter: query.AssetTypeFilter,
                SuggestedOutputDirectory: plan.SuggestedOutputDirectory,
                CandidateAssetCount: plan.Assets.Count,
                CandidateBundleCount: bundlePlans.Length,
                TotalSizeInBytes: plan.Assets.Sum(static asset => asset.SizeInBytes),
                BundlePlans: bundlePlans)));
    }
}
