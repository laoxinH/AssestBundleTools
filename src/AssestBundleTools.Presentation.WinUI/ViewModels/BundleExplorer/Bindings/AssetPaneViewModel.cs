using System.Collections.ObjectModel;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示中间区域一个资产窗格的全部展示状态。
/// </summary>
public sealed class AssetPaneViewModel : ObservableObject
{
    /// <summary>
    /// 保留当前窗格一次查询得到的完整结果集。
    /// 这样“仅已修改 / 仅可预览”这类纯展示筛选可以在 UI 层本地完成，
    /// 无需每次切换都重新请求应用层数据。
    /// </summary>
    private readonly List<AssetListEntryViewModel> _sourceItems = [];

    private string _paneTitle = string.Empty;
    private string _currentBundleDisplayName = string.Empty;
    private string _searchKeyword = string.Empty;
    private string _emptyStateText = string.Empty;
    private bool _isSearchExpanded;
    private bool _showModifiedOnly;
    private bool _showPreviewableOnly;
    private bool _supportsBatchExtractionQuickAction;
    private bool _isBusy;
    private bool _isReplacingAssetTypeOptions;
    private string? _selectedAssetTypeFilterKey;
    private AssetTypeOptionViewModel? _selectedAssetTypeOption;
    private AssetListEntryViewModel? _selectedAsset;
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

    public ObservableCollection<AssetTypeOptionViewModel> AssetTypeOptions { get; } = new();

    public ObservableCollection<AssetListEntryViewModel> Items { get; } = new();

    public ObservableCollection<AssetListEntryViewModel> SelectedAssets { get; } = new();

    public bool IsReplacingAssetTypeOptions
    {
        get => _isReplacingAssetTypeOptions;
        private set => SetProperty(ref _isReplacingAssetTypeOptions, value);
    }

    public AssetTypeOptionViewModel? SelectedAssetTypeOption
    {
        get => _selectedAssetTypeOption;
        set
        {
            SetSelectedAssetTypeOption(value, syncFilterKeyFromOption: true);
        }
    }

    public string? SelectedAssetTypeFilterKey
    {
        get => _selectedAssetTypeFilterKey;
        private set
        {
            var normalizedValue = NormalizeAssetTypeFilterKey(value);
            if (SetProperty(ref _selectedAssetTypeFilterKey, normalizedValue))
            {
                OnPropertyChanged(nameof(SelectedAssetType));
                OnPropertyChanged(nameof(SelectedAssetTypeDisplayNameFilter));
            }
        }
    }

    /// <summary>
    /// 当前窗格选中的资源类型筛选。
    /// 该值用于在标签页切换时保存和恢复列表过滤条件。
    /// </summary>
    public AssetType? SelectedAssetType =>
        AssetTypeOptionViewModel.TryParseFilterKey(
            SelectedAssetTypeFilterKey,
            out var assetType,
            out _)
            ? assetType
            : null;

    public string? SelectedAssetTypeDisplayNameFilter =>
        AssetTypeOptionViewModel.TryParseFilterKey(
            SelectedAssetTypeFilterKey,
            out _,
            out var assetTypeDisplayNameFilter)
            ? assetTypeDisplayNameFilter
            : null;

    public AssetListEntryViewModel? SelectedAsset
    {
        get => _selectedAsset;
        set => SetProperty(ref _selectedAsset, value);
    }

    /// <summary>
    /// 当前主选中资源的标识。
    /// 标签页切换后会尽量按这个标识恢复焦点资源。
    /// </summary>
    public AssetId? SelectedAssetId => SelectedAsset?.Item.AssetId;

    public int SelectedAssetCount => SelectedAssets.Count;

    public bool HasMultipleSelection => SelectedAssetCount > 1;

    public bool CanBatchExtractSelectedAssets =>
        SupportsBatchExtractionQuickAction &&
        !IsBusy &&
        HasMultipleSelection;

    public bool CanToggleSearch => !IsBusy && _sourceItems.Count > 0;

