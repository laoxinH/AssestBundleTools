namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示标准化资源包 artifact 清理后的占用变化。
/// </summary>
public sealed record BundleArtifactPruneResultDto(
    long SizeBeforeInBytes,
    long SizeAfterInBytes,
    int ArtifactCountBefore,
    int ArtifactCountAfter,
    IReadOnlyList<BundleArtifactCacheEntryDto> PrunedEntries);
