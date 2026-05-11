namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 描述一次资源提取 payload 写出请求。
/// </summary>
public sealed record AssetExtractionWriteRequestDto(
    string WorkspaceDataRootPath,
    string SuggestedOutputPath,
    byte[]? Content,
    string? SourcePayloadFilePath = null);
