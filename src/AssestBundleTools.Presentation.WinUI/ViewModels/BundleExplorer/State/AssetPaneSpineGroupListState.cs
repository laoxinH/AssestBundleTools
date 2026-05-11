using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

internal sealed class AssetPaneSpineGroupListState
{
    private readonly List<SpineAssetGroupEntryViewModel> _sourceItems = [];

    public ObservableCollection<SpineAssetGroupEntryViewModel> Items { get; } = new();

    public ObservableCollection<SpineAssetGroupEntryViewModel> SelectedSpineGroups { get; } = new();

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
        SelectedSpineGroups.Clear();
        SelectedSpineGroup = selectedSpineGroup;
        if (SelectedSpineGroup is not null && Items.Contains(SelectedSpineGroup))
        {
            SelectedSpineGroups.Add(SelectedSpineGroup);
        }

        UpdateSelectionState();
    }

    public void UpdateSelection(
        IEnumerable<SpineAssetGroupEntryViewModel> selectedSpineGroups,
        SpineAssetGroupEntryViewModel? primarySelectedSpineGroup)
    {
        SelectedSpineGroups.Clear();
        foreach (var item in selectedSpineGroups.Where(Items.Contains))
        {
            SelectedSpineGroups.Add(item);
        }

        if (primarySelectedSpineGroup is not null && Items.Contains(primarySelectedSpineGroup))
        {
            SelectedSpineGroup = primarySelectedSpineGroup;
            if (!SelectedSpineGroups.Contains(primarySelectedSpineGroup))
            {
                SelectedSpineGroups.Add(primarySelectedSpineGroup);
            }
        }
        else
        {
            SelectedSpineGroup = SelectedSpineGroups.LastOrDefault();
        }

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
        SelectedSpineGroups.Clear();
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
        SelectedSpineGroups.Clear();
        if (SelectedSpineGroup is not null)
        {
            SelectedSpineGroups.Add(SelectedSpineGroup);
        }

        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        foreach (var item in _sourceItems)
        {
            item.IsSelected = SelectedSpineGroups.Contains(item);
        }
    }
}
