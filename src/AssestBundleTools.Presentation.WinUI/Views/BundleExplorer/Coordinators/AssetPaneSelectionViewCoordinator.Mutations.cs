namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal sealed partial class AssetPaneSelectionViewCoordinator
{
    public void RunAssetItemsMutationPreservingSelection(Action mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        if (_isSynchronizingAssetSelectionFromViewModel)
        {
            mutation();
            return;
        }

        _isSynchronizingAssetSelectionFromViewModel = true;
        try
        {
            mutation();
            if (_getViewModel() is { } viewModel)
            {
                SynchronizeAssetSelectionCore(viewModel);
            }
        }
        finally
        {
            _isSynchronizingAssetSelectionFromViewModel = false;
        }
    }
}
