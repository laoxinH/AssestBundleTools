using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetPaneViewModel
{
    public void ReplaceAssetTypeOptions(IEnumerable<AssetTypeOptionViewModel> options)
    {
        _assetTypeFilter.ReplaceOptions(options);
        OnPropertyChanged(nameof(IsReplacingAssetTypeOptions));
        OnPropertyChanged(nameof(SelectedAssetTypeOption));
        NotifyAssetTypeFilterSelectionChanged();
    }

    public void ReplaceItems(string currentBundleDisplayName, IEnumerable<AssetListEntryViewModel> items, string emptyStateText)
    {
        ReplaceItems(
            currentBundleDisplayName,
            items,
            emptyStateText,
            preferredSelectedAssetId: null);
    }

    public void ReplaceItems(
        string currentBundleDisplayName,
        IEnumerable<AssetListEntryViewModel> items,
        string emptyStateText,
        AssetId? preferredSelectedAssetId)
    {
        CurrentBundleDisplayName = currentBundleDisplayName;
        EmptyStateText = emptyStateText;
        _assetList.ReplaceItems(
            items,
            preferredSelectedAssetId,
            ShowModifiedOnly,
            ShowPreviewableOnly);
        SetAssetMultiSelectionMode(false);
        OnPropertyChanged(nameof(CanToggleSearch));
        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }

    public void ReplaceSpineGroups(
        string currentBundleDisplayName,
        IEnumerable<SpineAssetGroupEntryViewModel> items,
        string emptyStateText)
    {
        var selectedGroupId = SelectedSpineGroup?.Item.GroupId;
        CurrentBundleDisplayName = currentBundleDisplayName;
        EmptyStateText = emptyStateText;
        _spineGroupList.ReplaceItems(items, selectedGroupId);
        SetSpineGroupMultiSelectionMode(false);
        OnPropertyChanged(nameof(CanToggleSearch));
        OnPropertyChanged(nameof(SelectedSpineGroup));
        NotifyListStateChanged();
        NotifySelectionStateChanged();
    }

    public void BeginAssetListLoading(string currentBundleDisplayName, string loadingStateText)
    {
        CurrentBundleDisplayName = currentBundleDisplayName;
        EmptyStateText = loadingStateText;
        _assetList.Clear();
        SetAssetMultiSelectionMode(false);
        OnPropertyChanged(nameof(SelectedAsset));
        OnPropertyChanged(nameof(CanToggleSearch));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }

    public void BeginSpineGroupListLoading(string currentBundleDisplayName, string loadingStateText)
    {
        CurrentBundleDisplayName = currentBundleDisplayName;
        EmptyStateText = loadingStateText;
        _spineGroupList.Clear();
        SetSpineGroupMultiSelectionMode(false);
        OnPropertyChanged(nameof(SelectedSpineGroup));
        OnPropertyChanged(nameof(CanToggleSearch));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }

    public void ClearItems(string emptyStateText)
    {
        CurrentBundleDisplayName = string.Empty;
        EmptyStateText = emptyStateText;
        _assetList.Clear();
        _spineGroupList.Clear();
        SetAssetMultiSelectionMode(false);
        SetSpineGroupMultiSelectionMode(false);
        IsSearchExpanded = false;
        OnPropertyChanged(nameof(SelectedAsset));
        OnPropertyChanged(nameof(SelectedSpineGroup));
        OnPropertyChanged(nameof(CanToggleSearch));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }

    public void NotifyListStateChanged()
    {
        OnPropertyChanged(nameof(AssetListVisibility));
        OnPropertyChanged(nameof(SpineGroupListVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(MultiSelectionQuickActionsVisibility));
    }

    private void ApplyLocalFilters(AssetId? preferredSelectedAssetId = null)
    {
        _assetList.ApplyLocalFilters(
            preferredSelectedAssetId,
            ShowModifiedOnly,
            ShowPreviewableOnly);
        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }
}
