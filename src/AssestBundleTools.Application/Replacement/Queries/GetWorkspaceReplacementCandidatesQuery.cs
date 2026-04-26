using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 查询当前工作区内与目标资源兼容的替换来源候选。
/// </summary>
public sealed record GetWorkspaceReplacementCandidatesQuery(
    WorkspaceId WorkspaceId,
    BundleId TargetBundleId,
    AssetId TargetAssetId);
