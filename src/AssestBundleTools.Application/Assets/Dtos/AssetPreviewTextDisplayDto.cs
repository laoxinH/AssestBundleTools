namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 描述文本预览可直接展示的有界样本。
/// </summary>
public sealed record AssetPreviewTextDisplayDto(
    int DisplayCharacterCount,
    int FullCharacterCount,
    int DisplayLineCount,
    int EstimatedFullLineCount,
    bool IsTruncated,
    int MaxDisplayCharacters,
    int MaxDisplayLineCount);
