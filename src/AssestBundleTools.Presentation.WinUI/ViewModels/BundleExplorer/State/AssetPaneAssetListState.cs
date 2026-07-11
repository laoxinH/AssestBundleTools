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
        bool showPreviewableOnly,
        AssetPaneSortColumn sortColumn,
        AssetPaneSortDirection sortDirection)
    {
        _sourceItems.Clear();
        _sourceItems.AddRange(items);
        ApplyLocalFilters(
            preferredSelectedAssetId,
            showModifiedOnly,
            showPreviewableOnly,
            sortColumn,
            sortDirection);
    }

    public void ApplyLocalFilters(
        AssetId? preferredSelectedAssetId,
        bool showModifiedOnly,
        bool showPreviewableOnly,
        AssetPaneSortColumn sortColumn,
        AssetPaneSortDirection sortDirection)
    {
        var desiredSelectedAssetId = preferredSelectedAssetId ?? SelectedAsset?.Item.AssetId;

        var filteredItems = _sourceItems
            .Where(item => !showModifiedOnly || item.Item.IsModified)
            .Where(item => !showPreviewableOnly || item.Item.HasPreview)
            .ToArray();
        var sortedItems = SortItems(filteredItems, sortColumn, sortDirection).ToArray();

        Items.Clear();
        foreach (var item in sortedItems)
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

    public void ApplySort(AssetPaneSortColumn sortColumn, AssetPaneSortDirection sortDirection)
    {
        var sortedItems = SortItems(Items, sortColumn, sortDirection).ToArray();
        for (var targetIndex = 0; targetIndex < sortedItems.Length; targetIndex++)
        {
            var currentIndex = Items.IndexOf(sortedItems[targetIndex]);
            if (currentIndex != targetIndex)
            {
                Items.Move(currentIndex, targetIndex);
            }
        }

        UpdateSelectionState();
    }

    public void UpdateSelection(
        IEnumerable<AssetListEntryViewModel> selectedAssets,
        AssetListEntryViewModel? primarySelectedAsset)
    {
        SelectedAssets.Clear();
        foreach (var item in selectedAssets.Where(Items.Contains))
        {
            SelectedAssets.Add(item);
        }

        SelectedAsset = primarySelectedAsset is not null && Items.Contains(primarySelectedAsset)
            ? primarySelectedAsset
            : SelectedAssets.LastOrDefault();
        UpdateSelectionState();
    }

    public void SelectAllVisibleItems()
    {
        SelectedAssets.Clear();
        foreach (var item in Items)
        {
            SelectedAssets.Add(item);
        }

        SelectedAsset = SelectedAssets.LastOrDefault();
        UpdateSelectionState();
    }

    public void InvertVisibleSelection()
    {
        var selectedSet = SelectedAssets.ToHashSet();
        var invertedItems = Items
            .Where(item => !selectedSet.Contains(item))
            .ToArray();

        SelectedAssets.Clear();
        foreach (var item in invertedItems)
        {
            SelectedAssets.Add(item);
        }

        SelectedAsset = SelectedAssets.LastOrDefault();
        UpdateSelectionState();
    }

    public void CollapseSelectionToPrimary()
    {
        if (SelectedAsset is null || !SelectedAssets.Contains(SelectedAsset))
        {
            SelectedAsset = SelectedAssets.LastOrDefault();
        }

        SelectedAssets.Clear();
        if (SelectedAsset is not null)
        {
            SelectedAssets.Add(SelectedAsset);
        }

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

    public bool TryGetSourceAsset(
        AssetId assetId,
        out AssetListEntryViewModel? assetEntry)
    {
        assetEntry = string.IsNullOrWhiteSpace(assetId.Value)
            ? null
            : _sourceItems.FirstOrDefault(item => item.Item.AssetId.Equals(assetId));
        return assetEntry is not null;
    }

    public bool ContainsVisibleAsset(AssetId assetId) =>
        !string.IsNullOrWhiteSpace(assetId.Value) &&
        Items.Any(item => item.Item.AssetId.Equals(assetId));

    public static bool ShouldBeVisible(
        AssetListEntryViewModel assetEntry,
        bool showModifiedOnly,
        bool showPreviewableOnly)
    {
        ArgumentNullException.ThrowIfNull(assetEntry);

        return (!showModifiedOnly || assetEntry.Item.IsModified) &&
               (!showPreviewableOnly || assetEntry.Item.HasPreview);
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

    private static IEnumerable<AssetListEntryViewModel> SortItems(
        IEnumerable<AssetListEntryViewModel> items,
        AssetPaneSortColumn sortColumn,
        AssetPaneSortDirection sortDirection)
    {
        return (sortColumn, sortDirection) switch
        {
            (AssetPaneSortColumn.Name, AssetPaneSortDirection.Ascending) => items
                .OrderBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static item => item.DisplayAssetPath, StringComparer.CurrentCultureIgnoreCase),
            (AssetPaneSortColumn.Name, AssetPaneSortDirection.Descending) => items
                .OrderByDescending(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static item => item.DisplayAssetPath, StringComparer.CurrentCultureIgnoreCase),
            (AssetPaneSortColumn.Size, AssetPaneSortDirection.Ascending) => items
                .OrderBy(static item => ResolveDisplaySizeInBytes(item))
                .ThenBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            (AssetPaneSortColumn.Size, AssetPaneSortDirection.Descending) => items
                .OrderByDescending(static item => ResolveDisplaySizeInBytes(item))
                .ThenBy(static item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => items
        };
    }

    private static long ResolveDisplaySizeInBytes(AssetListEntryViewModel item) =>
        item.Item.PreferredDisplaySizeInBytes ?? item.Item.SizeInBytes;
}
