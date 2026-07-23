using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 请求比较一个 Spine skeleton 目标与外部来源的真实载荷格式。
/// </summary>
public sealed record CheckSpineSkeletonReplacementCompatibilityQuery(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string SourceFilePath);
