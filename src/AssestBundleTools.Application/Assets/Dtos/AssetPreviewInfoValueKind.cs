namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示预览补充信息的值类型。
/// </summary>
/// <remarks>
/// 这里约束的是“值该如何被展示层解释”，
/// 而不是直接绑定某种具体语言的文案格式。
/// </remarks>
public enum AssetPreviewInfoValueKind
{
    Text = 0,
    Integer = 1,
    ByteSize = 2,
    Decimal = 3,
    DurationSeconds = 4,
    FrequencyHertz = 5,
    BitsPerSample = 6,
    Boolean = 7
}
