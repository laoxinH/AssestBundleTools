using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 负责资产窗格中 ListView 与绑定状态之间的选择同步。
/// </summary>
internal sealed class AssetPaneSelectionViewCoordinator
{
    private readonly ListView _assetListView;
    private readonly ListView _spineGroupListView;
    private readonly Func<AssetPaneViewModel?> _getViewModel;
    private bool _isSynchronizingAssetSelectionFromViewModel;
    private bool _isSynchronizingSpineGroupSelectionFromViewModel;

    public AssetPaneSelectionViewCoordinator(
        ListView assetListView,
        ListView spineGroupListView,
        Func<AssetPaneViewModel?> getViewModel)
    {
        _assetListView = assetListView ?? throw new ArgumentNullException(nameof(assetListView));
        _spineGroupListView = spineGroupListView ?? throw new ArgumentNullException(nameof(spineGroupListView));
        _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
    }

    public bool TryApplyAssetSelectionToViewModel()
    {
        if (_isSynchronizingAssetSelectionFromViewModel)
        {
            return false;
        }

        _getViewModel()?.UpdateSelection(
            _assetListView.SelectedItems.OfType<AssetListEntryViewModel>(),
            _assetListView.SelectedItem as AssetListEntryViewModel);
        return true;
    }

    public bool TryApplySpineGroupSelectionToViewModel()
    {
        if (_isSynchronizingSpineGroupSelectionFromViewModel)
        {
            return false;
        }

        _getViewModel()?.UpdateSpineGroupSelection(
            _spineGroupListView.SelectedItem as SpineAssetGroupEntryViewModel);
        return true;
    }

    public void SynchronizeAssetSelectionFromViewModel()
    {
        if (_isSynchronizingAssetSelectionFromViewModel)
        {
            return;
        }

        var viewModel = _getViewModel();
        if (viewModel is null)
        {
            return;
        }

        _isSynchronizingAssetSelectionFromViewModel = true;
        try
        {
            var targetSelection = viewModel.SelectedAsset;
            if (targetSelection is null)
            {
                if (_assetListView.SelectedItem is not null || _assetListView.SelectedItems.Count > 0)
                {
                    _assetListView.SelectedItem = null;
                    _assetListView.SelectedItems.Clear();
                }

                return;
            }

            if (!viewModel.Items.Contains(targetSelection))
            {
                return;
            }

            if (!ReferenceEquals(_assetListView.SelectedItem, targetSelection))
            {
                _assetListView.SelectedItem = targetSelection;
            }

            if (_assetListView.SelectedItems.Count != 1 ||
                !ReferenceEquals(_assetListView.SelectedItems[0], targetSelection))
            {
                _assetListView.SelectedItems.Clear();
                _assetListView.SelectedItems.Add(targetSelection);
            }

            _assetListView.ScrollIntoView(targetSelection);
        }
        finally
        {
            _isSynchronizingAssetSelectionFromViewModel = false;
        }
    }

    public void SynchronizeSpineGroupSelectionFromViewModel()
    {
        if (_isSynchronizingSpineGroupSelectionFromViewModel)
        {
            return;
        }

        var viewModel = _getViewModel();
        if (viewModel is null)
        {
            return;
        }

        _isSynchronizingSpineGroupSelectionFromViewModel = true;
        try
        {
            var targetSelection = viewModel.SelectedSpineGroup;
            if (targetSelection is null)
            {
                if (_spineGroupListView.SelectedItem is not null)
                {
                    _spineGroupListView.SelectedItem = null;
                }

                return;
            }

            if (!viewModel.SpineGroupItems.Contains(targetSelection))
            {
                return;
            }

            if (!ReferenceEquals(_spineGroupListView.SelectedItem, targetSelection))
            {
                _spineGroupListView.SelectedItem = targetSelection;
            }

            _spineGroupListView.ScrollIntoView(targetSelection);
        }
        finally
        {
            _isSynchronizingSpineGroupSelectionFromViewModel = false;
        }
    }
}
