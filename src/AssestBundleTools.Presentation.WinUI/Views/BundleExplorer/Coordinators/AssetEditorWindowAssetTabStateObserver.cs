using System.Collections.Specialized;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal sealed class AssetEditorWindowAssetTabStateObserver : IDisposable
{
    private readonly AssetDocumentGroupViewModel _documentGroup;
    private readonly Action _stateChanged;
    private readonly HashSet<AssetDocumentTabViewModel> _trackedAssetTabs = [];
    private bool _disposed;

    public AssetEditorWindowAssetTabStateObserver(
        AssetDocumentGroupViewModel documentGroup,
        Action stateChanged)
    {
        _documentGroup = documentGroup ?? throw new ArgumentNullException(nameof(documentGroup));
        _stateChanged = stateChanged ?? throw new ArgumentNullException(nameof(stateChanged));
        _documentGroup.AssetTabs.CollectionChanged += OnDocumentGroupAssetTabsChanged;
        SubscribeAssetTabStateEvents(_documentGroup.AssetTabs);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _documentGroup.AssetTabs.CollectionChanged -= OnDocumentGroupAssetTabsChanged;
        UnsubscribeAssetTabStateEvents(_trackedAssetTabs.ToArray());
        _disposed = true;
    }

    private void OnDocumentGroupAssetTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            SynchronizeAssetTabStateEventSubscriptions();
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<AssetDocumentTabViewModel>())
            {
                UnsubscribeAssetTabStateEvents(oldItem);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<AssetDocumentTabViewModel>())
            {
                SubscribeAssetTabStateEvents(newItem);
            }
        }
    }

    private void OnAssetTabBrowserStateChanged(object? sender, EventArgs e)
    {
        _stateChanged();
    }

    private void SubscribeAssetTabStateEvents(IEnumerable<AssetDocumentTabViewModel> assetTabs)
    {
        foreach (var assetTab in assetTabs)
        {
            SubscribeAssetTabStateEvents(assetTab);
        }
    }

    private void SubscribeAssetTabStateEvents(AssetDocumentTabViewModel assetTab)
    {
        if (!_trackedAssetTabs.Add(assetTab))
        {
            return;
        }

        assetTab.PreviewImageDisplayOptionsChanged += OnAssetTabBrowserStateChanged;
        assetTab.PreviewImageViewStateChanged += OnAssetTabBrowserStateChanged;
        assetTab.PreviewMeshDisplayOptionsChanged += OnAssetTabBrowserStateChanged;
    }

    private void UnsubscribeAssetTabStateEvents(IEnumerable<AssetDocumentTabViewModel> assetTabs)
    {
        foreach (var assetTab in assetTabs)
        {
            UnsubscribeAssetTabStateEvents(assetTab);
        }
    }

    private void UnsubscribeAssetTabStateEvents(AssetDocumentTabViewModel assetTab)
    {
        if (!_trackedAssetTabs.Remove(assetTab))
        {
            return;
        }

        assetTab.PreviewImageDisplayOptionsChanged -= OnAssetTabBrowserStateChanged;
        assetTab.PreviewImageViewStateChanged -= OnAssetTabBrowserStateChanged;
        assetTab.PreviewMeshDisplayOptionsChanged -= OnAssetTabBrowserStateChanged;
    }

    private void SynchronizeAssetTabStateEventSubscriptions()
    {
        var currentAssetTabs = _documentGroup.AssetTabs.ToHashSet();
        foreach (var trackedAssetTab in _trackedAssetTabs
                     .Where(assetTab => !currentAssetTabs.Contains(assetTab))
                     .ToArray())
        {
            UnsubscribeAssetTabStateEvents(trackedAssetTab);
        }

        foreach (var assetTab in currentAssetTabs)
        {
            SubscribeAssetTabStateEvents(assetTab);
        }
    }
}
