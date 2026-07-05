namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示应用到文本编辑草稿的一段基于 UTF-16 字符索引的范围替换。
/// </summary>
public sealed record TextAssetEditDraftRangeEditDto(
    int Start,
    int Length,
    string Text);
