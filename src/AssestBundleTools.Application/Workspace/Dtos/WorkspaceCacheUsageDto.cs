namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示当前工作区可重建缓存的聚合占用。
/// </summary>
public sealed record WorkspaceCacheUsageDto(
    long TotalSizeInBytes,
    int TotalEntryCount,
    BundleArtifactCacheUsageDto BundleArtifactCache,
    PreviewCacheUsageDto PreviewCache);
