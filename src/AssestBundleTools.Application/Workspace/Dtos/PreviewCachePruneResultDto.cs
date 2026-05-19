namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示预览缓存清理结果。
/// </summary>
public sealed record PreviewCachePruneResultDto(
    long SizeBeforeInBytes,
    long SizeAfterInBytes,
    int EntryCountBefore,
    int EntryCountAfter,
    IReadOnlyList<PreviewCacheEntryDto> PrunedEntries);
