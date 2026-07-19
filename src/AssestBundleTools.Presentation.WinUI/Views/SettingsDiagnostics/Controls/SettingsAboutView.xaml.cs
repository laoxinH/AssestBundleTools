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
    private readonly ApplicationUpdateCoordinator _updateCoordinator;
    private bool _isSubscribed;

    public SettingsAboutView(ApplicationUpdateCoordinator updateCoordinator)
    {
        _updateCoordinator = updateCoordinator ?? throw new ArgumentNullException(nameof(updateCoordinator));
        InitializeComponent();
        ApplyLocalizedText();
        ApplyUpdateState();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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

        UpdateTitleText.Text = UiText.Get(ResourceKeys.ApplicationUpdateSectionTitle);
        UpdateHintText.Text = UiText.Get(ResourceKeys.ApplicationUpdateSectionHint);
        UpdateCancelButton.Content = UiText.Get(ResourceKeys.ApplicationUpdateCancelButton);
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            _updateCoordinator.StateChanged += OnUpdateStateChanged;
            _isSubscribed = true;
        }

        ApplyUpdateState();
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!_isSubscribed)
        {
            return;
        }

        _updateCoordinator.StateChanged -= OnUpdateStateChanged;
        _isSubscribed = false;
    }

    private void OnUpdateStateChanged(object? sender, EventArgs e) => ApplyUpdateState();

    private void ApplyUpdateState()
    {
        var state = _updateCoordinator.State;
        UpdateStatusText.Text = UiText.Get(state.Kind switch
        {
            ApplicationUpdateStateKind.Unmanaged => ResourceKeys.ApplicationUpdateStateUnmanaged,
            ApplicationUpdateStateKind.Checking => ResourceKeys.ApplicationUpdateStateChecking,
            ApplicationUpdateStateKind.UpToDate => ResourceKeys.ApplicationUpdateStateUpToDate,
            ApplicationUpdateStateKind.Available => ResourceKeys.ApplicationUpdateStateAvailable,
            ApplicationUpdateStateKind.Downloading => ResourceKeys.ApplicationUpdateStateDownloading,
            ApplicationUpdateStateKind.ReadyToRestart => ResourceKeys.ApplicationUpdateStateReady,
            ApplicationUpdateStateKind.Failed => ResourceKeys.ApplicationUpdateStateFailed,
            _ => ResourceKeys.ApplicationUpdateStateIdle
        });
        UpdateVersionText.Text = string.IsNullOrWhiteSpace(state.TargetVersion)
            ? UiText.GetFormatted(ResourceKeys.ApplicationUpdateCurrentVersionFormat, state.CurrentVersion)
            : UiText.GetFormatted(
                ResourceKeys.ApplicationUpdateVersionTransitionFormat,
                state.CurrentVersion,
                state.TargetVersion);

        var isBusy = state.Kind is ApplicationUpdateStateKind.Checking or ApplicationUpdateStateKind.Downloading;
        UpdateActionButton.IsEnabled = !isBusy;
        UpdateActionButton.Content = UiText.Get(
            state.Kind is ApplicationUpdateStateKind.Available or ApplicationUpdateStateKind.ReadyToRestart
                ? ResourceKeys.ApplicationUpdateInstallButton
                : ResourceKeys.ApplicationUpdateCheckButton);

        UpdateProgressBar.Visibility = state.Kind == ApplicationUpdateStateKind.Downloading
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
        UpdateProgressBar.IsIndeterminate = state.ProgressPercentage is null;
        if (state.ProgressPercentage is { } percentage)
        {
            UpdateProgressBar.Value = percentage;
        }
        UpdateCancelButton.Visibility = state.Kind == ApplicationUpdateStateKind.Downloading
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private async void OnUpdateActionButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_updateCoordinator.State.Kind is
            ApplicationUpdateStateKind.Available or ApplicationUpdateStateKind.ReadyToRestart)
        {
            await _updateCoordinator.DownloadAndApplyAsync();
            return;
        }

        await _updateCoordinator.CheckManuallyAsync();
    }

    private void OnUpdateCancelButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        _updateCoordinator.CancelDownload();
}
