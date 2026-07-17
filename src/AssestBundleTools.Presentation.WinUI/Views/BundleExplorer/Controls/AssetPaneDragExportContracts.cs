using Windows.Storage;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed record AssetPaneDragExportPayload(
    IReadOnlyList<IStorageItem> StorageItems,
    string StagingSessionPath);

internal sealed class AssetPaneDragExportRequestedEventArgs : EventArgs
{
    public AssetPaneDragExportRequestedEventArgs(object item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    public object Item { get; }

    internal Task<AssetPaneDragExportPayload?>? PreparationTask { get; private set; }

    public void SetPreparationTask(Task<AssetPaneDragExportPayload?> preparationTask)
    {
        ArgumentNullException.ThrowIfNull(preparationTask);

        if (PreparationTask is not null)
        {
            throw new InvalidOperationException("A drag export preparation task has already been assigned.");
        }

        PreparationTask = preparationTask;
    }
}

internal sealed class AssetPaneElevatedDragExportRequestedEventArgs : EventArgs
{
    public AssetPaneElevatedDragExportRequestedEventArgs(object item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    public object Item { get; }

    internal Task? OperationTask { get; private set; }

    public void SetOperationTask(Task operationTask)
    {
        ArgumentNullException.ThrowIfNull(operationTask);

        if (OperationTask is not null)
        {
            throw new InvalidOperationException("An elevated drag-export operation task has already been assigned.");
        }

        OperationTask = operationTask;
    }
}
