using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示一个可承载普通资源文档与 Spine 组文档的页面级标签组。
/// </summary>
public sealed partial class AssetDocumentGroupViewModel : ObservableObject
{
    private AssetDocumentTabItemViewModel? _activeDocumentTab;
    private AssetDocumentTabViewModel? _activeAssetTab;
    private SpineAssetGroupDocumentTabViewModel? _activeSpineGroupTab;

    public ObservableCollection<AssetDocumentTabItemViewModel> DocumentTabs { get; } = new();

    public ObservableCollection<AssetDocumentTabViewModel> AssetTabs { get; } = new();

    public ObservableCollection<SpineAssetGroupDocumentTabViewModel> SpineGroupTabs { get; } = new();

    public AssetDocumentTabItemViewModel? ActiveDocumentTab
    {
        get => _activeDocumentTab;
        private set
        {
            if (SetProperty(ref _activeDocumentTab, value))
            {
                NotifyGroupStateChanged();
            }
        }
    }

    public AssetDocumentTabViewModel? ActiveAssetTab
    {
        get => _activeAssetTab;
        private set => SetProperty(ref _activeAssetTab, value);
    }

    public SpineAssetGroupDocumentTabViewModel? ActiveSpineGroupTab
    {
        get => _activeSpineGroupTab;
        private set => SetProperty(ref _activeSpineGroupTab, value);
    }

    public bool HasDocumentTabs => DocumentTabs.Count > 0;

    public bool HasAssetTabs => AssetTabs.Count > 0;

    public bool HasSpineGroupTabs => SpineGroupTabs.Count > 0;

    public Visibility TabStripVisibility => HasDocumentTabs ? Visibility.Visible : Visibility.Collapsed;

    public bool TryGetAssetTab(
        BundleId bundleId,
        AssetId assetId,
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab)
    {
        assetTab = AssetTabs.FirstOrDefault(tab =>
            tab.BundleId.Equals(bundleId) &&
            tab.AssetId.Equals(assetId));
        return assetTab is not null;
    }

    public bool TryGetSpineGroupTab(
        BundleId bundleId,
        string groupId,
        [NotNullWhen(true)] out SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        spineGroupTab = string.IsNullOrWhiteSpace(groupId)
            ? null
            : SpineGroupTabs.FirstOrDefault(tab =>
                tab.BundleId.Equals(bundleId) &&
                string.Equals(tab.GroupId, groupId, StringComparison.Ordinal));
        return spineGroupTab is not null;
    }

