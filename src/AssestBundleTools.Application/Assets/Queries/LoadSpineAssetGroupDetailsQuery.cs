using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 请求加载一个 Spine 聚合资源组的 atlas 结构化详情。
/// </summary>
public sealed record LoadSpineAssetGroupDetailsQuery(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    string GroupId);
