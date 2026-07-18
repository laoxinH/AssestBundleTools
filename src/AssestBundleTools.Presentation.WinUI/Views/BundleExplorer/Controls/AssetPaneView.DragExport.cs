using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class AssetPaneView
{
    private AssetPaneDragExportSourceCoordinator? _dragExportSourceCoordinator;
    private AssetPaneDragReplacementTargetCoordinator? _dragReplacementTargetCoordinator;

    internal event EventHandler<AssetPaneDragExportRequestedEventArgs>? DragExportRequested;

    internal event EventHandler<AssetPaneDragExportCleanupRequestedEventArgs>? DragExportCleanupRequested;

    internal event EventHandler<AssetPaneElevatedDragExportRequestedEventArgs>? ElevatedDragExportAttempted;

    internal event EventHandler<AssetPaneDragReplacementRequestedEventArgs>? DragReplacementRequested;

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
        _dragReplacementTargetCoordinator = new AssetPaneDragReplacementTargetCoordinator(
            AssetListView,
            SpineGroupListView,
            () => ViewModel is { IsMultiSelectionMode: false },
            RequestDragReplacementAsync);
    }

    internal bool IsExactDragReplacementTarget(Microsoft.UI.Xaml.DragEventArgs args) =>
        _dragReplacementTargetCoordinator?.IsExactTarget(args) == true;

    internal IReadOnlyList<IDisposable> RegisterElevatedDragReplacementTargets(
        Microsoft.UI.Xaml.Window sourceWindow,
        IElevatedFileDropRouter router) =>
        _dragReplacementTargetCoordinator?.RegisterElevatedTargets(sourceWindow, router) ?? [];

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

    private Task RequestDragReplacementAsync(object item, IReadOnlyList<string> sourcePaths)
    {
        var args = new AssetPaneDragReplacementRequestedEventArgs(item, sourcePaths);
        DragReplacementRequested?.Invoke(this, args);
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
