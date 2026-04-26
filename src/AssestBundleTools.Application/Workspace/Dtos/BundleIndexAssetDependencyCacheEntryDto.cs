namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示资源索引缓存中的单个依赖项摘要。
/// </summary>
public sealed record BundleIndexAssetDependencyCacheEntryDto(
    string AssetId,
    string AssetName,
    string AssetType,
    long PathId = 0,
    string? AssetTypeDisplayName = null);
