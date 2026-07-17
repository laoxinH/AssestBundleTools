using AssestBundleTools.Presentation.WinUI.Diagnostics;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class AssetPaneView
{
    private AssetPaneDragExportSourceCoordinator? _dragExportSourceCoordinator;

    internal event EventHandler<AssetPaneDragExportRequestedEventArgs>? DragExportRequested;

    internal event EventHandler<AssetPaneDragExportCleanupRequestedEventArgs>? DragExportCleanupRequested;

    internal event EventHandler<AssetPaneElevatedDragExportRequestedEventArgs>? ElevatedDragExportAttempted;

    private void InitializeDragExport()
    {
        _dragExportSourceCoordinator = new AssetPaneDragExportSourceCoordinator(
            AssetListView,
            SpineGroupListView,
            () => ViewModel is { IsMultiSelectionMode: false },
            RequestDragExportAsync,
            CleanupDragExportAsync,
            ProcessElevationDiagnostics.Capture().IsElevated == true,
            RequestElevatedDragExportAsync);
    }

    private Task<AssetPaneDragExportPayload?> RequestDragExportAsync(object item)
    {
        var args = new AssetPaneDragExportRequestedEventArgs(item);
        DragExportRequested?.Invoke(this, args);
        return args.PreparationTask ?? Task.FromResult<AssetPaneDragExportPayload?>(null);
    }

    private Task CleanupDragExportAsync(string stagingSessionPath)
    {
        var args = new AssetPaneDragExportCleanupRequestedEventArgs(stagingSessionPath);
        DragExportCleanupRequested?.Invoke(this, args);
        return args.CleanupTask ?? Task.CompletedTask;
    }

    private Task RequestElevatedDragExportAsync(object item)
    {
        var args = new AssetPaneElevatedDragExportRequestedEventArgs(item);
        ElevatedDragExportAttempted?.Invoke(this, args);
        return args.OperationTask ?? Task.CompletedTask;
    }
}

internal sealed class AssetPaneDragExportCleanupRequestedEventArgs : EventArgs
{
    public AssetPaneDragExportCleanupRequestedEventArgs(string stagingSessionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingSessionPath);
        StagingSessionPath = stagingSessionPath;
    }

    public string StagingSessionPath { get; }

    internal Task? CleanupTask { get; private set; }

    public void SetCleanupTask(Task cleanupTask)
    {
        ArgumentNullException.ThrowIfNull(cleanupTask);
        CleanupTask = cleanupTask;
    }
}
