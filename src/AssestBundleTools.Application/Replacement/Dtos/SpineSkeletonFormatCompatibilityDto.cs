using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示 Spine skeleton 目标与来源的内容格式比较结果。
/// </summary>
public sealed record SpineSkeletonFormatCompatibilityDto(
    SpineSkeletonAssetFormat TargetFormat,
    SpineSkeletonAssetFormat SourceFormat,
    bool IsCompatible);
