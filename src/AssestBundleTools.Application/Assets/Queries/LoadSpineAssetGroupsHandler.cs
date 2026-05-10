using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 处理 Spine 聚合资源组列表加载请求。
/// </summary>
public sealed class LoadSpineAssetGroupsHandler : IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;

    public LoadSpineAssetGroupsHandler(IWorkspaceSessionRegistry workspaceSessionRegistry)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
    }

    public Task<Result<SpineAssetGroupListResultDto>> HandleAsync(
        LoadSpineAssetGroupsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(query.BundleId.Value))
        {
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(query.WorkspaceId, query.BundleId))
        {
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Failure(ApplicationErrors.BundleNotFound(query.BundleId)));
        }

        if (!_workspaceSessionRegistry.TryGetBundleTree(query.WorkspaceId, out var bundleTree))
        {
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId)));
        }

        var bundleNodes = FlattenBundleNodes(bundleTree)
            .Where(static node => node.CanOpenAssets)
            .ToArray();
        var targetBundleNode = bundleNodes.FirstOrDefault(node => node.BundleId.Equals(query.BundleId));
        var targetBundleDisplayName = targetBundleNode?.DisplayName ?? query.BundleId.Value;
        var assetLists = new List<AssetListResultDto>(bundleNodes.Length);
        foreach (var bundleNode in bundleNodes)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(Result<SpineAssetGroupListResultDto>.Cancelled());
            }

            if (_workspaceSessionRegistry.TryGetAssetList(
                    query.WorkspaceId,
                    bundleNode.BundleId,
                    searchKeyword: null,
                    assetTypeFilter: null,
                    assetTypeDisplayNameFilter: null,
                    out var assetList) &&
                assetList is not null)
            {
                assetLists.Add(assetList);
            }
        }

        var assetDetailsByAssetKey = SpineAssetGroupDetectionDetailsCollector.Collect(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            assetLists,
            cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Cancelled());
        }

        var result = SpineAssetGroupDetector.CreateResult(
            query.WorkspaceId,
            query.BundleId,
            targetBundleDisplayName,
            query.SearchKeyword,
            assetLists,
            assetDetailsByAssetKey);

        return Task.FromResult(Result<SpineAssetGroupListResultDto>.Success(result));
    }

    private static IEnumerable<BundleTreeNodeDto> FlattenBundleNodes(IEnumerable<BundleTreeNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in FlattenBundleNodes(node.Children))
            {
                yield return child;
            }
        }
    }
}
