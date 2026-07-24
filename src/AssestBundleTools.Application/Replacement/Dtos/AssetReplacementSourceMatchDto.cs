using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示普通资产多来源拖入中一个已消除歧义的目标与来源映射。
/// </summary>
public sealed record AssetReplacementSourceMatchDto(
    AssetListItemDto Target,
    string SourceFilePath,
    string SourceFileName,
    bool RequireSpineSkeletonFormatMatch);
