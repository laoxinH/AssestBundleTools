namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetEditorWindowViewModel
{
    public async Task ActivateSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        if (spineGroupTab is null || IsBusy)
        {
            return;
        }

        DocumentGroup.SetActiveSpineGroupTab(spineGroupTab);
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        SyncActiveDocumentState();
        await LoadSpineGroupDetailsAsync(spineGroupTab);
    }

    public async Task CloseSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        if (spineGroupTab is null || IsBusy)
        {
            return;
        }

        var wasActive = ReferenceEquals(DocumentGroup.ActiveSpineGroupTab, spineGroupTab);
        DocumentGroup.RemoveSpineGroupTab(spineGroupTab);
        if (!DocumentGroup.HasDocumentTabs)
        {
            EmptyGroupRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (wasActive)
        {
            await ActivateDocumentTabAsync(DocumentGroup.ActiveDocumentTab);
            return;
        }

        SyncActiveDocumentState();
    }

    public async Task SelectActiveSpineGroupAtlasPageAsync(SpineAtlasPageOptionViewModel? atlasPageOption)
    {
        if (atlasPageOption is null ||
            DocumentGroup.ActiveSpineGroupTab is not { } spineGroupTab ||
            !spineGroupTab.SelectAtlasPageOption(atlasPageOption))
        {
            return;
        }

        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        OnPropertyChanged(nameof(Inspector));

        var texturePreview = await _spineGroupDetailsLoader.LoadTexturePreviewAsync(spineGroupTab, atlasPageOption);
        if (!ReferenceEquals(DocumentGroup.ActiveSpineGroupTab, spineGroupTab) ||
            !ReferenceEquals(spineGroupTab.SelectedAtlasPageOption, atlasPageOption))
        {
            return;
        }

        spineGroupTab.ApplyTexturePreview(texturePreview);
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        OnPropertyChanged(nameof(Inspector));
    }

    private async Task LoadSpineGroupDetailsAsync(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        if (spineGroupTab.Details is not null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var expectedGroupId = spineGroupTab.GroupId;
            var loadResult = await _spineGroupDetailsLoader.LoadAsync(spineGroupTab);
            if (loadResult is null ||
                !ReferenceEquals(DocumentGroup.ActiveSpineGroupTab, spineGroupTab) ||
                !string.Equals(expectedGroupId, spineGroupTab.GroupId, StringComparison.Ordinal))
            {
                return;
            }

            spineGroupTab.ApplyDetails(loadResult.Details, loadResult.TexturePreview);
            _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
            OnPropertyChanged(nameof(Inspector));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
