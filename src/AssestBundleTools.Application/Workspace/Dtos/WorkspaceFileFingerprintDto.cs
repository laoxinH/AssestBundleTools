namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示某个源文件在生成缓存时的文件指纹。
/// </summary>
/// <remarks>
/// 当前阶段优先记录路径、大小、最后写入时间与可选内容哈希，
/// 后续 AB 缓存失效判断可以直接基于这组字段进行比对，
/// 避免把完整解析结果误当成长期稳定事实来源。
/// </remarks>
public sealed record WorkspaceFileFingerprintDto(
    string SourcePath,
    long FileSizeInBytes,
    DateTimeOffset LastWriteTimeUtc,
    string? ContentHash);
