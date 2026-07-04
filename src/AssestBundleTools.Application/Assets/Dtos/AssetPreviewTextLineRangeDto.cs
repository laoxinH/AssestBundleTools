namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示从完整文本预览载荷中读取的一组规范化文本行。
/// </summary>
public sealed record AssetPreviewTextLineRangeDto(
    AssetPreviewFullContentReferenceDto Reference,
    IReadOnlyList<string> Lines,
    int StartLineIndex,
    int LineCount,
    int TotalLineCount,
    bool HasMore);
