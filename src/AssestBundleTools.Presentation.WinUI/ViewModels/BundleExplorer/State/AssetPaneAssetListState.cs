using System.Collections.ObjectModel;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

internal sealed class AssetPaneAssetListState
{
    private readonly List<AssetListEntryViewModel> _sourceItems = [];

    public ObservableCollection<AssetListEntryViewModel> Items { get; } = new();

    public ObservableCollection<AssetListEntryViewModel> SelectedAssets { get; } = new();

    public AssetListEntryViewModel? SelectedAsset { get; private set; }

    public int SourceCount => _sourceItems.Count;

    public int VisibleCount => Items.Count;

    public void ReplaceItems(
        IEnumerable<AssetListEntryViewModel> items,
        AssetId? preferredSelectedAssetId,
        bool showModifiedOnly,
        bool showPreviewableOnly)
    {
        _sourceItems.Clear();
        _sourceItems.AddRange(items);
        ApplyLocalFilters(
            preferredSelectedAssetId,
            showModifiedOnly,
            showPreviewableOnly);
    }

    public void ApplyLocalFilters(
        AssetId? preferredSelectedAssetId,
        bool showModifiedOnly,
        bool showPreviewableOnly)
    {
        var desiredSelectedAssetId = preferredSelectedAssetId ?? SelectedAsset?.Item.AssetId;

        var filteredItems = _sourceItems
            .Where(item => !showModifiedOnly || item.Item.IsModified)
            .Where(item => !showPreviewableOnly || item.Item.HasPreview)
            .ToArray();

        Items.Clear();
        foreach (var item in filteredItems)
        {
            Items.Add(item);
        }

        SelectedAssets.Clear();
        SelectedAsset = desiredSelectedAssetId is { } assetId
            ? Items.FirstOrDefault(item => item.Item.AssetId.Equals(assetId))
            : null;

        if (SelectedAsset is not null)
        {
            SelectedAssets.Add(SelectedAsset);
        }

        UpdateSelectionState();
    }

    public void UpdateSelection(
        IEnumerable<AssetListEntryViewModel> selectedAssets,
        AssetListEntryViewModel? primarySelectedAsset)
    {
        SelectedAssets.Clear();
        foreach (var item in selectedAssets)
        {
            SelectedAssets.Add(item);
        }

        SelectedAsset = primarySelectedAsset;
        UpdateSelectionState();
    }

    public void SetSelectedAsset(AssetListEntryViewModel? selectedAsset)
    {
        SelectedAssets.Clear();
        SelectedAsset = selectedAsset is not null && Items.Contains(selectedAsset)
            ? selectedAsset
            : null;

        if (SelectedAsset is not null)
        {
            SelectedAssets.Add(SelectedAsset);
        }

        UpdateSelectionState();
    }

    public bool TrySelectAsset(AssetId assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId.Value))
        {
            return false;
        }

        var target = Items.FirstOrDefault(item => item.Item.AssetId.Equals(assetId));
        if (target is null)
        {
            return false;
        }

        SelectedAssets.Clear();
        SelectedAssets.Add(target);
        SelectedAsset = target;
        UpdateSelectionState();
        return true;
    }

    public void ClearSelection()
    {
        SelectedAssets.Clear();
        SelectedAsset = null;
        UpdateSelectionState();
    }

    public void Clear()
    {
        foreach (var item in _sourceItems)
        {
            item.IsSelected = false;
        }

        _sourceItems.Clear();
        Items.Clear();
        SelectedAssets.Clear();
        SelectedAsset = null;
    }

    public void UpdateSelectionState()
    {
        foreach (var item in _sourceItems)
        {
            item.IsSelected = false;
        }

        foreach (var item in SelectedAssets)
        {
            item.IsSelected = true;
        }
    }
}
