namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示预览缓存容量治理请求。
/// </summary>
public sealed record PreviewCachePruneRequestDto(
    string WorkspaceKey,
    long? MaximumSizeInBytes,
    int? MaximumEntryCount,
    IReadOnlySet<string>? ProtectedPreviewCacheKeys = null);
