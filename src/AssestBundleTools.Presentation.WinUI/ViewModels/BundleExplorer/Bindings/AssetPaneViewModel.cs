using System.Collections.ObjectModel;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示中间区域一个资产窗格的全部展示状态。
/// </summary>
public sealed partial class AssetPaneViewModel : ObservableObject
{
    private readonly AssetPaneAssetListState _assetList = new();
    private readonly AssetPaneSpineGroupListState _spineGroupList = new();
    private readonly AssetPaneAssetTypeFilterState _assetTypeFilter = new();

    private string _paneTitle = string.Empty;
    private string _currentBundleDisplayName = string.Empty;
    private string _searchKeyword = string.Empty;
    private string _emptyStateTitleText = string.Empty;
    private string _emptyStateText = string.Empty;
    private AssetPaneViewMode _viewMode;
    private bool _isSearchExpanded;
    private bool _showModifiedOnly;
    private bool _showPreviewableOnly;
    private bool _supportsBatchExtractionQuickAction;
    private bool _isBusy;
    private bool _isListLoading;
    private bool _isAssetMultiSelectionMode;
    private bool _isSpineGroupMultiSelectionMode;
    private PaneId _paneId;

    public AssetPaneViewModel()
    {
        PaneId = PaneId.New();
    }

    public PaneId PaneId
    {
        get => _paneId;
        private set => SetProperty(ref _paneId, value);
    }

    public string PaneTitle
    {
        get => _paneTitle;
        set => SetProperty(ref _paneTitle, value);
    }

    public string CurrentBundleDisplayName
    {
        get => _currentBundleDisplayName;
        set => SetProperty(ref _currentBundleDisplayName, value);
    }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    public AssetPaneViewMode ViewMode
    {
        get => _viewMode;
        set
        {
            if (SetProperty(ref _viewMode, value))
            {
                NotifyViewModeChanged();
            }
        }
    }

    public bool IsAssetViewMode => ViewMode == AssetPaneViewMode.Assets;

    public bool IsSpineGroupViewMode => ViewMode == AssetPaneViewMode.SpineGroups;

    /// <summary>
    /// 控制资产列表搜索框是否处于展开状态。
    /// 该状态只属于当前窗格展示层，不参与应用层业务决策。
    /// </summary>
    public bool IsSearchExpanded
    {
        get => _isSearchExpanded;
        private set
        {
            if (SetProperty(ref _isSearchExpanded, value))
            {
                OnPropertyChanged(nameof(SearchToggleGlyph));
            }
        }
    }

    public string EmptyStateText
    {
        get => _emptyStateText;
        set => SetProperty(ref _emptyStateText, value);
    }

    public string EmptyStateTitleText
    {
        get => _emptyStateTitleText;
        set => SetProperty(ref _emptyStateTitleText, value);
    }

    public bool IsListLoading
    {
        get => _isListLoading;
        private set
        {
            if (SetProperty(ref _isListLoading, value))
            {
                NotifyListStateChanged();
            }
        }
    }

    /// <summary>
    /// 标识当前窗格是否承载“多选后提取选中”快捷操作。
    /// 当前只在主工作区活动资产窗格中开放该入口。
    /// 避免把批量操作分散到其他只读承载区域。
    /// </summary>
    public bool SupportsBatchExtractionQuickAction
    {
        get => _supportsBatchExtractionQuickAction;
        set
        {
            if (SetProperty(ref _supportsBatchExtractionQuickAction, value))
            {
                NotifySelectionStateChanged();
            }
        }
    }

