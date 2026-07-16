using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private readonly BundleExplorerAssetDragExportCoordinator _assetDragExportCoordinator;

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
}
