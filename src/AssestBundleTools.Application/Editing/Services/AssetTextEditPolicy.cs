namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 文本编辑器正文缓冲的应用层预算。
/// </summary>
public static class AssetTextEditPolicy
{
    public const int InlineEditableTextLength = 32 * 1024 * 1024;

    public const int AutoMaterializeTextLength = 2 * 1024 * 1024;

    public const int MaxRangeEditCount = 1024;

    public const int RangeEditReadChunkLength = 32768;
}
