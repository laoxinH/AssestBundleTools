namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 把已保存的显示语言同步到当前 Windows 应用语言覆盖。
/// </summary>
internal interface IAppDisplayLanguageOverride
{
    void Apply(AppDisplayLanguage language);
}
