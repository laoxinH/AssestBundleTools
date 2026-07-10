using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.Windows.Globalization;

namespace AssestBundleTools.Presentation.WinUI.Composition;

/// <summary>
/// 在资源初始化前同步 Windows App SDK 应用语言覆盖。
/// 未打包应用的跨会话持久化由应用偏好设置负责。
/// </summary>
internal sealed class WindowsAppDisplayLanguageOverride : IAppDisplayLanguageOverride
{
    public void Apply(AppDisplayLanguage language)
    {
        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = AppDisplayLanguageCodec.ToLanguageTag(language);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Failed to apply display language override. Language={language}; Exception={ex}");
        }
    }
}
