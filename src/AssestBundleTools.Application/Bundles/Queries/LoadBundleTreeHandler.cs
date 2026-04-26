using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Bundles.Queries;

/// <summary>
/// 处理 Bundle 树加载请求。
/// </summary>
public sealed class LoadBundleTreeHandler : IQueryHandler<LoadBundleTreeQuery, Result<IReadOnlyList<BundleTreeNodeDto>>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public LoadBundleTreeHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<IReadOnlyList<BundleTreeNodeDto>>> HandleAsync(
        LoadBundleTreeQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<IReadOnlyList<BundleTreeNodeDto>>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<IReadOnlyList<BundleTreeNodeDto>>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (!_workspaceSessionRegistry.TryGetBundleTree(query.WorkspaceId, out var bundleTree))
        {
            return Task.FromResult(Result<IReadOnlyList<BundleTreeNodeDto>>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        return Task.FromResult(Result<IReadOnlyList<BundleTreeNodeDto>>.Success(bundleTree));
    }
}
