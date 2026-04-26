using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示工作区内替换命令已被确认并写入工作区变更队列。
/// </summary>
public sealed record AssetReplacementResultDto(
    WorkspaceId WorkspaceId,
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string TargetAssetName,
    BundleId SourceBundleId,
    AssetId SourceAssetId,
    string SourceBundleDisplayName,
    string SourceAssetName,
    AssetType SourceAssetType,
    DateTimeOffset ReplacedAtUtc);
