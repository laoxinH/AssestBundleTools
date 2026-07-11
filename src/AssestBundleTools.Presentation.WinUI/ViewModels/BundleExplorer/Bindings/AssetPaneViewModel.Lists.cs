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
            EmptyStateTitleText,
            emptyStateText,
            preferredSelectedAssetId: null);
    }

    public void ReplaceItems(
        string currentBundleDisplayName,
        IEnumerable<AssetListEntryViewModel> items,
        string emptyStateText,
        AssetId? preferredSelectedAssetId)
    {
        ReplaceItems(
            currentBundleDisplayName,
            items,
            EmptyStateTitleText,
            emptyStateText,
            preferredSelectedAssetId);
    }

    public void ReplaceItems(
        string currentBundleDisplayName,
        IEnumerable<AssetListEntryViewModel> items,
        string emptyStateTitleText,
        string emptyStateText,
        AssetId? preferredSelectedAssetId)
    {
        CurrentBundleDisplayName = currentBundleDisplayName;
        EmptyStateTitleText = emptyStateTitleText;
        EmptyStateText = emptyStateText;
        IsListLoading = false;
        _assetList.ReplaceItems(
            items,
            preferredSelectedAssetId,
            ShowModifiedOnly,
            ShowPreviewableOnly,
            AssetSortColumn,
            AssetSortDirection);
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
        ReplaceSpineGroups(
            currentBundleDisplayName,
            items,
            EmptyStateTitleText,
            emptyStateText);
    }

    public void ReplaceSpineGroups(
        string currentBundleDisplayName,
        IEnumerable<SpineAssetGroupEntryViewModel> items,
        string emptyStateTitleText,
        string emptyStateText)
    {
        var selectedGroupId = SelectedSpineGroup?.Item.GroupId;
        CurrentBundleDisplayName = currentBundleDisplayName;
        EmptyStateTitleText = emptyStateTitleText;
        EmptyStateText = emptyStateText;
        IsListLoading = false;
        _spineGroupList.ReplaceItems(items, selectedGroupId);
        SetSpineGroupMultiSelectionMode(false);
        OnPropertyChanged(nameof(CanToggleSearch));
        OnPropertyChanged(nameof(SelectedSpineGroup));
        NotifyListStateChanged();
        NotifySelectionStateChanged();
    }

    public void BeginAssetListLoading(string currentBundleDisplayName, string loadingStateText)
    {
        BeginAssetListLoading(currentBundleDisplayName, EmptyStateTitleText, loadingStateText);
    }

    public void BeginAssetListLoading(
        string currentBundleDisplayName,
        string loadingStateTitleText,
        string loadingStateText)
    {
        CurrentBundleDisplayName = currentBundleDisplayName;
        EmptyStateTitleText = loadingStateTitleText;
        EmptyStateText = loadingStateText;
        IsListLoading = true;
        _assetList.Clear();
        SetAssetMultiSelectionMode(false);
        OnPropertyChanged(nameof(SelectedAsset));
        OnPropertyChanged(nameof(CanToggleSearch));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }

    public void BeginSpineGroupListLoading(string currentBundleDisplayName, string loadingStateText)
    {
        BeginSpineGroupListLoading(currentBundleDisplayName, EmptyStateTitleText, loadingStateText);
    }

    public void BeginSpineGroupListLoading(
        string currentBundleDisplayName,
        string loadingStateTitleText,
        string loadingStateText)
    {
        CurrentBundleDisplayName = currentBundleDisplayName;
        EmptyStateTitleText = loadingStateTitleText;
        EmptyStateText = loadingStateText;
        IsListLoading = true;
        _spineGroupList.Clear();
        SetSpineGroupMultiSelectionMode(false);
        OnPropertyChanged(nameof(SelectedSpineGroup));
        OnPropertyChanged(nameof(CanToggleSearch));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }

    public void ClearItems(string emptyStateText)
    {
        ClearItems(EmptyStateTitleText, emptyStateText);
    }

    public void ClearItems(string emptyStateTitleText, string emptyStateText)
    {
        CurrentBundleDisplayName = string.Empty;
        EmptyStateTitleText = emptyStateTitleText;
        EmptyStateText = emptyStateText;
        IsListLoading = false;
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
        OnPropertyChanged(nameof(EmptyStateTitleText));
        OnPropertyChanged(nameof(EmptyStateLoadingVisibility));
        OnPropertyChanged(nameof(EmptyStateIconVisibility));
        OnPropertyChanged(nameof(EmptyStateIconGlyph));
        OnPropertyChanged(nameof(MultiSelectionQuickActionsVisibility));
    }

    private void ApplyLocalFilters(AssetId? preferredSelectedAssetId = null)
    {
        _assetList.ApplyLocalFilters(
            preferredSelectedAssetId,
            ShowModifiedOnly,
            ShowPreviewableOnly,
            AssetSortColumn,
            AssetSortDirection);
        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }
}
