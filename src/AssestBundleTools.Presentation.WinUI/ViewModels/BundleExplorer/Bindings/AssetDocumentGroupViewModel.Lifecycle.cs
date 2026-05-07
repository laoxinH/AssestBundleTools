using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetDocumentGroupViewModel
{
    public void RemoveAssetTab(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!AssetTabs.Remove(assetTab))
        {
            return;
        }

        assetTab.CancelPreviewRequest();
        RemoveDocumentTab(assetTab);
    }

    public void RemoveSpineGroupTab(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTab);

        if (!SpineGroupTabs.Remove(spineGroupTab))
        {
            return;
        }

        RemoveDocumentTab(spineGroupTab);
    }

    public bool DetachAssetTab(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!AssetTabs.Remove(assetTab))
        {
            return false;
        }

        DetachDocumentTabCore(DocumentTabs.FirstOrDefault(tab => tab.Wraps(assetTab)), disposeWrapper: true);
        return true;
    }

    public bool DetachSpineGroupTab(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTab);

        if (!SpineGroupTabs.Remove(spineGroupTab))
        {
            return false;
        }

        DetachDocumentTabCore(DocumentTabs.FirstOrDefault(tab => tab.Wraps(spineGroupTab)), disposeWrapper: true);
        return true;
    }

    public bool DetachDocumentTab(AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);

        if (!DocumentTabs.Contains(documentTab))
        {
            return false;
        }

        if (documentTab.AssetTab is { } assetTab)
        {
            AssetTabs.Remove(assetTab);
        }
        else if (documentTab.SpineGroupTab is { } spineGroupTab)
        {
            SpineGroupTabs.Remove(spineGroupTab);
        }

        DetachDocumentTabCore(documentTab, disposeWrapper: false);
        NotifyGroupStateChanged();
        return true;
    }

    public AssetDocumentGroupTransfer? DetachAssetTabs()
    {
        if (AssetTabs.Count == 0)
        {
            return null;
        }

        var transfer = new AssetDocumentGroupTransfer(AssetTabs.ToArray(), ActiveAssetTab);
        foreach (var documentTab in DocumentTabs.Where(static tab => tab.IsAsset).ToArray())
        {
            DetachDocumentTabCore(documentTab, disposeWrapper: true);
        }

        AssetTabs.Clear();
        if (ActiveAssetTab is not null)
        {
            SetActiveDocumentTab(DocumentTabs.FirstOrDefault(), cancelPreviousAsset: false);
        }

        NotifyGroupStateChanged();
        return transfer;
    }

    public AssetDocumentGroupTransfer? DetachDocumentTabs()
    {
        if (DocumentTabs.Count == 0)
        {
            return null;
        }

        var transfer = new AssetDocumentGroupTransfer(DocumentTabs.ToArray(), ActiveDocumentTab);
        DocumentTabs.Clear();
        AssetTabs.Clear();
        SpineGroupTabs.Clear();
        ActiveDocumentTab = null;
        ActiveAssetTab = null;
        ActiveSpineGroupTab = null;
        NotifyGroupStateChanged();
        return transfer;
    }

    public void ClearAssetTabs()
    {
        foreach (var assetTab in AssetTabs)
        {
            assetTab.CancelPreviewRequest();
        }

        foreach (var documentTab in DocumentTabs.Where(static tab => tab.IsAsset).ToArray())
        {
            RemoveDocumentTabCore(documentTab);
        }

        AssetTabs.Clear();
        if (ActiveAssetTab is not null)
        {
            SetActiveDocumentTab(DocumentTabs.FirstOrDefault());
        }

        NotifyGroupStateChanged();
    }

    public void ClearSpineGroupTabs()
    {
        foreach (var documentTab in DocumentTabs.Where(static tab => tab.IsSpineGroup).ToArray())
        {
            RemoveDocumentTabCore(documentTab);
        }

        SpineGroupTabs.Clear();
        if (ActiveSpineGroupTab is not null)
        {
            SetActiveDocumentTab(DocumentTabs.FirstOrDefault());
        }

        NotifyGroupStateChanged();
    }

    public void Clear()
    {
        foreach (var assetTab in AssetTabs)
        {
            assetTab.CancelPreviewRequest();
        }

        foreach (var documentTab in DocumentTabs)
        {
            documentTab.Dispose();
        }

        DocumentTabs.Clear();
        AssetTabs.Clear();
        SpineGroupTabs.Clear();
        ActiveDocumentTab = null;
        ActiveAssetTab = null;
        ActiveSpineGroupTab = null;
        NotifyGroupStateChanged();
    }

    public void CancelAllAssetPreviewRequests()
    {
        foreach (var assetTab in AssetTabs)
        {
            assetTab.CancelPreviewRequest();
        }
    }

    private void RemoveDocumentTab(AssetDocumentTabViewModel assetTab)
    {
        var documentTab = DocumentTabs.FirstOrDefault(tab => tab.Wraps(assetTab));
        if (documentTab is null)
        {
            return;
        }

        RemoveDocumentTabCore(documentTab);
        if (ReferenceEquals(ActiveDocumentTab, documentTab))
        {
            SetActiveDocumentTab(DocumentTabs.FirstOrDefault());
        }

        NotifyGroupStateChanged();
    }

    private void RemoveDocumentTab(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        var documentTab = DocumentTabs.FirstOrDefault(tab => tab.Wraps(spineGroupTab));
        if (documentTab is null)
        {
            return;
        }

        RemoveDocumentTabCore(documentTab);
        if (ReferenceEquals(ActiveDocumentTab, documentTab))
        {
            SetActiveDocumentTab(DocumentTabs.FirstOrDefault());
        }

        NotifyGroupStateChanged();
    }

    private void RemoveDocumentTabCore(AssetDocumentTabItemViewModel documentTab)
    {
        if (DocumentTabs.Remove(documentTab))
        {
            documentTab.Dispose();
        }
    }

    private void DetachDocumentTabCore(AssetDocumentTabItemViewModel? documentTab, bool disposeWrapper)
    {
        if (documentTab is null || !DocumentTabs.Remove(documentTab))
        {
            return;
        }

        if (disposeWrapper)
        {
            documentTab.Dispose();
        }

        if (ReferenceEquals(ActiveDocumentTab, documentTab))
        {
            SetActiveDocumentTab(DocumentTabs.FirstOrDefault(), cancelPreviousAsset: false);
        }

        NotifyGroupStateChanged();
    }
}
