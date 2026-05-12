namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetPaneViewModel
{
    public void EnterMultiSelectionMode()
    {
        if (IsSpineGroupViewMode)
        {
            SetSpineGroupMultiSelectionMode(true);
            return;
        }

        SetAssetMultiSelectionMode(true);
    }

    public void ExitMultiSelectionMode()
    {
        if (IsSpineGroupViewMode)
        {
            _spineGroupList.CollapseSelectionToPrimary();
            SetSpineGroupMultiSelectionMode(false);
            OnPropertyChanged(nameof(SelectedSpineGroup));
            NotifySelectionStateChanged();
            return;
        }

        _assetList.CollapseSelectionToPrimary();
        SetAssetMultiSelectionMode(false);
        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
    }

    public void SelectAllVisibleItems()
    {
        EnterMultiSelectionMode();
        if (IsSpineGroupViewMode)
        {
            _spineGroupList.SelectAllVisibleItems();
            OnPropertyChanged(nameof(SelectedSpineGroup));
            NotifySelectionStateChanged();
            return;
        }

        _assetList.SelectAllVisibleItems();
        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
    }

    public void InvertVisibleSelection()
    {
        EnterMultiSelectionMode();
        if (IsSpineGroupViewMode)
        {
            _spineGroupList.InvertVisibleSelection();
            OnPropertyChanged(nameof(SelectedSpineGroup));
            NotifySelectionStateChanged();
            return;
        }

        _assetList.InvertVisibleSelection();
        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
    }

    private void SetAssetMultiSelectionMode(bool isEnabled)
    {
        if (_isAssetMultiSelectionMode == isEnabled)
        {
            return;
        }

        _isAssetMultiSelectionMode = isEnabled;
        if (IsAssetViewMode)
        {
            NotifySelectionStateChanged();
        }
    }

    private void SetSpineGroupMultiSelectionMode(bool isEnabled)
    {
        if (_isSpineGroupMultiSelectionMode == isEnabled)
        {
            return;
        }

        _isSpineGroupMultiSelectionMode = isEnabled;
        if (IsSpineGroupViewMode)
        {
            NotifySelectionStateChanged();
        }
    }
}
