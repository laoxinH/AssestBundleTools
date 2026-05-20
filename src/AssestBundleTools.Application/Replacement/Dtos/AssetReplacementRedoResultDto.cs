using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示重做工作区替换后的结果摘要。
/// </summary>
public sealed record AssetReplacementRedoResultDto(
    WorkspaceId WorkspaceId,
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string TargetAssetName,
    DateTimeOffset RedoneAtUtc);
