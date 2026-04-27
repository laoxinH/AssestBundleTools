using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Queries;

/// <summary>
/// 为批处理页与保存摘要页提供工作区变更读模型。
/// </summary>
public sealed class GetWorkspaceChangeSummaryHandler : IQueryHandler<GetWorkspaceChangeSummaryQuery, Result<WorkspaceChangeSummaryDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public GetWorkspaceChangeSummaryHandler(IWorkspaceSessionRegistry workspaceSessionRegistry)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
    }

    public Task<Result<WorkspaceChangeSummaryDto>> HandleAsync(
        GetWorkspaceChangeSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceChangeSummaryDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<WorkspaceChangeSummaryDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<WorkspaceChangeSummaryDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        _workspaceSessionRegistry.TryGetWorkspaceChangeSummary(query.WorkspaceId, out var summary);
        return Task.FromResult(Result<WorkspaceChangeSummaryDto>.Success(summary!));
    }
}
