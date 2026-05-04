using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示某个 Bundle 当前筛选条件下的资产列表结果。
/// </summary>
public sealed record AssetListResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    string BundleDisplayName,
    string? SearchKeyword,
    AssetType? AssetTypeFilter,
    int TotalCount,
    int FilteredCount,
    IReadOnlyList<AssetListItemDto> Items,
    string? AssetTypeDisplayNameFilter = null,
    IReadOnlyList<AssetTypeFacetDto>? TypeFacets = null);
