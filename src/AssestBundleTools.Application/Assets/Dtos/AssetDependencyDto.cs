using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示资源详情中的依赖项摘要。
/// </summary>
public sealed record AssetDependencyDto(
    AssetId AssetId,
    BundleId BundleId,
    string Name,
    AssetType AssetType,
    long PathId = 0,
    string? AssetTypeDisplayName = null,
    int? DependencyDepth = null);
