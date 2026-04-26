using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示撤销工作区内替换后的结果摘要。
/// </summary>
public sealed record AssetReplacementUndoResultDto(
    WorkspaceId WorkspaceId,
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string TargetAssetName);
