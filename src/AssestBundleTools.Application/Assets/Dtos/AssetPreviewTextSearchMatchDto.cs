namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示完整文本预览搜索返回的一条有限展示匹配。
/// </summary>
public sealed record AssetPreviewTextSearchMatchDto(
    int LineIndex,
    int ColumnIndex,
    string LineText,
    bool IsLineTextTruncated);
