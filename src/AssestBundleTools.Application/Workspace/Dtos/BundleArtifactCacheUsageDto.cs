namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示标准化资源包 artifact 缓存的当前占用。
/// </summary>
public sealed record BundleArtifactCacheUsageDto(
    long TotalSizeInBytes,
    int ArtifactCount,
    IReadOnlyList<BundleArtifactCacheEntryDto> Entries);
