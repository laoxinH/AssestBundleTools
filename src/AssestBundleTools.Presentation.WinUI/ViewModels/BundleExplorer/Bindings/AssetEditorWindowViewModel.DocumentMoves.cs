namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetEditorWindowViewModel
{
    public async Task MoveAssetDocumentToMainWindowAsync(AssetDocumentTabViewModel? assetTab)
    {
        var detachedAssetTab = await DetachAssetDocumentTabForExternalHostAsync(assetTab);
        if (detachedAssetTab is null)
        {
            return;
        }

        if (await _moveAssetDocumentToMainWindowAsync(detachedAssetTab))
        {
            await CompleteExternalAssetDocumentMoveAsync();
            return;
        }

        await RestoreDetachedAssetDocumentTabAsync(detachedAssetTab);
    }

    public Task MoveActiveAssetDocumentToMainWindowAsync() =>
        MoveActiveDocumentToMainWindowAsync();

    public async Task MoveActiveDocumentToMainWindowAsync()
    {
        await MoveDocumentToMainWindowAsync(DocumentGroup.ActiveDocumentTab);
    }

    public async Task MoveDocumentToMainWindowAsync(AssetDocumentTabItemViewModel? documentTab)
    {
        var detachedDocumentTab = await DetachDocumentTabForExternalHostAsync(documentTab);
        if (detachedDocumentTab is null)
        {
            return;
        }

        var transfer = new AssetDocumentGroupTransfer([detachedDocumentTab], detachedDocumentTab);
        if (await _moveAssetDocumentGroupToMainWindowAsync(transfer))
        {
            await CompleteExternalAssetDocumentMoveAsync();
            return;
        }

        await RestoreDetachedDocumentTabAsync(detachedDocumentTab);
    }

    public async Task MoveAssetDocumentGroupToMainWindowAsync()
    {
        var transfer = await DetachAssetDocumentGroupForExternalHostAsync();
        if (transfer is null)
        {
            return;
        }

        if (await _moveAssetDocumentGroupToMainWindowAsync(transfer))
        {
            await CompleteExternalAssetDocumentMoveAsync();
            return;
        }

        await RestoreDetachedAssetDocumentGroupAsync(transfer);
    }

    public async Task<AssetDocumentTabViewModel?> DetachAssetDocumentTabForExternalHostAsync(
        AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || IsBusy || !DocumentGroup.DetachAssetTab(assetTab))
        {
            return null;
        }

        if (DocumentGroup.HasDocumentTabs)
        {
            await ActivateDocumentTabAsync(DocumentGroup.ActiveDocumentTab);
        }
        else
        {
            SyncActiveDocumentState();
        }

        return assetTab;
    }

    public async Task<AssetDocumentTabItemViewModel?> DetachDocumentTabForExternalHostAsync(
        AssetDocumentTabItemViewModel? documentTab)
    {
        if (documentTab is null || IsBusy || !DocumentGroup.DetachDocumentTab(documentTab))
        {
            return null;
        }

        if (DocumentGroup.HasDocumentTabs)
        {
            await ActivateDocumentTabAsync(DocumentGroup.ActiveDocumentTab);
        }
        else
        {
            SyncActiveDocumentState();
        }

        return documentTab;
    }

    public Task<AssetDocumentGroupTransfer?> DetachAssetDocumentGroupForExternalHostAsync()
    {
        if (IsBusy)
        {
            return Task.FromResult<AssetDocumentGroupTransfer?>(null);
        }

        var transfer = DocumentGroup.DetachDocumentTabs();
        SyncActiveDocumentState();
        return Task.FromResult(transfer);
    }

    public async Task<bool> AttachAssetDocumentTabFromExternalHostAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || IsBusy)
        {
            return false;
        }

        if (DocumentGroup.TryGetAssetTab(assetTab.BundleId, assetTab.AssetId, out var existingAssetTab))
        {
            assetTab.CancelPreviewRequest();
            await ActivateAssetDocumentTabAsync(existingAssetTab);
            return true;
        }

        DocumentGroup.AddAssetTab(assetTab, activate: false);
        await ActivateAssetDocumentTabAsync(assetTab);
        return true;
    }

    public async Task<bool> AttachAssetDocumentGroupFromExternalHostAsync(AssetDocumentGroupTransfer? transfer)
    {
        if (transfer is null || IsBusy)
        {
            return false;
        }

        DocumentGroup.AddDocumentTabs(transfer);
        await ActivateDocumentTabAsync(DocumentGroup.ActiveDocumentTab);
        return true;
    }

    public async Task RestoreDetachedAssetDocumentTabAsync(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        DocumentGroup.AddAssetTab(assetTab, activate: false);
        await ActivateAssetDocumentTabAsync(assetTab);
    }

    public Task RestoreDetachedAssetDocumentGroupAsync(AssetDocumentGroupTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        DocumentGroup.AddDocumentTabs(transfer);
        return ActivateDocumentTabAsync(DocumentGroup.ActiveDocumentTab);
    }

    public Task RestoreDetachedDocumentTabAsync(AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);

        DocumentGroup.AddDocumentTabs(new AssetDocumentGroupTransfer([documentTab], documentTab));
        return ActivateDocumentTabAsync(documentTab);
    }

    public async Task CompleteExternalAssetDocumentMoveAsync()
    {
        if (!DocumentGroup.HasDocumentTabs)
        {
            EmptyGroupRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        await ActivateDocumentTabAsync(DocumentGroup.ActiveDocumentTab);
    }
}
