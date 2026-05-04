using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示某个资源包内可展示资产的类型统计，用于由应用层驱动类型筛选项。
/// </summary>
public sealed record AssetTypeFacetDto(
    AssetType AssetType,
    string AssetTypeDisplayName,
    int Count);
