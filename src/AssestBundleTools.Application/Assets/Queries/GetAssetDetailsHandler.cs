using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 处理单个资源详情查询请求。
/// </summary>
public sealed class GetAssetDetailsHandler : IQueryHandler<GetAssetDetailsQuery, Result<AssetDetailsDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public GetAssetDetailsHandler(IWorkspaceSessionRegistry? workspaceSessionRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? InMemoryWorkspaceSessionRegistry.Shared;
    }

    public Task<Result<AssetDetailsDto>> HandleAsync(
        GetAssetDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetDetailsDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetDetailsDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.BundleId.Value))
        {
            return Task.FromResult(Result<AssetDetailsDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.AssetId.Value))
        {
            return Task.FromResult(Result<AssetDetailsDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<AssetDetailsDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(query.WorkspaceId, query.BundleId))
        {
            return Task.FromResult(Result<AssetDetailsDto>.Failure(ApplicationErrors.BundleNotFound(query.BundleId)));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(query.WorkspaceId, query.BundleId, query.AssetId))
        {
            return Task.FromResult(Result<AssetDetailsDto>.Failure(ApplicationErrors.AssetNotFound(query.AssetId)));
        }

        _workspaceSessionRegistry.TryGetAssetDetails(
            query.WorkspaceId,
            query.BundleId,
            query.AssetId,
            out var assetDetails);

        return Task.FromResult(Result<AssetDetailsDto>.Success(assetDetails!));
    }
}
