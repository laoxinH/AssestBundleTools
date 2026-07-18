using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private readonly List<IDisposable> _elevatedAssetReplacementDropRegistrations = [];
    private readonly BundleExplorerAssetDragReplacementCoordinator _assetDragReplacementCoordinator;

    private void OnPrimaryPaneDragReplacementRequested(
        object? sender,
        AssetPaneDragReplacementRequestedEventArgs args) =>
        args.SetOperationTask(_assetDragReplacementCoordinator.HandleAsync(
            args.Item,
            args.SourcePaths));

    private void RegisterElevatedAssetReplacementDropTargets()
    {
        if (_elevatedAssetReplacementDropRegistrations.Count > 0 ||
            !TryGetHostWindow(out var hostWindow))
        {
            return;
        }

        _elevatedAssetReplacementDropRegistrations.AddRange(
            PrimaryAssetPaneView.RegisterElevatedDragReplacementTargets(
                hostWindow,
                App.CurrentApp.ElevatedFileDropRouter));
    }

    private void UnregisterElevatedAssetReplacementDropTargets()
    {
        foreach (var registration in _elevatedAssetReplacementDropRegistrations)
        {
            registration.Dispose();
        }

        _elevatedAssetReplacementDropRegistrations.Clear();
    }
}
