using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Bindings;

/// <summary>
/// 维护应用外壳当前导航段落的标题与描述。
/// </summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private string _currentSectionTitle = string.Empty;
    private string _currentSectionDescription = string.Empty;

    public ShellViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ApplySection(ShellNavigationKeys.LaunchGuide);
    }

    public string CurrentSectionTitle
    {
        get => _currentSectionTitle;
        private set => SetProperty(ref _currentSectionTitle, value);
    }

    public string CurrentSectionDescription
    {
        get => _currentSectionDescription;
        private set => SetProperty(ref _currentSectionDescription, value);
    }

    public void ApplySection(string navigationKey)
    {
        var titleKey = navigationKey switch
        {
            ShellNavigationKeys.LaunchGuide => ResourceKeys.ShellHeaderLaunchGuide,
            ShellNavigationKeys.Workbench => ResourceKeys.ShellHeaderWorkbench,
            ShellNavigationKeys.SettingsDiagnostics => ResourceKeys.ShellHeaderSettingsDiagnostics,
            _ => ResourceKeys.ShellHeaderLaunchGuide
        };

        var descriptionKey = navigationKey switch
        {
            ShellNavigationKeys.LaunchGuide => ResourceKeys.ShellDescriptionLaunchGuide,
            ShellNavigationKeys.Workbench => ResourceKeys.ShellDescriptionWorkbench,
            ShellNavigationKeys.SettingsDiagnostics => ResourceKeys.ShellDescriptionSettingsDiagnostics,
            _ => ResourceKeys.ShellDescriptionLaunchGuide
        };

        CurrentSectionTitle = _localizationService.GetRequiredString(titleKey);
        CurrentSectionDescription = _localizationService.GetRequiredString(descriptionKey);
    }
}
