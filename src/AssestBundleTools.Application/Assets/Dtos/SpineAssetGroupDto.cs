using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示一个按 skeleton / atlas / texture page 聚合出的 Spine 资源组。
/// </summary>
public sealed record SpineAssetGroupDto(
    string GroupId,
    BundleId BundleId,
    string DisplayName,
    string GroupKey,
    IReadOnlyList<SpineAssetGroupPartDto> SkeletonAssets,
    IReadOnlyList<SpineAssetGroupPartDto> AtlasAssets,
    IReadOnlyList<SpineAssetGroupPartDto> TexturePageAssets,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics);
