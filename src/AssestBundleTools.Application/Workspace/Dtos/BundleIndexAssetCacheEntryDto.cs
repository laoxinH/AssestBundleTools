namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示资源包索引缓存中的单个资产摘要。
/// </summary>
/// <remarks>
/// 这里只保存列表浏览和快速预览所需的轻量字段，
/// 不保存完整反序列化对象树，避免缓存体积和兼容性风险失控。
/// </remarks>
public sealed record BundleIndexAssetCacheEntryDto(
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
    IReadOnlyList<BundleIndexAssetDependencyCacheEntryDto>? Dependencies = null);
