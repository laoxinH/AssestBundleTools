using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 请求加载某个 Bundle 的资产列表，可附带搜索和类型过滤条件。
/// </summary>
public sealed record LoadAssetListQuery(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    string? SearchKeyword = null,
    AssetType? AssetTypeFilter = null,
    string? AssetTypeDisplayNameFilter = null);
