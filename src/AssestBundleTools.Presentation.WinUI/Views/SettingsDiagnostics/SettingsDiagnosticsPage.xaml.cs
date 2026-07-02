using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics;

/// <summary>
/// 设置与诊断页面。
/// </summary>
public sealed partial class SettingsDiagnosticsPage : Page
{
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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SettingsDiagnosticsViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        PageEyebrowText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPageEyebrow);
        PageTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPageTitle);
        PageSubtitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPageSubtitle);
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
        RestoreLastWorkspaceOnStartupToggleSwitch.Header = UiText.Get(ResourceKeys.SettingsDiagnosticsRestoreLastWorkspaceToggleHeader);
        RestoreLastWorkspaceOnStartupDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRestoreLastWorkspaceToggleDescription);
        RecentWorkspaceOpenBehaviorLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorLabel);
        RecentWorkspaceOpenBehaviorDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorDescription);
        RecentWorkspaceOpenBehaviorRadioButtons.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionCurrentWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionNewWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionAskEveryTime)
        };
        AssetEditorCloseBehaviorLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorLabel);
        AssetEditorCloseBehaviorDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorDescription);
        AssetEditorCloseBehaviorRadioButtons.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorOptionReturnToMainWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorOptionDirectClose)
        };
        WorkspaceImportPerformanceTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportPerformanceTitle);
        WorkspaceImportPerformanceHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportPerformanceHint);
        WorkspaceImportWorkerCountLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountLabel);
        WorkspaceImportWorkerCountDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountDescription);
        WorkspaceImportWorkerCountRadioButtons.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionAutomatic),
            UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionOne),
            UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionTwo),
            UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionThree),
            UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionFour)
        };
        PreviewWorkerCountLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountLabel);
        PreviewWorkerCountDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountDescription);
        PreviewWorkerCountRadioButtons.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionAutomatic),
            UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionOne),
            UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionTwo),
            UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionThree),
            UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionFour)
        };
        OperationRecordsTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordsTitle);
        OperationRecordsHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordsHint);
        OperationRecordLimitLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitLabel);
        OperationRecordLimitDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitDescription);
        OperationRecordLimitRadioButtons.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitOptionOneHundred),
            UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitOptionTwoHundredFifty),
            UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitOptionFiveHundred),
            UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitOptionOneThousand)
        };
        BundleArtifactCacheTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheTitle);
        BundleArtifactCacheHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheHint);
        BundleArtifactCacheAutoPruneToggleSwitch.Header = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheAutoPruneHeader);
        BundleArtifactCacheAutoPruneDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheAutoPruneDescription);
        BundleArtifactCacheMaximumSizeLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeLabel);
        BundleArtifactCacheMaximumSizeRadioButtons.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionOneGb),
            UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionTwoGb),
            UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionFourGb),
            UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionEightGb),
            UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeOptionSixteenGb)
        };
        RefreshBundleArtifactCacheButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheRefreshButton);
        ClearBundleArtifactCacheButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheClearButton);
        ResponsibilityTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsResponsibilityTitle);
        ResponsibilityBodyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsResponsibilityBody);
        DiagnosticsTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDiagnosticsTitle);
        DiagnosticsBodyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDiagnosticsBody);
        LogCenterTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsLogCenterTitle);
        LogCenterHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsLogCenterHint);
    }

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
