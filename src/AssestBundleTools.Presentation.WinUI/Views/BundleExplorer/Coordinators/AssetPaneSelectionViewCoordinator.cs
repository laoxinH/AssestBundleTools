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
            _spineGroupListView.SelectedItems.OfType<SpineAssetGroupEntryViewModel>(),
            _spineGroupListView.SelectedItem as SpineAssetGroupEntryViewModel);
        return true;
    }

    public bool SelectAssetForContextAction(
        AssetListEntryViewModel targetSelection,
        bool preserveExistingSelection)
    {
        ArgumentNullException.ThrowIfNull(targetSelection);

        var viewModel = _getViewModel();
        if (viewModel is null || !viewModel.Items.Contains(targetSelection))
        {
            return false;
        }

        _isSynchronizingAssetSelectionFromViewModel = true;
        try
        {
            if (!preserveExistingSelection)
            {
                _assetListView.SelectedItems.Clear();
            }

            _assetListView.SelectedItem = targetSelection;
            if (!_assetListView.SelectedItems.Contains(targetSelection))
            {
                _assetListView.SelectedItems.Add(targetSelection);
            }

            viewModel.UpdateSelection(
                _assetListView.SelectedItems.OfType<AssetListEntryViewModel>(),
                targetSelection);
            _assetListView.UpdateLayout();
            _assetListView.ScrollIntoView(targetSelection);
            return true;
        }
        finally
        {
            _isSynchronizingAssetSelectionFromViewModel = false;
        }
    }

    public bool SelectSpineGroupForContextAction(
        SpineAssetGroupEntryViewModel targetSelection,
        bool preserveExistingSelection)
    {
        ArgumentNullException.ThrowIfNull(targetSelection);

        var viewModel = _getViewModel();
        if (viewModel is null || !viewModel.SpineGroupItems.Contains(targetSelection))
        {
            return false;
        }

        _isSynchronizingSpineGroupSelectionFromViewModel = true;
        try
        {
            if (!preserveExistingSelection)
            {
                _spineGroupListView.SelectedItems.Clear();
            }

            _spineGroupListView.SelectedItem = targetSelection;
            if (!_spineGroupListView.SelectedItems.Contains(targetSelection))
            {
                _spineGroupListView.SelectedItems.Add(targetSelection);
            }

            viewModel.UpdateSpineGroupSelection(
                _spineGroupListView.SelectedItems.OfType<SpineAssetGroupEntryViewModel>(),
                targetSelection);
            _spineGroupListView.UpdateLayout();
            _spineGroupListView.ScrollIntoView(targetSelection);
            return true;
        }
        finally
        {
            _isSynchronizingSpineGroupSelectionFromViewModel = false;
        }
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
                if (_spineGroupListView.SelectedItem is not null ||
                    _spineGroupListView.SelectedItems.Count > 0)
                {
                    _spineGroupListView.SelectedItem = null;
                    _spineGroupListView.SelectedItems.Clear();
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

            if (_spineGroupListView.SelectedItems.Count != viewModel.SelectedSpineGroups.Count ||
                viewModel.SelectedSpineGroups.Any(item => !_spineGroupListView.SelectedItems.Contains(item)))
            {
                _spineGroupListView.SelectedItems.Clear();
                foreach (var selectedGroup in viewModel.SelectedSpineGroups)
                {
                    _spineGroupListView.SelectedItems.Add(selectedGroup);
                }
            }

            _spineGroupListView.ScrollIntoView(targetSelection);
        }
        finally
        {
            _isSynchronizingSpineGroupSelectionFromViewModel = false;
        }
    }

    public void ScrollSelectedItemIntoView()
    {
        var viewModel = _getViewModel();
        if (viewModel?.IsSpineGroupViewMode == true)
        {
            ScrollSelectedSpineGroupIntoView(viewModel);
            return;
        }

        ScrollSelectedAssetIntoView(viewModel);
    }

    private void ScrollSelectedAssetIntoView(AssetPaneViewModel? viewModel)
    {
        var targetSelection = viewModel?.SelectedAsset;
        if (targetSelection is null || viewModel is null || !viewModel.Items.Contains(targetSelection))
        {
            return;
        }

        _assetListView.UpdateLayout();
        _assetListView.ScrollIntoView(targetSelection);
    }

    private void ScrollSelectedSpineGroupIntoView(AssetPaneViewModel viewModel)
    {
        var targetSelection = viewModel.SelectedSpineGroup;
        if (targetSelection is null || !viewModel.SpineGroupItems.Contains(targetSelection))
        {
            return;
        }

        _spineGroupListView.UpdateLayout();
        _spineGroupListView.ScrollIntoView(targetSelection);
    }
}
