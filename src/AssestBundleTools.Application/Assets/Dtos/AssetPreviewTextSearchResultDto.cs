namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示完整文本预览搜索的有界结果。
/// </summary>
public sealed record AssetPreviewTextSearchResultDto(
    AssetPreviewFullContentReferenceDto Reference,
    string SearchText,
    bool MatchCase,
    IReadOnlyList<AssetPreviewTextSearchMatchDto> Matches,
    int StartLineIndex,
    int TotalScannedLineCount,
    bool HasMore);
