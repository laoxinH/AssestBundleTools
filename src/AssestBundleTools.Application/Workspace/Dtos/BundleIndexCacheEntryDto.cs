namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示某个资源包的解析索引缓存。
/// </summary>
/// <remarks>
/// 这是“可重建缓存”，不是工作区元数据本体。
/// 它的目标是加快资源包列表、资产列表和基础预览恢复，
/// 因此会单独落在工作区数据目录的缓存层。
/// </remarks>
public sealed record BundleIndexCacheEntryDto(
    string WorkspaceKey,
    string BundleCacheKey,
    string BundleId,
    string DisplayName,
    string LogicalPath,
    WorkspaceFileFingerprintDto SourceFingerprint,
    DateTimeOffset CachedAtUtc,
    string ParserVersion,
    string ToolVersion,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<BundleIndexAssetCacheEntryDto> Assets,
    string? DecryptorId = null,
    string? DecryptorVersion = null,
    IReadOnlyList<string>? SerializedFileNames = null,
    string? OriginalCompression = null);
