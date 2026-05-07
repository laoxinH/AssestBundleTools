namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine skeleton 的轻量结构摘要。
/// </summary>
public sealed record SpineSkeletonSummaryDto(
    SpineAssetGroupPartDto SkeletonAsset,
    SpineSkeletonAssetFormat Format,
    string? SpineVersion,
    int? BoneCount,
    int? SlotCount,
    int? SkinCount,
    int? AnimationCount,
    int? EventCount,
    int? IkConstraintCount,
    int? TransformConstraintCount,
    int? PathConstraintCount);
