using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 为批处理页提供当前工作区的批量替换计划预览。
/// </summary>
public sealed class GetWorkspaceBatchReplacementPreviewHandler : IQueryHandler<GetWorkspaceBatchReplacementPreviewQuery, Result<WorkspaceBatchReplacementPreviewDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public GetWorkspaceBatchReplacementPreviewHandler(IWorkspaceSessionRegistry workspaceSessionRegistry)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
    }

    public Task<Result<WorkspaceBatchReplacementPreviewDto>> HandleAsync(
        GetWorkspaceBatchReplacementPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceBatchReplacementPreviewDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<WorkspaceBatchReplacementPreviewDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<WorkspaceBatchReplacementPreviewDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        var plan = WorkspaceBatchReplacementPlanner.BuildPlan(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            query.SourceFilter);

        var bundlePlans = plan.Items
            .GroupBy(
                item => new { item.BundleId, item.BundleDisplayName },
                static item => item)
            .OrderBy(group => group.Key.BundleDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var items = group.ToArray();
                return new WorkspaceBatchReplacementBundlePlanDto(
                    BundleId: group.Key.BundleId,
                    BundleDisplayName: group.Key.BundleDisplayName,
                    AssetCount: items.Length,
                    WorkspaceReplacementCount: items.Count(static item => item.HasWorkspaceReplacement),
                    FileReplacementCount: items.Count(static item => item.HasFileReplacement));
            })
            .ToArray();

        return Task.FromResult(Result<WorkspaceBatchReplacementPreviewDto>.Success(
            new WorkspaceBatchReplacementPreviewDto(
                WorkspaceId: plan.WorkspaceId,
                WorkspaceDisplayName: plan.WorkspaceDisplayName,
                SourceFilter: plan.SourceFilter,
                CandidateAssetCount: plan.Items.Count,
                CandidateBundleCount: bundlePlans.Length,
                WorkspaceReplacementCount: plan.Items.Count(static item => item.HasWorkspaceReplacement),
                FileReplacementCount: plan.Items.Count(static item => item.HasFileReplacement),
                BundlePlans: bundlePlans)));
    }
}
