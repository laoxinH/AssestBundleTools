using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.PluginManagement;
using AssestBundleTools.Presentation.WinUI.Views.PluginManagement.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views.PluginManagement;

/// <summary>
/// 管理导入解密与导出资源处理 CLI 插件。
/// </summary>
public sealed partial class PluginManagementPage : Page
{
    private ToolTip? _pluginDirectoryHelpToolTip;

    public PluginManagementPage()
    {
        ViewModel = new PluginManagementViewModel(
            App.CurrentApp.AssetBundleDecryptorCatalogService,
            App.CurrentApp.ResourceProcessorCatalogService,
            App.CurrentApp.AssetBundleDecryptorRegistry,
            App.CurrentApp.AppNotificationService,
            App.CurrentApp.AppLogService,
            App.CurrentApp.LocalizationService,
            App.CurrentApp.PathNavigationService);

        InitializeComponent();
        DataContext = ViewModel;
        ApplyLocalizedText();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public PluginManagementViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        PageEyebrowText.Text = UiText.Get(ResourceKeys.PluginManagementPageEyebrow);
        PageTitleText.Text = UiText.Get(ResourceKeys.PluginManagementPageTitle);
        PageSubtitleText.Text = UiText.Get(ResourceKeys.PluginManagementPageSubtitle);
        SearchTextBox.PlaceholderText = UiText.Get(ResourceKeys.PluginManagementSearchPlaceholder);
        RefreshCatalogButtonText.Text = UiText.Get(ResourceKeys.PluginManagementRefreshButton);
        PluginDirectoryTitleText.Text = UiText.Get(ResourceKeys.PluginManagementDirectoryTitle);
        PluginDirectoryHelpText.Text = UiText.Get(ResourceKeys.PluginManagementDirectoryHelpText);
        var pluginDirectoryHelpToolTipText = UiText.Get(ResourceKeys.PluginManagementDirectoryHint);
        _pluginDirectoryHelpToolTip = CreateImmediateInfoToolTip(pluginDirectoryHelpToolTipText);
        ToolTipService.SetToolTip(PluginDirectoryHelpHost, _pluginDirectoryHelpToolTip);
        AutomationProperties.SetName(PluginDirectoryHelpHost, PluginDirectoryHelpText.Text);
        AutomationProperties.SetHelpText(PluginDirectoryHelpHost, pluginDirectoryHelpToolTipText);
        PluginDirectoryAllCountLabelText.Text = UiText.Get(ResourceKeys.PluginManagementCatalogAllCountLabel);
        PluginDirectoryDecryptorCountLabelText.Text = UiText.Get(ResourceKeys.PluginManagementCatalogDecryptorCountLabel);
        PluginDirectoryProcessorCountLabelText.Text = UiText.Get(ResourceKeys.PluginManagementCatalogProcessorCountLabel);
        var addPluginMenuButtonToolTip = UiText.Get(ResourceKeys.PluginManagementAddPluginMenuButtonToolTip);
        ToolTipService.SetToolTip(AddPluginMenuButton, addPluginMenuButtonToolTip);
        AutomationProperties.SetName(AddPluginMenuButton, addPluginMenuButtonToolTip);
        AddDecryptorMenuFlyoutItem.Text = UiText.Get(ResourceKeys.PluginManagementAddDecryptorButton);
        AddResourceProcessorMenuFlyoutItem.Text = UiText.Get(ResourceKeys.PluginManagementAddResourceProcessorButton);
        DecryptorGroupTitleText.Text = UiText.Get(ResourceKeys.PluginManagementTypeImportDecryptor);
        ResourceProcessorGroupTitleText.Text = UiText.Get(ResourceKeys.PluginManagementTypeExportProcessor);
        PluginDirectoryEmptyTitleText.Text = UiText.Get(ResourceKeys.PluginManagementDirectoryEmptyTitle);
        PluginDirectoryEmptyText.Text = UiText.Get(ResourceKeys.PluginManagementDirectoryEmptyText);
        PluginDetailsEmptyTitleText.Text = UiText.Get(ResourceKeys.PluginManagementDetailsEmptyTitle);
        PluginDetailsEmptyText.Text = UiText.Get(ResourceKeys.PluginManagementDetailsEmptyText);
        EditSelectedPluginButtonText.Text = UiText.Get(ResourceKeys.PluginManagementEditButton);
        ValidateSelectedPluginButtonText.Text = UiText.Get(ResourceKeys.PluginManagementValidateButton);
        CopyDiagnosticsButtonText.Text = UiText.Get(ResourceKeys.PluginManagementCopyDiagnosticsButton);
        OpenPluginLocationButtonText.Text = UiText.Get(ResourceKeys.PluginManagementOpenLocationButton);
        DeleteSelectedPluginButtonText.Text = UiText.Get(ResourceKeys.PluginManagementDeleteButton);
        DiagnosticSectionTitleText.Text = UiText.Get(ResourceKeys.PluginManagementDiagnosticSectionTitle);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private async void OnRefreshCatalogButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCatalogAsync();
    }

