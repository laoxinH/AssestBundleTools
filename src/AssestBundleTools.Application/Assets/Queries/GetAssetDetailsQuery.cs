using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 请求获取单个资源的详情信息。
/// </summary>
public sealed record GetAssetDetailsQuery(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId);
