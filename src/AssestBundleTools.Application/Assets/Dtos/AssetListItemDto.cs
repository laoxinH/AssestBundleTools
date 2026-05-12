using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示资产列表中的单个资源摘要。
/// </summary>
public sealed record AssetListItemDto(
    AssetId AssetId,
    BundleId BundleId,
    string Name,
    string AssetPath,
    AssetType AssetType,
    long SizeInBytes,
    long? PreferredDisplaySizeInBytes,
    bool HasPreview,
    bool IsModified,
    string? AssetTypeDisplayName = null,
    DateTimeOffset? LastExtractedAtUtc = null);
