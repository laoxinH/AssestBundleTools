namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示单工作区预览缓存的当前占用。
/// </summary>
public sealed record PreviewCacheUsageDto(
    long TotalSizeInBytes,
    int EntryCount,
    IReadOnlyList<PreviewCacheEntryDto> Entries);
