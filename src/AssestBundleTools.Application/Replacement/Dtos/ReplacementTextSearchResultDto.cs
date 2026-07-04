using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示文件替换来源文本的有界搜索结果。
/// </summary>
public sealed record ReplacementTextSearchResultDto(
    string SourcePath,
    string ContentType,
    IReadOnlyList<AssetPreviewTextSearchMatchDto> Matches,
    int StartLineIndex,
    int TotalScannedLineCount,
    bool HasMore,
    long ContentLength);
