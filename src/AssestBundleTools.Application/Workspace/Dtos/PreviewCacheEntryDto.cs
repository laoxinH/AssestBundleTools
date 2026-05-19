namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示单个预览缓存项的占用摘要。
/// </summary>
public sealed record PreviewCacheEntryDto(
    string WorkspaceKey,
    string PreviewCacheKey,
    string BundleCacheKey,
    string? AssetId,
    string PreviewKind,
    long SizeInBytes,
    DateTimeOffset GeneratedAtUtc);
