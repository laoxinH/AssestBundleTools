using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;
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
    private const string DiagnosticsSubPageTag = "diagnostics";
    private const string LogsSubPageTag = "logs";

    public SettingsDiagnosticsPage()
    {
        ViewModel = new SettingsDiagnosticsViewModel(
            App.CurrentApp.AppLogService,
            App.CurrentApp.AppNotificationService,
            App.CurrentApp.AppPreferencesService,
            App.CurrentApp.WorkspaceContextService,
            App.CurrentApp.LocalizationService,
            App.CurrentApp.WorkspaceImportCoordinatorService,
            App.CurrentApp.WorkspaceCacheMaintenanceGateway,
            App.CurrentApp.BackgroundWorkScheduler);

        InitializeComponent();
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
        SettingsDiagnosticsNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavDiagnostics);
        SettingsLogsNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavLogs);
        WorkspaceStatusTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceStatusTitle);
        WorkspaceStatusHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceStatusHint);
        ActiveWorkspaceImportBundleFilesButtonText.Text = UiText.Get(ResourceKeys.WorkspaceImportFilesButtonText);
        ActiveWorkspaceImportBundleDirectoryButtonText.Text = UiText.Get(ResourceKeys.WorkspaceImportDirectoryButtonText);
        ActiveWorkspaceEmptyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsActiveWorkspaceEmpty);
        WorkspaceCatalogTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogTitle);
        WorkspaceCatalogHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogHint);
        WorkspaceCatalogEmptyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceCatalogEmpty);
        StartupBehaviorTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsStartupBehaviorTitle);
        StartupBehaviorHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsStartupBehaviorHint);
        RestoreLastWorkspaceOnStartupLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRestoreLastWorkspaceToggleHeader);
        RestoreLastWorkspaceOnStartupDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRestoreLastWorkspaceRowDescription);
        RecentWorkspaceOpenBehaviorLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorLabel);
        RecentWorkspaceOpenBehaviorDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorRowDescription);
        RecentWorkspaceOpenBehaviorComboBox.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionCurrentWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionNewWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionAskEveryTime)
        };
        AssetEditorCloseBehaviorLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorLabel);
        AssetEditorCloseBehaviorDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorRowDescription);
        AssetEditorCloseBehaviorComboBox.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorOptionReturnToMainWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorOptionDirectClose)
        };
        WorkspaceImportPerformanceTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportPerformanceTitle);
        WorkspaceImportPerformanceHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportPerformanceHint);
        WorkspaceImportWorkerCountLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountLabel);
        WorkspaceImportWorkerCountDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountRowDescription);
        PreviewWorkerCountLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountLabel);
        PreviewWorkerCountDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountRowDescription);
        OperationRecordsTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordsTitle);
        OperationRecordsHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordsHint);
        OperationRecordLimitLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitLabel);
        OperationRecordLimitDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitRowDescription);
        BundleArtifactCacheTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheTitle);
        BundleArtifactCacheHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheHint);
        BundleArtifactCacheAutoPruneLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheAutoPruneHeader);
        BundleArtifactCacheAutoPruneDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheAutoPruneRowDescription);
        BundleArtifactCacheMaximumSizeLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeLabel);
        BundleArtifactCacheMaximumSizeDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeRowDescription);
        RefreshBundleArtifactCacheButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheRefreshButton);
        ClearBundleArtifactCacheButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheClearButton);
        ResponsibilityTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsResponsibilityTitle);
        ResponsibilityBodyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsResponsibilityBody);
        DiagnosticsTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDiagnosticsTitle);
        DiagnosticsBodyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDiagnosticsBody);
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
        DiagnosticsSettingsPanel.Visibility = IsSubPageSelected(selectedTag, DiagnosticsSubPageTag);
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