    /// <summary>
    /// 由页面级 ViewModel 回填当前忙碌状态，
    /// 用于控制列表内快捷操作按钮，避免重复触发批量命令。
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanBatchExtractSelectedAssets));
                OnPropertyChanged(nameof(CanToggleSearch));
            }
        }
    }

    /// <summary>
    /// 仅显示已修改资源的本地筛选开关。
    /// 该状态只影响当前窗格展示，不会反向修改工作区数据。
    /// </summary>
    public bool ShowModifiedOnly
    {
        get => _showModifiedOnly;
        set
        {
            if (SetProperty(ref _showModifiedOnly, value))
            {
                ApplyLocalFilters();
            }
        }
    }

    /// <summary>
    /// 仅显示可预览资源的本地筛选开关。
    /// 当前阶段先基于列表 DTO 中的摘要能力标记进行过滤。
    /// </summary>
    public bool ShowPreviewableOnly
    {
        get => _showPreviewableOnly;
        set
        {
            if (SetProperty(ref _showPreviewableOnly, value))
            {
                ApplyLocalFilters();
            }
        }
    }

    public ObservableCollection<AssetTypeOptionViewModel> AssetTypeOptions => _assetTypeFilter.Options;

    public ObservableCollection<AssetListEntryViewModel> Items => _assetList.Items;

    public ObservableCollection<AssetListEntryViewModel> SelectedAssets => _assetList.SelectedAssets;

    public ObservableCollection<SpineAssetGroupEntryViewModel> SpineGroupItems => _spineGroupList.Items;

    public ObservableCollection<SpineAssetGroupEntryViewModel> SelectedSpineGroups => _spineGroupList.SelectedSpineGroups;

    public bool IsReplacingAssetTypeOptions
    {
        get => _assetTypeFilter.IsReplacingOptions;
    }

    public AssetTypeOptionViewModel? SelectedAssetTypeOption
    {
        get => _assetTypeFilter.SelectedOption;
        set
        {
            SetSelectedAssetTypeOption(value, syncFilterKeyFromOption: true);
        }
    }

    public string? SelectedAssetTypeFilterKey
    {
        get => _assetTypeFilter.SelectedFilterKey;
        private set
        {
            if (_assetTypeFilter.SetSelectedFilterKey(value))
            {
                NotifyAssetTypeFilterSelectionChanged();
            }
        }
    }

    /// <summary>
    /// 当前窗格选中的资源类型筛选。
    /// 该值用于在标签页切换时保存和恢复列表过滤条件。
    /// </summary>
    public AssetType? SelectedAssetType =>
        _assetTypeFilter.SelectedAssetType;

    public string? SelectedAssetTypeDisplayNameFilter =>
        _assetTypeFilter.SelectedAssetTypeDisplayNameFilter;

    public AssetListEntryViewModel? SelectedAsset
    {
        get => _assetList.SelectedAsset;
        set
        {
            if (ReferenceEquals(_assetList.SelectedAsset, value))
            {
                return;
            }

            _assetList.SetSelectedAsset(value);
            SetAssetMultiSelectionMode(false);
            OnPropertyChanged();
            NotifySelectionStateChanged();
        }
    }

    public SpineAssetGroupEntryViewModel? SelectedSpineGroup
    {
        get => _spineGroupList.SelectedSpineGroup;
        set
        {
            if (ReferenceEquals(_spineGroupList.SelectedSpineGroup, value))
            {
                return;
            }

            _spineGroupList.SetSelectedSpineGroup(value);
            SetSpineGroupMultiSelectionMode(false);
            OnPropertyChanged();
            NotifySelectionStateChanged();
        }
    }

    /// <summary>
    /// 当前主选中资源的标识。
    /// 标签页切换后会尽量按这个标识恢复焦点资源。
    /// </summary>
    public AssetId? SelectedAssetId => SelectedAsset?.Item.AssetId;

    public int SelectedAssetCount => SelectedAssets.Count;

    public int SelectedSpineGroupCount => SelectedSpineGroups.Count;

    public int SelectedItemCount =>
        IsSpineGroupViewMode ? SelectedSpineGroupCount : SelectedAssetCount;

    public bool IsMultiSelectionMode =>
        IsSpineGroupViewMode ? _isSpineGroupMultiSelectionMode : _isAssetMultiSelectionMode;

    public bool HasSelectedItems => SelectedItemCount > 0;

    public bool HasMultipleSelection =>
        IsSpineGroupViewMode ? SelectedSpineGroupCount > 1 : SelectedAssetCount > 1;

    public bool CanBatchExtractSelectedAssets =>
        SupportsBatchExtractionQuickAction &&
        !IsBusy &&
        ((IsAssetViewMode && _isAssetMultiSelectionMode && SelectedAssetCount > 0) ||
         (IsSpineGroupViewMode && _isSpineGroupMultiSelectionMode && SelectedSpineGroupCount > 0));

    public bool CanToggleSearch => !IsBusy && CurrentSourceItemCount > 0;

    public bool HasCurrentAssetListSnapshot =>
        IsAssetViewMode && _assetList.SourceCount > 0;

    public bool HasCurrentSpineGroupListSnapshot =>
        IsSpineGroupViewMode && _spineGroupList.SourceCount > 0;

    public string SearchToggleGlyph => IsSearchExpanded ? "\uE6D3" : "\uEFD7";

    public Visibility AssetListVisibility => IsAssetViewMode && Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SpineGroupListVisibility => IsSpineGroupViewMode && SpineGroupItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => CurrentVisibleItemCount == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateLoadingVisibility =>
        IsListLoading && CurrentVisibleItemCount == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateIconVisibility =>
        !IsListLoading && CurrentVisibleItemCount == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string EmptyStateIconGlyph => IsSpineGroupViewMode ? "\uE8EB" : "\uE6E9";

    public Visibility AssetFilterVisibility => IsAssetViewMode ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MultiSelectionQuickActionsVisibility =>
        SupportsBatchExtractionQuickAction && IsMultiSelectionMode && CurrentVisibleItemCount > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    private int CurrentSourceItemCount =>
        IsSpineGroupViewMode ? _spineGroupList.SourceCount : _assetList.SourceCount;

    private int CurrentVisibleItemCount =>
        IsSpineGroupViewMode ? _spineGroupList.VisibleCount : _assetList.VisibleCount;

    /// <summary>
    /// 切换资产列表搜索框的展开状态。
    /// 收起时同步清空关键字，让列表快速回到完整浏览结果。
    /// </summary>
    public void ToggleSearch()
    {
        if (!CanToggleSearch)
        {
            return;
        }

        if (IsSearchExpanded)
        {
            SearchKeyword = string.Empty;
            IsSearchExpanded = false;
            return;
        }

        IsSearchExpanded = true;
    }

    /// <summary>
    /// 由视图层把 ListView 当前的多选结果同步回 ViewModel。
    /// 这样页面级 ViewModel 可以同时读取“焦点项”和“完整多选集”。
    /// </summary>
    public void UpdateSelection(
        IEnumerable<AssetListEntryViewModel> selectedAssets,
        AssetListEntryViewModel? primarySelectedAsset)
    {
        _assetList.UpdateSelection(selectedAssets, primarySelectedAsset);
        if (SelectedAssetCount > 1)
        {
            SetAssetMultiSelectionMode(true);
        }

        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
    }

    public void UpdateSpineGroupSelection(SpineAssetGroupEntryViewModel? primarySelectedGroup)
    {
        SelectedSpineGroup = primarySelectedGroup;
    }

    public void UpdateSpineGroupSelection(
        IEnumerable<SpineAssetGroupEntryViewModel> selectedSpineGroups,
        SpineAssetGroupEntryViewModel? primarySelectedGroup)
    {
        _spineGroupList.UpdateSelection(selectedSpineGroups, primarySelectedGroup);
        if (SelectedSpineGroupCount > 1)
        {
            SetSpineGroupMultiSelectionMode(true);
        }

        OnPropertyChanged(nameof(SelectedSpineGroup));
        NotifySelectionStateChanged();
    }

    /// <summary>
    /// 主动清空当前窗格的选中状态。
    /// 主要用于详情标签被关闭后同步回退列表焦点，避免旧选中项立即重新打开详情。
    /// </summary>
    public void ClearSelection()
    {
        _assetList.ClearSelection();
        SetAssetMultiSelectionMode(false);
        OnPropertyChanged(nameof(SelectedAsset));
        NotifySelectionStateChanged();
    }

    /// <summary>
    /// 恢复标签页上一次记录的列表浏览状态。
    /// 当前阶段只恢复搜索、类型筛选和本地展示筛选，不恢复完整多选集。
    /// </summary>
    public void RestoreViewState(
        string searchKeyword,
        string? selectedAssetTypeFilterKey,
        bool showModifiedOnly,
        bool showPreviewableOnly,
        AssetPaneViewMode viewMode = AssetPaneViewMode.Assets)
    {
        SearchKeyword = searchKeyword;
        IsSearchExpanded = !string.IsNullOrWhiteSpace(searchKeyword);
        ShowModifiedOnly = showModifiedOnly;
        ShowPreviewableOnly = showPreviewableOnly;
        ViewMode = viewMode;
        SelectedAssetTypeFilterKey = selectedAssetTypeFilterKey;
        SetSelectedAssetTypeOption(
            _assetTypeFilter.SelectOption(SelectedAssetTypeFilterKey) ??
            _assetTypeFilter.EnsureRestoredOption(SelectedAssetTypeFilterKey) ??
            AssetTypeOptions.FirstOrDefault(),
            syncFilterKeyFromOption: false);
    }

    private void NotifySelectionStateChanged()
    {
        _assetList.UpdateSelectionState();
        _spineGroupList.UpdateSelectionState();
        OnPropertyChanged(nameof(SelectedAssetCount));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(SelectedSpineGroupCount));
        OnPropertyChanged(nameof(SelectedItemCount));
        OnPropertyChanged(nameof(HasSelectedItems));
        OnPropertyChanged(nameof(IsMultiSelectionMode));
        OnPropertyChanged(nameof(CanBatchExtractSelectedAssets));
        OnPropertyChanged(nameof(MultiSelectionQuickActionsVisibility));
    }

    private void NotifyViewModeChanged()
    {
        OnPropertyChanged(nameof(IsAssetViewMode));
        OnPropertyChanged(nameof(IsSpineGroupViewMode));
        OnPropertyChanged(nameof(AssetFilterVisibility));
        OnPropertyChanged(nameof(EmptyStateIconGlyph));
        OnPropertyChanged(nameof(SelectedItemCount));
        OnPropertyChanged(nameof(HasSelectedItems));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(IsMultiSelectionMode));
        OnPropertyChanged(nameof(CanBatchExtractSelectedAssets));
        OnPropertyChanged(nameof(CanToggleSearch));
        OnPropertyChanged(nameof(MultiSelectionQuickActionsVisibility));
        NotifyListStateChanged();
    }

    // 这里必须按引用更新，避免重建选项集合后 SelectedItem 仍指向旧实例而导致下拉空白。
    private void SetSelectedAssetTypeOption(
        AssetTypeOptionViewModel? option,
        bool syncFilterKeyFromOption)
    {
        var previousOption = _assetTypeFilter.SelectedOption;
        var previousFilterKey = _assetTypeFilter.SelectedFilterKey;

        _assetTypeFilter.SetSelectedOption(option, syncFilterKeyFromOption);

        var optionChanged = !ReferenceEquals(previousOption, _assetTypeFilter.SelectedOption);
        var filterKeyChanged = !string.Equals(
            previousFilterKey,
            _assetTypeFilter.SelectedFilterKey,
            StringComparison.Ordinal);

        if (!optionChanged && !filterKeyChanged)
        {
            return;
        }

        if (optionChanged)
        {
            OnPropertyChanged(nameof(SelectedAssetTypeOption));
        }

        if (filterKeyChanged)
        {
            NotifyAssetTypeFilterSelectionChanged();
        }
    }

    private void NotifyAssetTypeFilterSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedAssetTypeFilterKey));
        OnPropertyChanged(nameof(SelectedAssetType));
        OnPropertyChanged(nameof(SelectedAssetTypeDisplayNameFilter));
    }
}
