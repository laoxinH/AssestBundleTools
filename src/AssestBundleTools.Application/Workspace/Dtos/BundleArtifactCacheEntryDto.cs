namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示一个标准化资源包 artifact 的缓存占用摘要。
/// </summary>
public sealed record BundleArtifactCacheEntryDto(
    string WorkspaceKey,
    string BundleCacheKey,
    long SizeInBytes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastAccessedAtUtc,
    bool IsMaterialized);
