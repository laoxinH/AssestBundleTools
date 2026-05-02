using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics.Controls;
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
            App.CurrentApp.AssetBundleDecryptorCatalogService,
            App.CurrentApp.AssetBundleDecryptorRegistry);

        InitializeComponent();
        ApplyLocalizedText();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public SettingsDiagnosticsViewModel ViewModel { get; }

    public string EditDecryptorButtonLabelText => UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorEditButton);

    public string DeleteDecryptorButtonLabelText => UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorDeleteButton);

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
        DecryptorCatalogTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorCatalogTitle);
        DecryptorCatalogHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorCatalogHint);
        AddDecryptorButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorCatalogAddButton);
        RefreshDecryptorCatalogButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorCatalogRefreshButton);
        DecryptorCatalogEmptyText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorCatalogEmpty);
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

    private async void OnAddDecryptorButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ShowDecryptorEditorDialogAsync(entryId: null);
    }

    private async void OnRefreshDecryptorCatalogButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        await ViewModel.RefreshDecryptorCatalogAsync();
    }

    private async void OnEditDecryptorButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string entryId })
        {
            return;
        }

        await ShowDecryptorEditorDialogAsync(entryId);
    }

    private async void OnDeleteDecryptorButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string entryId } ||
            XamlRoot is null ||
            !ViewModel.TryGetDecryptorEntry(entryId, out var entry) ||
            entry is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorDeleteTitle),
            PrimaryButtonText = UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorDeletePrimaryButton),
            CloseButtonText = UiText.Get(ResourceKeys.SettingsDiagnosticsDecryptorDeleteCloseButton),
            DefaultButton = ContentDialogButton.Close,
            Content = new TextBlock
            {
                Text = UiText.GetFormatted(
                    ResourceKeys.SettingsDiagnosticsDecryptorDeleteDescriptionFormat,
                    entry.Definition.DisplayName,
                    entry.Definition.Id),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords
            }
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteDecryptorAsync(entryId);
        }
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ViewModel.Dispose();
        Unloaded -= OnUnloaded;
    }

    private async Task ShowDecryptorEditorDialogAsync(string? entryId)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var editor = new AssetBundleDecryptorEditorDialogContent();
        string? dialogTargetDisplayName = entryId;
        if (!string.IsNullOrWhiteSpace(entryId) &&
            ViewModel.TryGetDecryptorEntry(entryId, out var entry) &&
            entry is not null)
        {
            editor.ApplyEntry(entry);
            dialogTargetDisplayName = entry.Definition.DisplayName;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = string.IsNullOrWhiteSpace(entryId)
                ? UiText.Get(ResourceKeys.AssetBundleDecryptorEditorNewTitle)
                : UiText.GetFormatted(ResourceKeys.AssetBundleDecryptorEditorEditTitleFormat, dialogTargetDisplayName ?? entryId),
            PrimaryButtonText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorPrimaryButton),
            CloseButtonText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorCloseButton),
            DefaultButton = ContentDialogButton.Primary,
            Content = editor
        };

        AssestBundleTools.Application.Bundles.Commands.UpsertAssetBundleDecryptorCommand? submittedCommand = null;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!editor.TryCreateCommand(out var command) || command is null)
            {
                args.Cancel = true;
                submittedCommand = null;
                return;
            }

            submittedCommand = command;
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary ||
            submittedCommand is null)
        {
            return;
        }

        await ViewModel.SaveDecryptorAsync(submittedCommand);
    }
}
