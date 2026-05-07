using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 请求加载某个 Bundle 视图下的 Spine 聚合资源组。
/// </summary>
public sealed record LoadSpineAssetGroupsQuery(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    string? SearchKeyword = null);
