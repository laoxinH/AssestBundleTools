using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.PluginManagement;

public sealed partial class PluginManagementViewModel
{
    public bool TryGetPostExportPluginEntry(string pluginId, out PostExportPluginCatalogEntryDto? entry)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            entry = null;
            return false;
        }

        return _postExportPluginEntriesById.TryGetValue(pluginId.Trim(), out entry);
    }

    public async Task<bool> SavePostExportPluginAsync(
        UpsertPostExportPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _postExportPluginCatalogService.UpsertAsync(command, cancellationToken);
        if (!TryGetValue(result, out var savedEntry, ResourceKeys.PluginManagementPostExportSaveFailedMessage) ||
            savedEntry is null)
        {
            return false;
        }

        await RefreshCatalogAsync(cancellationToken);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementPostExportSaveSucceededMessageFormat,
                savedEntry.DisplayName),
            AppNotificationSeverity.Success);
        return true;
    }

    public async Task<bool> DeletePostExportPluginAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetPostExportPluginEntry(pluginId, out var entry) || entry is null)
        {
            return false;
        }

        var result = await _postExportPluginCatalogService.DeleteAsync(
            new DeletePostExportPluginCommand(pluginId),
            cancellationToken);
        if (!TryGetValue(result, ResourceKeys.PluginManagementPostExportDeleteFailedMessage))
        {
            return false;
        }

        await RefreshCatalogAsync(cancellationToken);
        _appNotificationService.Show(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementPostExportDeleteSucceededMessageFormat,
                entry.DisplayName),
            AppNotificationSeverity.Success);
        return true;
    }
}
