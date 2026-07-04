namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示从文件替换来源读取的一组规范化文本行。
/// </summary>
public sealed record ReplacementTextLineRangeDto(
    string SourcePath,
    string ContentType,
    IReadOnlyList<string> Lines,
    int StartLineIndex,
    int LineCount,
    int TotalLineCount,
    long ContentLength);
