namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示把标准资源包回写为来源游戏格式的请求。
/// </summary>
public sealed record AssetBundleEncryptionRequestDto(
    string SourcePath,
    string OutputPath,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    string? BundleCacheKey = null,
    string? WorkingDirectoryPath = null,
    IReadOnlyDictionary<string, string>? Parameters = null);
