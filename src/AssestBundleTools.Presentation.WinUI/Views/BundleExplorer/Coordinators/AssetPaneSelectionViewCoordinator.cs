using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 负责资产窗格中 ListView 与绑定状态之间的选择同步。
/// </summary>
internal sealed partial class AssetPaneSelectionViewCoordinator
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

        var selectedAssets = _assetListView.SelectedItems
            .OfType<AssetListEntryViewModel>()
            .ToArray();
        _getViewModel()?.UpdateSelection(
            selectedAssets,
            AssetPaneListViewSelectionWriter.ResolvePrimarySelection(
                selectedAssets,
                _assetListView.SelectedItem as AssetListEntryViewModel));
        return true;
    }

    public bool TryApplySpineGroupSelectionToViewModel()
    {
        if (_isSynchronizingSpineGroupSelectionFromViewModel)
        {
            return false;
        }

        var selectedSpineGroups = _spineGroupListView.SelectedItems
            .OfType<SpineAssetGroupEntryViewModel>()
            .ToArray();
        _getViewModel()?.UpdateSpineGroupSelection(
            selectedSpineGroups,
            AssetPaneListViewSelectionWriter.ResolvePrimarySelection(
                selectedSpineGroups,
                _spineGroupListView.SelectedItem as SpineAssetGroupEntryViewModel));
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

    public bool ToggleAssetSelectionInMultiSelectionMode(AssetListEntryViewModel targetSelection)
    {
        ArgumentNullException.ThrowIfNull(targetSelection);

        var viewModel = _getViewModel();
        if (viewModel is null ||
            !viewModel.IsAssetViewMode ||
            !viewModel.IsMultiSelectionMode ||
            !viewModel.Items.Contains(targetSelection))
        {
            return false;
        }

        return ApplyAssetSelectionInMultiSelectionMode(
            viewModel,
            targetSelection,
            !viewModel.SelectedAssets.Contains(targetSelection));
    }

    public bool ToggleAssetSelectionRangeInMultiSelectionMode(
        AssetListEntryViewModel anchorSelection,
        AssetListEntryViewModel targetSelection)
    {
        ArgumentNullException.ThrowIfNull(anchorSelection);
        ArgumentNullException.ThrowIfNull(targetSelection);

        var viewModel = _getViewModel();
        if (viewModel is null ||
            !viewModel.IsAssetViewMode ||
            !viewModel.IsMultiSelectionMode ||
            !viewModel.Items.Contains(anchorSelection) ||
            !viewModel.Items.Contains(targetSelection))
        {
            return false;
        }

        return ApplyAssetSelectionToggleRangeInMultiSelectionMode(viewModel, anchorSelection, targetSelection);
    }

    private bool ApplyAssetSelectionInMultiSelectionMode(
        AssetPaneViewModel viewModel,
        AssetListEntryViewModel targetSelection,
        bool shouldSelect)
    {
        var mutation = AssetPaneSelectionRangeResolver.Resolve(
            viewModel.SelectedAssets,
            viewModel.Items,
            targetSelection,
            targetSelection,
            shouldSelect,
            viewModel.SelectedAsset);

        _isSynchronizingAssetSelectionFromViewModel = true;
        try
        {
            viewModel.UpdateSelection(mutation.SelectedItems, mutation.PrimarySelection);
            return true;
        }
        finally
        {
            _isSynchronizingAssetSelectionFromViewModel = false;
        }
    }

    private bool ApplyAssetSelectionToggleRangeInMultiSelectionMode(
        AssetPaneViewModel viewModel,
        AssetListEntryViewModel anchorSelection,
        AssetListEntryViewModel targetSelection)
    {
        if (ReferenceEquals(anchorSelection, targetSelection))
        {
            return false;
        }

        var mutation = AssetPaneSelectionRangeResolver.ResolveToggleRange(
            viewModel.SelectedAssets,
            viewModel.Items,
            anchorSelection,
            targetSelection,
            viewModel.SelectedAsset);

        _isSynchronizingAssetSelectionFromViewModel = true;
        try
        {
            viewModel.UpdateSelection(mutation.SelectedItems, mutation.PrimarySelection);
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

    public bool ToggleSpineGroupSelectionInMultiSelectionMode(SpineAssetGroupEntryViewModel targetSelection)
    {
        ArgumentNullException.ThrowIfNull(targetSelection);

        var viewModel = _getViewModel();
        if (viewModel is null ||
            !viewModel.IsSpineGroupViewMode ||
            !viewModel.IsMultiSelectionMode ||
            !viewModel.SpineGroupItems.Contains(targetSelection))
        {
            return false;
        }

        return ApplySpineGroupSelectionInMultiSelectionMode(
            viewModel,
            targetSelection,
            !viewModel.SelectedSpineGroups.Contains(targetSelection));
    }

    public bool ToggleSpineGroupSelectionRangeInMultiSelectionMode(
        SpineAssetGroupEntryViewModel anchorSelection,
        SpineAssetGroupEntryViewModel targetSelection)
    {
        ArgumentNullException.ThrowIfNull(anchorSelection);
        ArgumentNullException.ThrowIfNull(targetSelection);

        var viewModel = _getViewModel();
        if (viewModel is null ||
            !viewModel.IsSpineGroupViewMode ||
            !viewModel.IsMultiSelectionMode ||
            !viewModel.SpineGroupItems.Contains(anchorSelection) ||
            !viewModel.SpineGroupItems.Contains(targetSelection))
        {
            return false;
        }

        return ApplySpineGroupSelectionToggleRangeInMultiSelectionMode(viewModel, anchorSelection, targetSelection);
    }

    private bool ApplySpineGroupSelectionInMultiSelectionMode(
        AssetPaneViewModel viewModel,
        SpineAssetGroupEntryViewModel targetSelection,
        bool shouldSelect)
    {
        var mutation = AssetPaneSelectionRangeResolver.Resolve(
            viewModel.SelectedSpineGroups,
            viewModel.SpineGroupItems,
            targetSelection,
            targetSelection,
            shouldSelect,
            viewModel.SelectedSpineGroup);

        _isSynchronizingSpineGroupSelectionFromViewModel = true;
        try
        {
            viewModel.UpdateSpineGroupSelection(mutation.SelectedItems, mutation.PrimarySelection);
            return true;
        }
        finally
        {
            _isSynchronizingSpineGroupSelectionFromViewModel = false;
        }
    }

    private bool ApplySpineGroupSelectionToggleRangeInMultiSelectionMode(
        AssetPaneViewModel viewModel,
        SpineAssetGroupEntryViewModel anchorSelection,
        SpineAssetGroupEntryViewModel targetSelection)
    {
        if (ReferenceEquals(anchorSelection, targetSelection))
        {
            return false;
        }

        var mutation = AssetPaneSelectionRangeResolver.ResolveToggleRange(
            viewModel.SelectedSpineGroups,
            viewModel.SpineGroupItems,
            anchorSelection,
            targetSelection,
            viewModel.SelectedSpineGroup);

        _isSynchronizingSpineGroupSelectionFromViewModel = true;
        try
        {
            viewModel.UpdateSpineGroupSelection(mutation.SelectedItems, mutation.PrimarySelection);
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
            SynchronizeAssetSelectionCore(viewModel);
        }
        finally
        {
            _isSynchronizingAssetSelectionFromViewModel = false;
        }
    }

    private void SynchronizeAssetSelectionCore(AssetPaneViewModel viewModel)
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

        AssetPaneListViewSelectionWriter.ApplySelection(
            _assetListView,
            viewModel.SelectedAssets,
            targetSelection);

        _assetListView.ScrollIntoView(targetSelection);
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

            AssetPaneListViewSelectionWriter.ApplySelection(
                _spineGroupListView,
                viewModel.SelectedSpineGroups,
                targetSelection);

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
