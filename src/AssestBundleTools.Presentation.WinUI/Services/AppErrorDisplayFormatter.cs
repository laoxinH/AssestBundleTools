using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 负责把应用层返回的标准错误格式化为当前界面语言下的可读文本。
/// </summary>
public static class AppErrorDisplayFormatter
{
    public static string Format(AppError error, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(localizationService);

        return error.Parameters.Count > 0
            ? localizationService.GetFormattedString(
                error.MessageToken,
                error.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : localizationService.GetRequiredString(error.MessageToken);
    }
}
