namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示当前工作区缓存的显式清理结果。
/// </summary>
public sealed record WorkspaceCacheClearResultDto(
    long SizeBeforeInBytes,
    long SizeAfterInBytes,
    int EntryCountBefore,
    int EntryCountAfter,
    BundleArtifactPruneResultDto BundleArtifactCacheResult,
    PreviewCachePruneResultDto PreviewCacheResult);
