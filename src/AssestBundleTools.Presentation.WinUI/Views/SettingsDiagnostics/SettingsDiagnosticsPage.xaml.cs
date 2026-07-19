using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;
using AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics;

/// <summary>
/// 设置与诊断页面。
/// </summary>
public sealed partial class SettingsDiagnosticsPage : Page
{
    private const double DefaultContentColumnMaxWidth = 980;
    private readonly SettingsPreferencesView _preferencesView;
    private readonly SettingsPerformanceCacheView _performanceCacheView;
    private readonly SettingsAboutView _aboutView;

    public SettingsDiagnosticsPage()
    {
        ViewModel = new SettingsDiagnosticsViewModel(
            App.CurrentApp.AppLogService,
            App.CurrentApp.AppNotificationService,
            App.CurrentApp.AppPreferencesService,
            App.CurrentApp.WorkspaceContextService,
            App.CurrentApp.LocalizationService,
            App.CurrentApp.WorkspaceCacheMaintenanceGateway,
            App.CurrentApp.BackgroundWorkScheduler);

        InitializeComponent();
        _preferencesView = new SettingsPreferencesView(ViewModel);
        _performanceCacheView = new SettingsPerformanceCacheView(ViewModel);
        _aboutView = new SettingsAboutView(App.CurrentApp.ApplicationUpdateCoordinator);
        PreferenceSettingsPanel.Content = _preferencesView;
        PerformanceCacheSettingsPanel.Content = _performanceCacheView;
        AboutSettingsPanel.Content = _aboutView;
        ApplyLocalizedText();
        ActivateSection(SettingsDiagnosticsSection.Preferences);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SettingsDiagnosticsViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        PageTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPageTitle);
        WorkspaceStatusTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceStatusTitle);
        WorkspaceStatusHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceStatusHint);
        ActiveWorkspaceEmptyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsActiveWorkspaceEmpty);
        WorkspaceCatalogTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogTitle);
        WorkspaceCatalogHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogHint);
        WorkspaceCatalogEmptyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogEmpty);
        LogCenterTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsLogCenterTitle);
        LogCenterHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsLogCenterHint);
    }

    internal void ActivateSection(SettingsDiagnosticsSection section)
    {
        WorkspaceSettingsPanel.Visibility = ToVisibility(section == SettingsDiagnosticsSection.Workspace);
        PreferenceSettingsPanel.Visibility = ToVisibility(section == SettingsDiagnosticsSection.Preferences);
        PerformanceCacheSettingsPanel.Visibility = ToVisibility(section == SettingsDiagnosticsSection.PerformanceCache);
        LogCenterPanel.Visibility = ToVisibility(section == SettingsDiagnosticsSection.Logs);
        AboutSettingsPanel.Visibility = ToVisibility(section == SettingsDiagnosticsSection.About);
    }

    internal void ApplyNavigationPaneLayout(bool isPaneOpen, double paneWidthDifference)
    {
        var reclaimedWidth = isPaneOpen ? 0 : Math.Max(0, paneWidthDifference);
        var contentColumnMaxWidth = DefaultContentColumnMaxWidth + reclaimedWidth;

        WorkspaceSettingsContentColumn.MaxWidth = contentColumnMaxWidth;
        LogCenterContentColumn.MaxWidth = contentColumnMaxWidth;
        _preferencesView.SetContentColumnMaxWidth(contentColumnMaxWidth);
        _performanceCacheView.SetContentColumnMaxWidth(contentColumnMaxWidth);
        _aboutView.SetContentColumnMaxWidth(contentColumnMaxWidth);
    }

    private static Visibility ToVisibility(bool isVisible) =>
        isVisible ? Visibility.Visible : Visibility.Collapsed;

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ViewModel.Dispose();
        Unloaded -= OnUnloaded;
    }

}
