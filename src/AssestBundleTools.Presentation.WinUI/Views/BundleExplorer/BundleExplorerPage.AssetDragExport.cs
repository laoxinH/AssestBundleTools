using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private readonly BundleExplorerAssetDragExportCoordinator _assetDragExportCoordinator;
    private bool _isElevatedDragExportActionRunning;

    private void OnPrimaryPaneDragExportRequested(
        object? sender,
        AssetPaneDragExportRequestedEventArgs args)
    {
        if (ViewModel.WorkspaceShell.WorkspaceSummary is not { } workspaceSummary)
        {
            return;
        }

        args.SetPreparationTask(_assetDragExportCoordinator.PrepareAsync(workspaceSummary, args.Item));
    }

    private void OnPrimaryPaneDragExportCleanupRequested(
        object? sender,
        AssetPaneDragExportCleanupRequestedEventArgs args)
    {
        args.SetCleanupTask(_assetDragExportCoordinator.CleanupAsync(args.StagingSessionPath));
    }

    private async void OnPrimaryPaneElevatedDragExportAttempted(
        object? sender,
        AssetPaneElevatedDragExportRequestedEventArgs args)
    {
        if (_isElevatedDragExportActionRunning)
        {
            return;
        }

        _isElevatedDragExportActionRunning = true;
        try
        {
            if (App.CurrentApp.AppPreferencesService.ElevatedDragDropCompatibilityEnabled)
            {
                await StartElevatedCompatibleDragExportAsync(args.Item);
                return;
            }

            if (!TryGetHostWindow(out var hostWindow))
            {
                return;
            }

            ShellLaunchRequest? launchRequest = null;
            if (ViewModel.TryGetCurrentWorkspaceNavigationRequest(out var workspaceNavigationRequest))
            {
                launchRequest = new ShellLaunchRequest(
                    ShellNavigationKeys.Workbench,
                    workspaceNavigationRequest);
            }

            var restartStarted = await App.CurrentApp.ElevationRestartPromptCoordinator
                .PromptForDragExportRestartAsync(hostWindow, launchRequest);
            if (!restartStarted)
            {
                return;
            }

            App.CurrentApp.WorkspaceProcessCoordinatorService.ClearWorkspaceRegistration();
            App.CurrentApp.Exit();
        }
        finally
        {
            _isElevatedDragExportActionRunning = false;
        }
    }

    private async Task StartElevatedCompatibleDragExportAsync(object item)
    {
        var result = await App.CurrentApp.ElevatedDragExportBrokerService.StartDragAsync(
            async () =>
            {
                if (ViewModel.WorkspaceShell.WorkspaceSummary is not { } workspaceSummary)
                {
                    return null;
                }

                var payload = await _assetDragExportCoordinator.PrepareAsync(workspaceSummary, item);
                return payload?.StorageItems
                    .Select(static storageItem => storageItem.Path)
                    .ToArray();
            });
        var diagnostic = App.CurrentApp.LocalizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogElevatedDragExportBrokerFormat,
            result.Succeeded,
            result.DiagnosticDetail);
        if (result.Succeeded)
        {
            App.CurrentApp.AppLogService.AppendInfo(diagnostic);
            return;
        }

        App.CurrentApp.AppLogService.AppendError(diagnostic);
        App.CurrentApp.AppNotificationService.Show(
            App.CurrentApp.LocalizationService.GetRequiredString(
                ResourceKeys.AppNotificationErrorTitle),
            App.CurrentApp.LocalizationService.GetRequiredString(
                ResourceKeys.BundleExplorerNotificationDragExportFailed),
            AppNotificationSeverity.Error);
    }
}
