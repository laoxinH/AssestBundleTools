using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

internal sealed class AssetPaneSpineGroupListState
{
    private readonly List<SpineAssetGroupEntryViewModel> _sourceItems = [];

    public ObservableCollection<SpineAssetGroupEntryViewModel> Items { get; } = new();

    public SpineAssetGroupEntryViewModel? SelectedSpineGroup { get; private set; }

    public int SourceCount => _sourceItems.Count;

    public int VisibleCount => Items.Count;

    public void ReplaceItems(
        IEnumerable<SpineAssetGroupEntryViewModel> items,
        string? preferredSelectedGroupId)
    {
        _sourceItems.Clear();
        _sourceItems.AddRange(items);
        ApplyItems(preferredSelectedGroupId);
    }

    public void SetSelectedSpineGroup(SpineAssetGroupEntryViewModel? selectedSpineGroup)
    {
        SelectedSpineGroup = selectedSpineGroup;
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
        SelectedSpineGroup = null;
    }

    private void ApplyItems(string? preferredSelectedGroupId)
    {
        Items.Clear();
        foreach (var item in _sourceItems)
        {
            Items.Add(item);
        }

        SelectedSpineGroup = !string.IsNullOrWhiteSpace(preferredSelectedGroupId)
            ? Items.FirstOrDefault(item =>
                string.Equals(item.Item.GroupId, preferredSelectedGroupId, StringComparison.Ordinal))
            : null;
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        foreach (var item in _sourceItems)
        {
            item.IsSelected = ReferenceEquals(item, SelectedSpineGroup);
        }
    }
}
