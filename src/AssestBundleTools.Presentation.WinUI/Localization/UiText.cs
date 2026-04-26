namespace AssestBundleTools.Presentation.WinUI.Localization;

/// <summary>
/// 提供 UI 层统一的资源键读取入口。
/// 当前先由页面和控件的 code-behind 主动取值，
/// 避免继续依赖运行时表现异常的 `x:Uid` 自动注入链路。
/// </summary>
internal static class UiText
{
    public static string Get(string resourceKey)
    {
        return App.CurrentApp.LocalizationService.GetRequiredString(resourceKey);
    }

    public static string GetFormatted(string resourceKey, params object[] arguments)
    {
        return App.CurrentApp.LocalizationService.GetFormattedString(resourceKey, arguments);
    }
}
