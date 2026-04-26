namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示 Reader 识别出的单个资源依赖摘要。
/// </summary>
public sealed record AssetBundleReadAssetDependencyDto(
    string AssetId,
    string AssetName,
    string AssetType,
    long PathId = 0,
    string? AssetTypeDisplayName = null);
