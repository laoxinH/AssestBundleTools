using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.PluginManagement;

public sealed partial class PluginManagementPage
{
    private async void OnImportPluginFolderMenuFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        var sourcePath = await App.CurrentApp.FilePickerService.PickPluginPackageDirectoryAsync();
        await ImportSelectedPluginPackageAsync(sourcePath);
    }

    private async void OnImportPluginArchiveMenuFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        var sourcePath = await App.CurrentApp.FilePickerService.PickPluginPackageArchiveAsync();
        await ImportSelectedPluginPackageAsync(sourcePath);
    }

    private async Task ImportSelectedPluginPackageAsync(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        var importResult = await ViewModel.ImportPluginPackageAsync(sourcePath, replaceExisting: false);
        if (importResult?.Status != PluginPackageImportStatusDto.OverwriteConfirmationRequired ||
            XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = UiText.Get(ResourceKeys.PluginManagementPackageOverwriteTitle),
            PrimaryButtonText = UiText.Get(ResourceKeys.PluginManagementPackageOverwritePrimaryButton),
            CloseButtonText = UiText.Get(ResourceKeys.PluginManagementPackageOverwriteCloseButton),
            DefaultButton = ContentDialogButton.Close,
            Content = new TextBlock
            {
                Text = UiText.GetFormatted(
                    ResourceKeys.PluginManagementPackageOverwriteDescriptionFormat,
                    importResult.DisplayName,
                    importResult.PluginId,
                    importResult.Version),
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.ImportPluginPackageAsync(sourcePath, replaceExisting: true);
        }
    }
}
