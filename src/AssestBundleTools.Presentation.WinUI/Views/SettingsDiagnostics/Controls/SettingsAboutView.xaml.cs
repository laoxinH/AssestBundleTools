using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics.Controls;

/// <summary>
/// 展示应用版本、项目入口与署名信息。
/// </summary>
public sealed partial class SettingsAboutView : UserControl
{
    private readonly AssemblyAppVersionProvider _versionProvider = new();

    public SettingsAboutView()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public void SetContentColumnMaxWidth(double maxWidth)
    {
        AboutSettingsContentColumn.MaxWidth = maxWidth;
    }

    private void ApplyLocalizedText()
    {
        ProductNameText.Text = UiText.Get(ResourceKeys.MainWindowTitle);
        ProductDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutProductDescription);
        VersionText.Text = UiText.GetFormatted(
            ResourceKeys.SettingsDiagnosticsAboutVersionFormat,
            _versionProvider.GetCurrentVersion());
        VersionSourceText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutVersionSource);

        LinksTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutLinksTitle);
        LinksHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutLinksHint);
        GitHubTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutGitHubTitle);
        GitHubAddressText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutGitHubAddress);
        WebsiteTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutWebsiteTitle);
        WebsiteAddressText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutWebsiteAddress);

        var openLinkText = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutOpenLinkButton);
        GitHubLinkButtonText.Text = openLinkText;
        WebsiteLinkButtonText.Text = openLinkText;
        AutomationProperties.SetName(GitHubLinkButton, $"{GitHubTitleText.Text}: {openLinkText}");
        AutomationProperties.SetName(WebsiteLinkButton, $"{WebsiteTitleText.Text}: {openLinkText}");

        CreditsTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutCreditsTitle);
        CreditsHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutCreditsHint);
        MaintainerLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutMaintainerLabel);
        MaintainerValueText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutMaintainerValue);
        IconArtistLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutIconArtistLabel);
        IconArtistValueText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutIconArtistValue);
        PlatformLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutPlatformLabel);
        PlatformValueText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAboutPlatformValue);
    }
}
