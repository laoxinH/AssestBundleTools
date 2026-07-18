namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class AssetPaneDragReplacementRequestedEventArgs : EventArgs
{
    public AssetPaneDragReplacementRequestedEventArgs(
        object item,
        IReadOnlyList<string> sourcePaths)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        SourcePaths = sourcePaths ?? throw new ArgumentNullException(nameof(sourcePaths));
    }

    public object Item { get; }

    public IReadOnlyList<string> SourcePaths { get; }

    internal Task? OperationTask { get; private set; }

    public void SetOperationTask(Task operationTask)
    {
        ArgumentNullException.ThrowIfNull(operationTask);
        if (OperationTask is not null)
        {
            throw new InvalidOperationException("A drag-replacement operation task has already been assigned.");
        }

        OperationTask = operationTask;
    }
}