    public string SearchToggleGlyph => IsSearchExpanded ? "\uE711" : "\uE721";

    public Visibility ListVisibility => Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MultiSelectionQuickActionsVisibility =>
        SupportsBatchExtractionQuickAction && HasMultipleSelection
            ? Visibility.Visible
            : Visibility.Collapsed;

    public void ConfigurePane(PaneId paneId, string paneTitle, string emptyStateText)
    {
        PaneId = paneId;
        PaneTitle = paneTitle;
        EmptyStateText = emptyStateText;
    }

    public void ReplaceAssetTypeOptions(IEnumerable<AssetTypeOptionViewModel> options)
    {
        var selectedFilterKey = SelectedAssetTypeFilterKey;
        var materializedOptions = options.ToArray();
        if (AreAssetTypeOptionsEquivalent(materializedOptions))
        {
            SelectedAssetTypeOption = SelectAssetTypeOption(selectedFilterKey) ??
                                      AssetTypeOptions.FirstOrDefault();
            return;
        }

        IsReplacingAssetTypeOptions = true;
        try
        {
            AssetTypeOptions.Clear();
            foreach (var option in materializedOptions)
            {
                AssetTypeOptions.Add(option);
            }

            var selectedOption = SelectAssetTypeOption(selectedFilterKey) ??
                                 EnsureRestoredAssetTypeOption(selectedFilterKey) ??
                                 AssetTypeOptions.FirstOrDefault();
            SetSelectedAssetTypeOption(selectedOption, syncFilterKeyFromOption: false);
            SelectedAssetTypeFilterKey = selectedOption?.FilterKey;
        }
        finally
        {
            IsReplacingAssetTypeOptions = false;
        }
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
        _sourceItems.Clear();
        _sourceItems.AddRange(items);
        OnPropertyChanged(nameof(CanToggleSearch));
        ApplyLocalFilters(preferredSelectedAssetId);
    }

    public void ClearItems(string emptyStateText)
    {
        CurrentBundleDisplayName = string.Empty;
        EmptyStateText = emptyStateText;
        _sourceItems.Clear();
        Items.Clear();
        SelectedAssets.Clear();
        SelectedAsset = null;
        IsSearchExpanded = false;
        OnPropertyChanged(nameof(CanToggleSearch));
        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }

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
        SelectedAssets.Clear();
        foreach (var item in selectedAssets)
        {
            SelectedAssets.Add(item);
        }

