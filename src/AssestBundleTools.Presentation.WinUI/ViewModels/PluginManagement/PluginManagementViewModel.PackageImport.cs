using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.PluginManagement;

public sealed partial class PluginManagementViewModel
{
    public async Task<PluginPackageImportResultDto?> ImportPluginPackageAsync(
        string sourcePath,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        try
        {
            var result = await _pluginPackageImportService.ImportAsync(
                new ImportPluginPackageCommand(sourcePath, replaceExisting),
                cancellationToken);
            if (!TryGetValue(result, out var importResult, ResourceKeys.PluginManagementPackageImportFailedMessage) ||
                importResult is null)
            {
                return null;
            }

            if (importResult.Status == PluginPackageImportStatusDto.OverwriteConfirmationRequired)
            {
                return importResult;
            }

            _reloadAssetBundleDecryptorRegistry();
            await RefreshCatalogAsync(cancellationToken);
            _appNotificationService.Show(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.PluginManagementPackageImportSucceededMessageFormat,
                    importResult.DisplayName,
                    importResult.Version),
                AppNotificationSeverity.Success);
            return importResult;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
