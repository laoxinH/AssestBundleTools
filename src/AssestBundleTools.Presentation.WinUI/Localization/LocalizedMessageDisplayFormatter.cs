using AssestBundleTools.Application.Common.Messages;

namespace AssestBundleTools.Presentation.WinUI.Localization;

/// <summary>
/// 负责把应用层返回的结构化消息格式化为当前界面语言下的可读文本。
/// </summary>
public static class LocalizedMessageDisplayFormatter
{
    public static string Format(LocalizedMessageDto? message, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        if (message is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(message.MessageToken))
        {
            return message.Arguments is { Count: > 0 }
                ? localizationService.GetFormattedString(
                    message.MessageToken,
                    message.Arguments.Cast<object>().ToArray())
                : localizationService.GetRequiredString(message.MessageToken);
        }

        return string.IsNullOrWhiteSpace(message.FallbackText)
            ? string.Empty
            : message.FallbackText.Trim();
    }
}
