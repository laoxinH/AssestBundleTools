namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 描述资源提取 payload 写出后的真实文件结果。
/// </summary>
public sealed record AssetExtractionWriteResultDto(
    string OutputPath,
    string OutputDirectory,
    long OutputSizeInBytes,
    bool OutputFileExists);
