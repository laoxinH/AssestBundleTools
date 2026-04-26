namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示 Reader 返回的单个资产摘要。
/// </summary>
public sealed record AssetBundleReadAssetDto(
    string AssetId,
    string AssetName,
    string AssetType,
    string AssetPath,
    string? ContainerPath,
    long SizeInBytes,
    long? PreferredDisplaySizeInBytes,
    bool HasPreview,
    bool CanExtract,
    bool CanReplace,
    string SerializedFileName,
    long PathId,
    string? AssetTypeDisplayName = null,
    IReadOnlyList<AssetBundleReadAssetDependencyDto>? Dependencies = null);
