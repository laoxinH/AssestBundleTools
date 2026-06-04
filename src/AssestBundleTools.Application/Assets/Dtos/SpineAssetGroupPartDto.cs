using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine 资源组中的一个可跳转组成资产。
/// </summary>
public sealed record SpineAssetGroupPartDto(
    SpineAssetGroupPartRole Role,
    BundleId BundleId,
    AssetId AssetId,
    string BundleDisplayName,
    string Name,
    string AssetPath,
    AssetType AssetType,
    string? AssetTypeDisplayName,
    DateTimeOffset? LastExtractedAtUtc = null,
    string? FileReplacementSourceFileName = null,
    string? FileReplacementSourcePath = null,
    AssetType? FileReplacementSourceAssetType = null,
    IReadOnlyList<AssetChangeRecordDto>? ChangeRecords = null);
