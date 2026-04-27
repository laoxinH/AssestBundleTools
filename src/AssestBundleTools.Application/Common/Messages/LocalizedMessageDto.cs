namespace AssestBundleTools.Application.Common.Messages;

/// <summary>
/// 表示一条可由展示层本地化的结构化消息。
/// </summary>
/// <remarks>
/// Application / Infrastructure 只负责返回稳定消息令牌和参数，
/// 由 Presentation 在最终展示时再决定使用哪种语言格式化。
/// 如果消息来自外部脚本或系统异常，也允许仅携带原始回退文本。
/// </remarks>
public sealed record LocalizedMessageDto(
    string? MessageToken = null,
    IReadOnlyList<string>? Arguments = null,
    string? FallbackText = null);
