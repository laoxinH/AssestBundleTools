namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示从预览缓存文本 payload 中读取的一组规范化文本行。
/// </summary>
public sealed record PreviewCacheTextLineRangeDto(
    PreviewCacheDescriptorDto Descriptor,
    IReadOnlyList<string> Lines,
    int StartLineIndex,
    int LineCount,
    int TotalLineCount,
    long ContentLength);