    public void AddAssetTab(AssetDocumentTabViewModel assetTab, bool activate)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        var documentTab = GetOrAddAssetDocumentTab(assetTab);
        if (activate)
        {
            SetActiveDocumentTab(documentTab);
        }
    }

    public void AddSpineGroupTab(SpineAssetGroupDocumentTabViewModel spineGroupTab, bool activate)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTab);

        var documentTab = GetOrAddSpineGroupDocumentTab(spineGroupTab);
        if (activate)
        {
            SetActiveDocumentTab(documentTab);
        }
    }

    public void AddAssetTabs(AssetDocumentGroupTransfer transfer) => AddDocumentTabs(transfer);

    public void AddDocumentTabs(AssetDocumentGroupTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        AssetDocumentTabItemViewModel? activeDocumentTab = null;
        foreach (var documentTab in transfer.DocumentTabs)
        {
            var attachedDocumentTab = AttachDocumentTab(documentTab);
            if (ReferenceEquals(documentTab, transfer.ActiveDocumentTab))
            {
                activeDocumentTab = attachedDocumentTab;
            }
        }

        NotifyGroupStateChanged();
        SetActiveDocumentTab(activeDocumentTab ?? ResolveFallbackActiveDocumentTab(transfer));
    }

    public void SetActiveAssetTab(AssetDocumentTabViewModel? assetTab)
    {
        SetActiveDocumentTab(assetTab is null ? null : GetOrAddAssetDocumentTab(assetTab));
    }

    public void ClearActiveAssetTab()
    {
        if (ActiveDocumentTab?.IsSpineGroup == true)
        {
            return;
        }

        var fallbackDocumentTab = DocumentTabs.FirstOrDefault(static tab => tab.IsSpineGroup);
        if (ActiveAssetTab is not null || (ActiveDocumentTab is null && fallbackDocumentTab is not null))
        {
            SetActiveDocumentTab(fallbackDocumentTab);
        }
    }

    public void SetActiveSpineGroupTab(SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        SetActiveDocumentTab(spineGroupTab is null ? null : GetOrAddSpineGroupDocumentTab(spineGroupTab));
    }

    public void SetActiveDocumentTab(AssetDocumentTabItemViewModel? documentTab)
    {
        SetActiveDocumentTab(documentTab, cancelPreviousAsset: true);
    }

    private void SetActiveDocumentTab(
        AssetDocumentTabItemViewModel? documentTab,
        bool cancelPreviousAsset)
    {
        if (documentTab is not null && !DocumentTabs.Contains(documentTab))
        {
            AttachDocumentTab(documentTab);
        }

        var previousActiveAssetTab = ActiveAssetTab;
        ActiveDocumentTab = documentTab;
        ActiveAssetTab = documentTab?.AssetTab;
        ActiveSpineGroupTab = documentTab?.SpineGroupTab;
        if (cancelPreviousAsset && !ReferenceEquals(previousActiveAssetTab, ActiveAssetTab))
        {
            previousActiveAssetTab?.CancelPreviewRequest();
        }
    }

    public void RestoreAssetTabs(
        IEnumerable<AssetDocumentTabViewModel> assetTabs,
        string? activeAssetTabId)
    {
        ArgumentNullException.ThrowIfNull(assetTabs);

        ClearAssetTabs();
        AssetDocumentTabViewModel? activeAssetTab = null;
        foreach (var assetTab in assetTabs)
        {
            AddAssetTab(assetTab, activate: false);
            if (activeAssetTab is null &&
                string.Equals(assetTab.DocumentId, activeAssetTabId, StringComparison.Ordinal))
            {
                activeAssetTab = assetTab;
            }
        }

        SetActiveAssetTab(activeAssetTab ?? AssetTabs.FirstOrDefault());
    }

    public void RestoreSpineGroupTabs(
        IEnumerable<SpineAssetGroupDocumentTabViewModel> spineGroupTabs,
        string? activeSpineGroupTabId)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTabs);

        ClearSpineGroupTabs();
        SpineAssetGroupDocumentTabViewModel? activeSpineGroupTab = null;
        foreach (var spineGroupTab in spineGroupTabs)
        {
            AddSpineGroupTab(spineGroupTab, activate: false);
            if (activeSpineGroupTab is null &&
                string.Equals(spineGroupTab.DocumentId, activeSpineGroupTabId, StringComparison.Ordinal))
            {
                activeSpineGroupTab = spineGroupTab;
            }
        }

        if (activeSpineGroupTab is not null)
        {
            SetActiveSpineGroupTab(activeSpineGroupTab);
        }
    }

    public bool ReorderAssetTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedAssetTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedAssetTabs);

        var reordered = ObservableCollectionOrderHelper.Reorder(AssetTabs, orderedAssetTabs);
        if (reordered && DocumentTabs.All(static tab => tab.IsAsset))
        {
            ObservableCollectionOrderHelper.Reorder(
                DocumentTabs,
                orderedAssetTabs
                    .Select(assetTab => DocumentTabs.First(tab => tab.Wraps(assetTab)))
                    .ToArray());
        }

        NotifyGroupStateChanged();
        return reordered;
    }

    public bool ReorderSpineGroupTabs(IReadOnlyList<SpineAssetGroupDocumentTabViewModel> orderedSpineGroupTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedSpineGroupTabs);

        var reordered = ObservableCollectionOrderHelper.Reorder(SpineGroupTabs, orderedSpineGroupTabs);
        NotifyGroupStateChanged();
        return reordered;
    }

    public bool ReorderDocumentTabs(IReadOnlyList<AssetDocumentTabItemViewModel> orderedDocumentTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedDocumentTabs);

        if (!ObservableCollectionOrderHelper.Reorder(DocumentTabs, orderedDocumentTabs))
        {
            return false;
        }

        ObservableCollectionOrderHelper.Reorder(
            AssetTabs,
            DocumentTabs
                .Select(static tab => tab.AssetTab)
                .Where(static tab => tab is not null)
                .Cast<AssetDocumentTabViewModel>()
                .ToArray());
        ObservableCollectionOrderHelper.Reorder(
            SpineGroupTabs,
            DocumentTabs
                .Select(static tab => tab.SpineGroupTab)
                .Where(static tab => tab is not null)
                .Cast<SpineAssetGroupDocumentTabViewModel>()
                .ToArray());
        NotifyGroupStateChanged();
        return true;
    }

    private AssetDocumentTabItemViewModel AttachDocumentTab(AssetDocumentTabItemViewModel documentTab)
    {
        if (documentTab.AssetTab is { } assetTab)
        {
            if (TryGetAssetTab(assetTab.BundleId, assetTab.AssetId, out var existingAssetTab))
            {
                assetTab.CancelPreviewRequest();
                documentTab.Dispose();
                return DocumentTabs.First(tab => tab.Wraps(existingAssetTab));
            }

            AssetTabs.Add(assetTab);
        }
        else if (documentTab.SpineGroupTab is { } spineGroupTab)
        {
            if (TryGetSpineGroupTab(spineGroupTab.BundleId, spineGroupTab.GroupId, out var existingSpineGroupTab))
            {
                documentTab.Dispose();
                return DocumentTabs.First(tab => tab.Wraps(existingSpineGroupTab));
            }

            SpineGroupTabs.Add(spineGroupTab);
        }

        DocumentTabs.Add(documentTab);
        return documentTab;
    }

    private AssetDocumentTabItemViewModel GetOrAddAssetDocumentTab(AssetDocumentTabViewModel assetTab)
    {
        if (DocumentTabs.FirstOrDefault(tab => tab.Wraps(assetTab)) is { } documentTab)
        {
            return documentTab;
        }

        if (!AssetTabs.Contains(assetTab))
        {
            AssetTabs.Add(assetTab);
        }

        var newDocumentTab = AssetDocumentTabItemViewModel.FromAsset(assetTab);
        DocumentTabs.Add(newDocumentTab);
        NotifyGroupStateChanged();
        return newDocumentTab;
    }

    private AssetDocumentTabItemViewModel GetOrAddSpineGroupDocumentTab(
        SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        if (DocumentTabs.FirstOrDefault(tab => tab.Wraps(spineGroupTab)) is { } documentTab)
        {
            return documentTab;
        }

        if (!SpineGroupTabs.Contains(spineGroupTab))
        {
            SpineGroupTabs.Add(spineGroupTab);
        }

        var newDocumentTab = AssetDocumentTabItemViewModel.FromSpineGroup(spineGroupTab);
        DocumentTabs.Add(newDocumentTab);
        NotifyGroupStateChanged();
        return newDocumentTab;
    }

    private AssetDocumentTabItemViewModel? ResolveFallbackActiveDocumentTab(AssetDocumentGroupTransfer transfer)
    {
        if (transfer.ActiveDocumentTab.AssetTab is { } activeAssetTab &&
            DocumentTabs.FirstOrDefault(tab => tab.Wraps(activeAssetTab)) is { } assetDocumentTab)
        {
            return assetDocumentTab;
        }

        if (transfer.ActiveDocumentTab.SpineGroupTab is { } activeSpineGroupTab &&
            DocumentTabs.FirstOrDefault(tab => tab.Wraps(activeSpineGroupTab)) is { } spineGroupDocumentTab)
        {
            return spineGroupDocumentTab;
        }

        return DocumentTabs.FirstOrDefault();
    }

    private void NotifyGroupStateChanged()
    {
        OnPropertyChanged(nameof(HasDocumentTabs));
        OnPropertyChanged(nameof(HasAssetTabs));
        OnPropertyChanged(nameof(HasSpineGroupTabs));
        OnPropertyChanged(nameof(TabStripVisibility));
    }
}
