using System.Collections.Concurrent;
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
    private readonly IWorkspaceDataStoreFactory? _workspaceDataStoreFactory;
    private readonly SpineGroupCatalogCacheCoordinator _runtimeCacheCoordinator;
    private readonly ConcurrentDictionary<string, SpineGroupCatalogCacheCoordinator> _persistentCacheCoordinators =
        new(StringComparer.OrdinalIgnoreCase);

    public LoadSpineAssetGroupsHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory? workspaceDataStoreFactory = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory;
        _runtimeCacheCoordinator = new SpineGroupCatalogCacheCoordinator(persistentStore: null);
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

        SpineAssetGroupListResultDto result;
        try
        {
            result = CreateCatalogCacheCoordinator(query).GetOrBuildResult(
                query.WorkspaceId,
                query.BundleId,
                targetBundleDisplayName,
                query.SearchKeyword,
                assetLists,
                token => SpineAssetGroupDetectionDetailsCollector.Collect(
                    _workspaceSessionRegistry,
                    query.WorkspaceId,
                    assetLists,
                    token),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Result<SpineAssetGroupListResultDto>.Cancelled());
        }

        result = SpineAssetGroupPartDetailsHydrator.HydrateResult(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            result,
            cancellationToken);

        return Task.FromResult(Result<SpineAssetGroupListResultDto>.Success(result));
    }

    private SpineGroupCatalogCacheCoordinator CreateCatalogCacheCoordinator(LoadSpineAssetGroupsQuery query)
    {
        if (_workspaceDataStoreFactory is null ||
            string.IsNullOrWhiteSpace(query.WorkspaceDataRootPath))
        {
            return _runtimeCacheCoordinator;
        }

        var workspaceDataRootPath = query.WorkspaceDataRootPath.Trim();
        return _persistentCacheCoordinators.GetOrAdd(workspaceDataRootPath, rootPath =>
            new SpineGroupCatalogCacheCoordinator(
                _workspaceDataStoreFactory.CreateSpineGroupCatalogCacheStore(rootPath)));
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
