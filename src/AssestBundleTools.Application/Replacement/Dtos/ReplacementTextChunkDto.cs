namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示从文件替换来源读取的规范化文本片段。
/// </summary>
public sealed record ReplacementTextChunkDto(
    string SourcePath,
    string ContentType,
    string Text,
    int Offset,
    int Length,
    int TotalLength,
    long ContentLength);
