namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示一次资源包解密请求。
/// </summary>
/// <remarks>
/// 当前阶段这里仍只承载“进入解密器前”的文件级上下文，
/// 真正的 AB 解析与缓存写入会在后续真实导入链路中继续衔接。
/// </remarks>
public sealed record AssetBundleDecryptionRequestDto(
    string SourcePath,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    string? BundleCacheKey = null,
    string? WorkingDirectoryPath = null,
    string? PreferredOutputFileName = null,
    IReadOnlyDictionary<string, string>? Parameters = null);
