using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 处理资产列表加载请求。
/// </summary>
public sealed class LoadAssetListHandler : IQueryHandler<LoadAssetListQuery, Result<AssetListResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public LoadAssetListHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<AssetListResultDto>> HandleAsync(
        LoadAssetListQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetListResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetListResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.BundleId.Value))
        {
            return Task.FromResult(Result<AssetListResultDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<AssetListResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(query.WorkspaceId, query.BundleId))
        {
            return Task.FromResult(Result<AssetListResultDto>.Failure(ApplicationErrors.BundleNotFound(query.BundleId)));
        }

        _workspaceSessionRegistry.TryGetAssetList(
            query.WorkspaceId,
            query.BundleId,
            query.SearchKeyword,
            query.AssetTypeFilter,
            out var assetListResult);

        return Task.FromResult(Result<AssetListResultDto>.Success(assetListResult!));
    }
}
