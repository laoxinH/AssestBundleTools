using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Services;
using Windows.Globalization;

namespace AssestBundleTools.Presentation.WinUI.Composition;

/// <summary>
/// 在资源初始化前同步 Windows 应用语言覆盖，并由系统持久化到下一次启动。
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
