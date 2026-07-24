using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private async void OnInspectorExtractRequested(object sender, EventArgs e)
    {
        await ViewModel.ExtractSelectedAssetAsync();
    }

    private void OnInspectorOpenPreviewLocationRequested(object sender, EventArgs e)
    {
        ViewModel.OpenCurrentPreviewLocation();
    }

    private async void OnInspectorLoadMoreTextRequested(object sender, EventArgs e)
    {
        await ViewModel.AssetWorkflow.LoadMorePreviewTextLinesAsync();
    }

    private async void OnInspectorResolvedAssetOpenRequested(
        object sender,
        ResolvedAssetOpenRequestedEventArgs e)
    {
        await _assetEditorWindowCoordinator.HandleMainResolvedAssetOpenRequestedAsync(e.Target);
    }

    private async void OnPrimaryPaneBatchExtractRequested(object sender, EventArgs e)
    {
        await ViewModel.ExtractSelectedAssetsAsync();
    }

    private async void OnPrimaryPaneOpenSelectedItemInNewWindowRequested(object sender, EventArgs e)
    {
        await _assetEditorWindowCoordinator.HandleMainAssetSelectionChangedAsync();
        await _assetEditorWindowCoordinator.OpenDocumentInNewWindowAsync(
            ViewModel.DocumentWorkspace.ActiveDocumentTab);
    }

    private async void OnPrimaryPaneExtractSelectedItemRequested(object sender, EventArgs e)
    {
        await _assetEditorWindowCoordinator.HandleMainAssetSelectionChangedAsync();
        await ViewModel.ExtractSelectedAssetAsync();
    }

    private async void OnPrimaryPaneReplaceSelectedAssetRequested(object sender, EventArgs e)
    {
        var selectedAsset = ViewModel.AssetWorkflow.PrimaryPane.SelectedAsset?.Item;
        await _assetEditorWindowCoordinator.HandleMainAssetSelectionChangedAsync();
        if (selectedAsset?.AssetType == AssetType.Sprite)
        {
            await _assetEditorWindowCoordinator.OpenSpriteAssetEditWindowAsync(
                selectedAsset.BundleId,
                selectedAsset.AssetId);
            return;
        }

        await ModifySelectedAssetAsync();
    }

    private async void OnInspectorModifyFromFileReplacementSourceRequested(object sender, EventArgs e)
    {
        await ModifySelectedAssetAsync();
    }

    private async void OnInspectorSelectFileReplacementSourceRequested(object sender, EventArgs e)
    {
        await ViewModel.SelectFileReplacementSourceAsync();
    }

    private async void OnInspectorApplyReplacementRequested(object sender, EventArgs e)
    {
        await ViewModel.ApplyPendingReplacementAsync();
    }

    private async void OnInspectorUndoReplacementRequested(
        object sender,
        InspectorChangeRecordActionRequestedEventArgs e)
    {
        await ViewModel.UndoAppliedReplacementAsync(e.OperationId);
    }

    private async void OnInspectorRedoReplacementRequested(
        object sender,
        InspectorChangeRecordActionRequestedEventArgs e)
    {
        await ViewModel.RedoAppliedReplacementAsync(e.OperationId);
    }

    private async void OnInspectorDeleteChangeRecordRequested(
        object sender,
        InspectorChangeRecordActionRequestedEventArgs e)
    {
        await ViewModel.AssetWorkflow.DeleteChangeRecordsAsync(e.OperationIds, e.IsRestoreOriginal);
    }
}
