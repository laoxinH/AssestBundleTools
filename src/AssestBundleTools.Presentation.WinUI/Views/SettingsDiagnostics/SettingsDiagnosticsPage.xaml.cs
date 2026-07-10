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
    private const string WorkspaceSubPageTag = "workspace";
    private const string PreferencesSubPageTag = "preferences";
    private const string PerformanceCacheSubPageTag = "performance-cache";
    private const string LogsSubPageTag = "logs";

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
        PreferenceSettingsPanel.Content = new SettingsPreferencesView(ViewModel);
        ApplyLocalizedText();
        SettingsSubPageNavigationView.SelectedItem = SettingsWorkspaceNavItem;
        ShowSettingsSubPage(WorkspaceSubPageTag);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SettingsDiagnosticsViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        PageTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPageTitle);
        SettingsWorkspaceNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavWorkspace);
        SettingsPreferencesNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavPreferences);
        SettingsPerformanceCacheNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavPerformanceCache);
        SettingsLogsNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavLogs);
        WorkspaceStatusTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceStatusTitle);
        WorkspaceStatusHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceStatusHint);
        ActiveWorkspaceEmptyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsActiveWorkspaceEmpty);
        WorkspaceCatalogTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogTitle);
        WorkspaceCatalogHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogHint);
        WorkspaceCatalogEmptyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogEmpty);
        WorkspaceImportPerformanceTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportPerformanceTitle);
        WorkspaceImportPerformanceHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportPerformanceHint);
        WorkspaceImportWorkerCountLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountLabel);
        WorkspaceImportWorkerCountDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountRowDescription);
        PreviewWorkerCountLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountLabel);
        PreviewWorkerCountDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountRowDescription);
        BundleArtifactCacheTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheTitle);
        BundleArtifactCacheHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheHint);
        BundleArtifactCacheAutoPruneLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheAutoPruneHeader);
        BundleArtifactCacheAutoPruneDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheAutoPruneRowDescription);
        BundleArtifactCacheMaximumSizeLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeLabel);
        BundleArtifactCacheMaximumSizeDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeRowDescription);
        RefreshBundleArtifactCacheButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheRefreshButton);
        ClearBundleArtifactCacheButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheClearButton);
        LogCenterTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsLogCenterTitle);
        LogCenterHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsLogCenterHint);
    }

    private void OnSettingsSubPageNavigationSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string selectedTag)
        {
            ShowSettingsSubPage(selectedTag);
        }
    }

    private void ShowSettingsSubPage(string selectedTag)
    {
        WorkspaceSettingsPanel.Visibility = IsSubPageSelected(selectedTag, WorkspaceSubPageTag);
        PreferenceSettingsPanel.Visibility = IsSubPageSelected(selectedTag, PreferencesSubPageTag);
        PerformanceCacheSettingsPanel.Visibility = IsSubPageSelected(selectedTag, PerformanceCacheSubPageTag);
        LogCenterPanel.Visibility = IsSubPageSelected(selectedTag, LogsSubPageTag);
    }

    private static Visibility IsSubPageSelected(string selectedTag, string targetTag) =>
        string.Equals(selectedTag, targetTag, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

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