        SelectedAsset = primarySelectedAsset;
        NotifySelectionStateChanged();
    }

    /// <summary>
    /// 主动清空当前窗格的选中状态。
    /// 主要用于详情标签被关闭后同步回退列表焦点，避免旧选中项立即重新打开详情。
    /// </summary>
    public void ClearSelection()
    {
        SelectedAssets.Clear();
        SelectedAsset = null;
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
        bool showPreviewableOnly)
    {
        SearchKeyword = searchKeyword;
        IsSearchExpanded = !string.IsNullOrWhiteSpace(searchKeyword);
        ShowModifiedOnly = showModifiedOnly;
        ShowPreviewableOnly = showPreviewableOnly;
        SelectedAssetTypeFilterKey = selectedAssetTypeFilterKey;
        SetSelectedAssetTypeOption(
            SelectAssetTypeOption(SelectedAssetTypeFilterKey) ??
            EnsureRestoredAssetTypeOption(SelectedAssetTypeFilterKey) ??
            AssetTypeOptions.FirstOrDefault(),
            syncFilterKeyFromOption: false);
    }

    /// <summary>
    /// 按资源标识恢复主选中项。
    /// 如果目标资源已经不在当前筛选结果中，则保持空选中。
    /// 避免程序自动把焦点跳到筛选结果第一项。
    /// </summary>
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
        NotifySelectionStateChanged();
        return true;
    }

    public void NotifyListStateChanged()
    {
        OnPropertyChanged(nameof(ListVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    /// <summary>
    /// 将应用层返回的完整结果集重新映射到当前窗格的展示集合。
    /// 这里会尽量保留用户原先的选中项；
    /// 如果旧选中项已经不在当前结果里，则保持空选中，不再自动回退到第一项。
    /// </summary>
    private void ApplyLocalFilters(AssetId? preferredSelectedAssetId = null)
    {
        var desiredSelectedAssetId = preferredSelectedAssetId ?? SelectedAsset?.Item.AssetId;

        var filteredItems = _sourceItems
            .Where(item => !ShowModifiedOnly || item.Item.IsModified)
            .Where(item => !ShowPreviewableOnly || item.Item.HasPreview)
            .ToArray();

        Items.Clear();
        foreach (var item in filteredItems)
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

        NotifySelectionStateChanged();
        NotifyListStateChanged();
    }

    private void NotifySelectionStateChanged()
    {
        UpdateItemSelectionState();
        OnPropertyChanged(nameof(SelectedAssetCount));
        OnPropertyChanged(nameof(HasMultipleSelection));
        OnPropertyChanged(nameof(CanBatchExtractSelectedAssets));
        OnPropertyChanged(nameof(MultiSelectionQuickActionsVisibility));
    }

    /// <summary>
    /// 列表行的视觉选中态直接从真实选择集合派生。
    /// 避免模板自己猜选中状态，后续扩展多选视觉时也更容易统一维护。
    /// </summary>
    private void UpdateItemSelectionState()
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

    private AssetTypeOptionViewModel? SelectAssetTypeOption(string? filterKey)
    {
        return string.IsNullOrWhiteSpace(filterKey)
            ? AssetTypeOptions.FirstOrDefault(option => option.AssetType is null)
            : AssetTypeOptions.FirstOrDefault(option => option.MatchesFilterKey(filterKey));
    }

    private bool AreAssetTypeOptionsEquivalent(IReadOnlyList<AssetTypeOptionViewModel> options)
    {
        if (AssetTypeOptions.Count != options.Count)
        {
            return false;
        }

        for (var index = 0; index < options.Count; index++)
        {
            var current = AssetTypeOptions[index];
            var next = options[index];
            if (!string.Equals(current.FilterKey, next.FilterKey, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(current.DisplayName, next.DisplayName, StringComparison.Ordinal) ||
                current.Count != next.Count)
            {
                return false;
            }
        }

        return true;
    }

    private AssetTypeOptionViewModel? EnsureRestoredAssetTypeOption(string? filterKey)
    {
        var restoredOption = AssetTypeOptionViewModel.CreateFromFilterKey(filterKey);
        if (restoredOption.AssetType is null)
        {
            return null;
        }

        AssetTypeOptions.Add(restoredOption);
        return restoredOption;
    }

    // 这里必须按引用更新，避免重建选项集合后 SelectedItem 仍指向旧实例而导致下拉空白。
    private void SetSelectedAssetTypeOption(
        AssetTypeOptionViewModel? option,
        bool syncFilterKeyFromOption)
    {
        if (ReferenceEquals(_selectedAssetTypeOption, option))
        {
            if (syncFilterKeyFromOption && !IsReplacingAssetTypeOptions)
            {
                SelectedAssetTypeFilterKey = option?.FilterKey;
            }

            return;
        }

        _selectedAssetTypeOption = option;
        OnPropertyChanged(nameof(SelectedAssetTypeOption));

        if (syncFilterKeyFromOption && !IsReplacingAssetTypeOptions)
        {
            SelectedAssetTypeFilterKey = option?.FilterKey;
            return;
        }

        OnPropertyChanged(nameof(SelectedAssetType));
        OnPropertyChanged(nameof(SelectedAssetTypeDisplayNameFilter));
    }

    private static string? NormalizeAssetTypeFilterKey(string? filterKey) =>
        string.IsNullOrWhiteSpace(filterKey)
            ? null
            : filterKey.Trim();
}
