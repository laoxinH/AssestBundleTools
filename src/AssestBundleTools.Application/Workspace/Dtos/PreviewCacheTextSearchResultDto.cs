using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示预览缓存文本 payload 的有界搜索结果。
/// </summary>
public sealed record PreviewCacheTextSearchResultDto(
    PreviewCacheDescriptorDto Descriptor,
    IReadOnlyList<AssetPreviewTextSearchMatchDto> Matches,
    int StartLineIndex,
    int TotalScannedLineCount,
    bool HasMore,
    long ContentLength);