    private static ToolTip CreateImmediateInfoToolTip(string text) =>
        new()
        {
            MaxWidth = 360,
            Placement = PlacementMode.Right,
            Content = new TextBlock
            {
                MaxWidth = 320,
                FontSize = 12,
                LineHeight = 18,
                Text = text,
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };

    private void OnPluginDirectoryHelpPointerEntered(object sender, PointerRoutedEventArgs e) =>
        SetImmediateToolTipOpen(_pluginDirectoryHelpToolTip, true);

    private void OnPluginDirectoryHelpPointerExited(object sender, PointerRoutedEventArgs e) =>
        SetImmediateToolTipOpen(_pluginDirectoryHelpToolTip, false);

    private static void SetImmediateToolTipOpen(ToolTip? toolTip, bool isOpen)
    {
        if (toolTip is not null)
        {
            toolTip.IsOpen = isOpen;
        }
    }

    private void OnPluginListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is PluginManagementPluginItemViewModel plugin)
        {
            ViewModel.SelectedPlugin = plugin;
        }
    }

    private async void OnAddDecryptorMenuFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        await ShowDecryptorEditorDialogAsync(entryId: null);
    }

    private async void OnAddResourceProcessorMenuFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        await ShowResourceProcessorEditorDialogAsync(entryId: null);
    }

    private async void OnEditSelectedPluginButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPlugin?.DecryptorEntry is not null)
        {
            await ShowDecryptorEditorDialogAsync(ViewModel.SelectedPlugin.Id);
            return;
        }

        if (ViewModel.SelectedPlugin?.ResourceProcessorEntry is not null)
        {
            await ShowResourceProcessorEditorDialogAsync(ViewModel.SelectedPlugin.Id);
        }
    }

    private async void OnDeleteSelectedPluginButtonClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null ||
            ViewModel.SelectedPlugin is null)
        {
            return;
        }

        var selectedPlugin = ViewModel.SelectedPlugin;
        var isDecryptor = selectedPlugin.DecryptorEntry is not null;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = UiText.Get(isDecryptor
                ? ResourceKeys.PluginManagementDecryptorDeleteTitle
                : ResourceKeys.PluginManagementProcessorDeleteTitle),
            PrimaryButtonText = UiText.Get(ResourceKeys.PluginManagementDeletePrimaryButton),
            CloseButtonText = UiText.Get(ResourceKeys.PluginManagementDeleteCloseButton),
            DefaultButton = ContentDialogButton.Close,
            Content = new TextBlock
            {
                Text = UiText.GetFormatted(
                    isDecryptor
                        ? ResourceKeys.PluginManagementDecryptorDeleteDescriptionFormat
                        : ResourceKeys.PluginManagementProcessorDeleteDescriptionFormat,
                    selectedPlugin.DisplayName,
                    selectedPlugin.Id),
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (isDecryptor)
        {
            await ViewModel.DeleteDecryptorAsync(selectedPlugin.Id);
            return;
        }

        await ViewModel.DeleteResourceProcessorAsync(selectedPlugin.Id);
    }

    private void OnValidateSelectedPluginButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ValidateSelectedPlugin();
    }

    private void OnCopyDiagnosticsButtonClick(object sender, RoutedEventArgs e)
    {
        var dataPackage = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };
        dataPackage.SetText(ViewModel.BuildSelectedDiagnosticsText());
        Clipboard.SetContent(dataPackage);

        App.CurrentApp.AppNotificationService.Show(
            UiText.Get(ResourceKeys.AppNotificationSuccessTitle),
            UiText.Get(ResourceKeys.PluginManagementDiagnosticsCopiedMessage),
            AppNotificationSeverity.Success);
    }

    private void OnOpenPluginLocationButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TryOpenSelectedPluginLocation())
        {
            return;
        }

        App.CurrentApp.AppNotificationService.Show(
            UiText.Get(ResourceKeys.AppNotificationErrorTitle),
            UiText.Get(ResourceKeys.PluginManagementOpenLocationFailedMessage),
            AppNotificationSeverity.Warning);
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

        UpsertAssetBundleDecryptorCommand? submittedCommand = null;
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
        if (result != ContentDialogResult.Primary || submittedCommand is null)
        {
            return;
        }

        await ViewModel.SaveDecryptorAsync(submittedCommand);
    }

    private async Task ShowResourceProcessorEditorDialogAsync(string? entryId)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var editor = new ResourceProcessorEditorDialogContent();
        string? dialogTargetDisplayName = entryId;
        if (!string.IsNullOrWhiteSpace(entryId) &&
            ViewModel.TryGetResourceProcessorEntry(entryId, out var entry) &&
            entry is not null)
        {
            editor.ApplyEntry(entry);
            dialogTargetDisplayName = entry.DisplayName;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = string.IsNullOrWhiteSpace(entryId)
                ? UiText.Get(ResourceKeys.ResourceProcessorEditorNewTitle)
                : UiText.GetFormatted(ResourceKeys.ResourceProcessorEditorEditTitleFormat, dialogTargetDisplayName ?? entryId),
            PrimaryButtonText = UiText.Get(ResourceKeys.ResourceProcessorEditorPrimaryButton),
            CloseButtonText = UiText.Get(ResourceKeys.ResourceProcessorEditorCloseButton),
            DefaultButton = ContentDialogButton.Primary,
            Content = editor
        };

        UpsertResourceProcessorCommand? submittedCommand = null;
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
        if (result != ContentDialogResult.Primary || submittedCommand is null)
        {
            return;
        }

        await ViewModel.SaveResourceProcessorAsync(submittedCommand);
    }
}
