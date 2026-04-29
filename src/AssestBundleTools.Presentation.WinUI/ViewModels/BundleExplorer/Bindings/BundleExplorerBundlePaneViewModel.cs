using System.Collections.ObjectModel;
using System.Globalization;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 维护资源包浏览左侧窗格的显示状态与检索上下文。
/// </summary>
/// <remarks>
/// 这里只负责“资源包列表如何展示、如何筛选、当前高亮的是谁”。
/// 不承载工作区加载、资源详情读取或预览编排逻辑，避免主页面 ViewModel 继续膨胀。
/// </remarks>
public sealed class BundleExplorerBundlePaneViewModel : ObservableObject, IBundleExplorerBundlePaneHost
{
    private readonly ILocalizationService _localizationService;
    private readonly List<BundleGroupViewModel> _allBundleGroups = new();
    private readonly Dictionary<string, BundleNodeViewModel> _bundleLookup = new(StringComparer.Ordinal);
    private bool _hasWorkspace;
    private bool _isBusy;
    private bool _isBundlePaneCollapsed;
    private bool _isBundleSearchExpanded;
    private string _bundleSearchKeyword = string.Empty;
    private BundleNodeViewModel? _selectedBundleNode;

    public BundleExplorerBundlePaneViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    internal event EventHandler<BundleExplorerPageStateChangedEventArgs<BundleExplorerBundlePanePageStateChange>>? PageStateChanged;

    /// <summary>
    /// 当左侧搜索关键字变化时通知上层持久化浏览状态。
    /// </summary>
    public event EventHandler? BrowserStateChanged;

    public ObservableCollection<BundleGroupViewModel> BundleGroups { get; } = new();

    public bool IsBundlePaneCollapsed
    {
        get => _isBundlePaneCollapsed;
        private set
        {
            if (SetProperty(ref _isBundlePaneCollapsed, value))
            {
                OnPropertyChanged(nameof(BundlePaneCollapseToolTip));
                NotifyPageStateChanged(BundleExplorerBundlePanePageStateChange.PaneChrome);
            }
        }
    }

    public bool IsBundleSearchExpanded
    {
        get => _isBundleSearchExpanded;
        private set
        {
            if (SetProperty(ref _isBundleSearchExpanded, value))
            {
                OnPropertyChanged(nameof(BundleSearchToggleGlyph));
                OnPropertyChanged(nameof(BundleSearchToggleToolTip));
                NotifyPageStateChanged(BundleExplorerBundlePanePageStateChange.SearchChrome);
            }
        }
    }

    public string BundleSearchKeyword
    {
        get => _bundleSearchKeyword;
        set
        {
            var normalizedKeyword = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _bundleSearchKeyword, normalizedKeyword))
            {
                OnPropertyChanged(nameof(BundleSearchEmptyStateText));
                ApplyBundleGroupFilter();
                BrowserStateChanged?.Invoke(this, EventArgs.Empty);
                NotifyPageStateChanged(
                    BundleExplorerBundlePanePageStateChange.SearchInput |
                    BundleExplorerBundlePanePageStateChange.CollectionPresentation);
            }
        }
    }

    public bool HasAnyBundles => _allBundleGroups.Count > 0;

    public bool CanToggleBundleSearch => _hasWorkspace && !_isBusy;

    public bool CanToggleBundlePaneCollapse => _hasWorkspace && !_isBusy;

    public string BundleSearchToggleGlyph => IsBundleSearchExpanded ? "\uE711" : "\uE721";

    public string BundleSearchToggleToolTip => IsBundleSearchExpanded
        ? GetString(ResourceKeys.BundleExplorerPageBundleSearchToggleButtonCloseToolTip)
        : GetString(ResourceKeys.BundleExplorerPageBundleSearchToggleButtonOpenToolTip);

    public string BundlePaneCollapseToolTip => IsBundlePaneCollapsed
        ? GetString(ResourceKeys.BundleExplorerPageBundlePaneCollapseButtonExpandToolTip)
        : GetString(ResourceKeys.BundleExplorerPageBundlePaneCollapseButtonCollapseToolTip);

    public string BundleSearchEmptyStateText =>
        _localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerBundleSearchEmptyStateFormat,
            string.IsNullOrWhiteSpace(BundleSearchKeyword)
                ? GetString(ResourceKeys.BundleExplorerSelectionNone)
                : BundleSearchKeyword.Trim());

    /// <summary>
    /// 左侧资源包区域的空状态文案。
    /// 未打开工作区时提示先打开工作区；已打开但没有资源包时提示当前工作区为空。
    /// </summary>
    public string BundlePaneEmptyStateText => _hasWorkspace
        ? GetString(ResourceKeys.BundleExplorerEmptyWorkspace)
        : GetString(ResourceKeys.BundleExplorerNoWorkspace);

    public Visibility BundlePaneEmptyStateVisibility =>
        !_hasWorkspace || (_hasWorkspace && _allBundleGroups.Count == 0)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility BundleListVisibility => _hasWorkspace && BundleGroups.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility BundleSearchEmptyStateVisibility =>
        _hasWorkspace &&
        _allBundleGroups.Count > 0 &&
        BundleGroups.Count == 0 &&
        !string.IsNullOrWhiteSpace(BundleSearchKeyword)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string CollapsedBundlePaneBundleCountText => GetVisibleBundleCount().ToString(CultureInfo.CurrentCulture);

    public string CollapsedBundlePaneGroupCountText => BundleGroups.Count.ToString(CultureInfo.CurrentCulture);

    public string CollapsedBundlePaneSelectedBundleShortText => BuildCollapsedBundlePaneSelectedBundleShortText();

    public string CollapsedBundlePaneSelectedBundleToolTip =>
        _selectedBundleNode?.DisplayName ?? GetString(ResourceKeys.BundleExplorerSelectionNone);

    public void UpdateInteractionState(bool hasWorkspace, bool isBusy)
    {
        var workspaceChanged = _hasWorkspace != hasWorkspace;
        var busyChanged = _isBusy != isBusy;
        if (!workspaceChanged && !busyChanged)
        {
            return;
        }

        _hasWorkspace = hasWorkspace;
        _isBusy = isBusy;

        if (!hasWorkspace)
        {
            ResetSearchAndLayoutState();
        }

        NotifyContextDependentStateChanged();
        NotifyPageStateChanged(
            BundleExplorerBundlePanePageStateChange.InteractionState |
            BundleExplorerBundlePanePageStateChange.CollectionPresentation);
    }

    /// <summary>
    /// 从工作区浏览快照恢复搜索关键字与展开态。
    /// 这里显式同步两者，避免不同工作区之间残留旧的展开状态。
    /// </summary>
    public void RestoreSearchState(string? keyword)
    {
        BundleSearchKeyword = keyword?.Trim() ?? string.Empty;
        IsBundleSearchExpanded = !string.IsNullOrWhiteSpace(BundleSearchKeyword);
    }

    /// <summary>
    /// 清理资源包树与选择状态，供工作区切换或重置时复用。
    /// </summary>
    public void Clear()
    {
        _allBundleGroups.Clear();
        _bundleLookup.Clear();
        BundleGroups.Clear();
        SetSelectedBundle(null);
        NotifyBundleCollectionStateChanged();
        NotifyPageStateChanged(
            BundleExplorerBundlePanePageStateChange.CollectionPresentation |
            BundleExplorerBundlePanePageStateChange.SelectionSummary);
    }

    public void RebuildBundleGroups(IEnumerable<BundleTreeNodeDto> bundleTree)
    {
        ArgumentNullException.ThrowIfNull(bundleTree);

        _allBundleGroups.Clear();
        _bundleLookup.Clear();

        foreach (var groupNode in bundleTree.Where(static node => node.IsVirtualFolder))
        {
            var bundles = groupNode.Children
                .Where(static child => !child.IsVirtualFolder)
                .Select(child =>
                {
                    var node = new BundleNodeViewModel(
                        child.BundleId,
                        child.DisplayName,
                        child.LogicalPath,
                        child.SourcePath,
                        child.AssetCount);

                    _bundleLookup[node.BundleId.Value] = node;
                    return node;
                })
                .ToArray();

            if (bundles.Length > 0)
            {
                _allBundleGroups.Add(new BundleGroupViewModel(groupNode.DisplayName, bundles));
            }
        }

        ApplyBundleGroupFilter();
        NotifyPageStateChanged(
            BundleExplorerBundlePanePageStateChange.CollectionPresentation |
            BundleExplorerBundlePanePageStateChange.SelectionSummary);
    }

    public void ToggleBundleSearch()
    {
        if (!CanToggleBundleSearch)
        {
            return;
        }

        if (IsBundleSearchExpanded)
        {
            BundleSearchKeyword = string.Empty;
            IsBundleSearchExpanded = false;
            return;
        }

        IsBundleSearchExpanded = true;
    }

    public void ToggleBundlePaneCollapsed()
    {
        if (!CanToggleBundlePaneCollapse)
        {
            return;
        }

        if (!IsBundlePaneCollapsed)
        {
            BundleSearchKeyword = string.Empty;
            IsBundleSearchExpanded = false;
        }

        IsBundlePaneCollapsed = !IsBundlePaneCollapsed;
    }

    public BundleNodeViewModel? FindBundleNode(BundleId bundleId) =>
        _bundleLookup.TryGetValue(bundleId.Value, out var bundleNode)
            ? bundleNode
            : null;

    public BundleNodeViewModel? FindBundleNodeByDisplayName(string displayName) =>
        _allBundleGroups
            .SelectMany(static group => group.Bundles)
            .FirstOrDefault(bundle => string.Equals(bundle.DisplayName, displayName, StringComparison.OrdinalIgnoreCase));

    public BundleNodeViewModel? FindBundleNodeByLogicalPath(string logicalPath) =>
        _allBundleGroups
            .SelectMany(static group => group.Bundles)
            .FirstOrDefault(bundle => string.Equals(bundle.LogicalPath, logicalPath, StringComparison.OrdinalIgnoreCase));

    public BundleNodeViewModel? GetFirstVisibleBundle() =>
        BundleGroups.SelectMany(static group => group.Bundles).FirstOrDefault();

    public void SetSelectedBundle(BundleNodeViewModel? bundleNode)
    {
        if (ReferenceEquals(_selectedBundleNode, bundleNode))
        {
            return;
        }

        if (_selectedBundleNode is not null)
        {
            _selectedBundleNode.IsSelected = false;
        }

        _selectedBundleNode = bundleNode;
        if (_selectedBundleNode is not null)
        {
            _selectedBundleNode.IsSelected = true;
        }

        NotifyCollapsedBundlePaneSummaryChanged();
        NotifyPageStateChanged(BundleExplorerBundlePanePageStateChange.SelectionSummary);
    }

    public string ResolveBundleDisplayName(BundleId bundleId) =>
        _bundleLookup.TryGetValue(bundleId.Value, out var bundleNode)
            ? bundleNode.DisplayName
            : bundleId.Value;

    public string ResolvePrimaryPaneEmptyStateText(
        string noWorkspaceText,
        string emptyWorkspaceText,
        string noOpenTabsText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noWorkspaceText);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyWorkspaceText);
        ArgumentException.ThrowIfNullOrWhiteSpace(noOpenTabsText);

        if (!_hasWorkspace)
        {
            return noWorkspaceText;
        }

        return _allBundleGroups.Count == 0
            ? emptyWorkspaceText
            : noOpenTabsText;
    }

    private void ResetSearchAndLayoutState()
    {
        if (!string.IsNullOrWhiteSpace(BundleSearchKeyword))
        {
            BundleSearchKeyword = string.Empty;
        }

        IsBundleSearchExpanded = false;
        IsBundlePaneCollapsed = false;
    }

    private void ApplyBundleGroupFilter()
    {
        var searchKeyword = NormalizeSearchKeyword(BundleSearchKeyword);
        BundleGroups.Clear();

        foreach (var group in _allBundleGroups)
        {
            var visibleBundles = GroupMatchesSearch(group, searchKeyword)
                ? group.Bundles.ToArray()
                : group.Bundles
                    .Where(bundle => BundleMatchesSearch(bundle, searchKeyword))
                    .ToArray();

            if (visibleBundles.Length > 0)
            {
                BundleGroups.Add(new BundleGroupViewModel(group.DisplayName, visibleBundles));
            }
        }

        NotifyBundleCollectionStateChanged();
    }

    private void NotifyContextDependentStateChanged()
    {
        OnPropertyChanged(nameof(CanToggleBundleSearch));
        OnPropertyChanged(nameof(CanToggleBundlePaneCollapse));
        OnPropertyChanged(nameof(BundlePaneEmptyStateText));
        NotifyBundleCollectionStateChanged();
    }

    private void NotifyBundleCollectionStateChanged()
    {
        OnPropertyChanged(nameof(BundlePaneEmptyStateVisibility));
        OnPropertyChanged(nameof(BundleListVisibility));
        OnPropertyChanged(nameof(BundleSearchEmptyStateVisibility));
        NotifyCollapsedBundlePaneSummaryChanged();
    }

    /// <summary>
    /// 收缩窄条依赖的都是派生摘要字段，因此在集合或选中项变化时集中刷新即可。
    /// </summary>
    private void NotifyCollapsedBundlePaneSummaryChanged()
    {
        OnPropertyChanged(nameof(CollapsedBundlePaneBundleCountText));
        OnPropertyChanged(nameof(CollapsedBundlePaneGroupCountText));
        OnPropertyChanged(nameof(CollapsedBundlePaneSelectedBundleShortText));
        OnPropertyChanged(nameof(CollapsedBundlePaneSelectedBundleToolTip));
    }

    /// <summary>
    /// 为收缩态窄条生成简短缩写，避免长文件名撑坏布局，同时尽量保留辨识度。
    /// </summary>
    private string BuildCollapsedBundlePaneSelectedBundleShortText()
    {
        if (string.IsNullOrWhiteSpace(_selectedBundleNode?.DisplayName))
        {
            return GetString(ResourceKeys.BundleExplorerCollapsedPaneSelectedBundleShortNone);
        }

        var displayName = _selectedBundleNode.DisplayName.Trim();
        var normalizedDisplayName = displayName.EndsWith(".ab", StringComparison.OrdinalIgnoreCase)
            ? displayName[..^3]
            : displayName;

        var compactText = new string(normalizedDisplayName
            .Where(static character => !char.IsWhiteSpace(character) && character is not '_' and not '-')
            .ToArray());

        if (string.IsNullOrWhiteSpace(compactText))
        {
            return GetString(ResourceKeys.BundleExplorerCollapsedPaneSelectedBundleShortNone);
        }

        var shortText = compactText.Length > 2
            ? compactText[..2]
            : compactText;

        return shortText.ToUpper(CultureInfo.CurrentCulture);
    }

    private static string? NormalizeSearchKeyword(string? searchKeyword)
    {
        if (string.IsNullOrWhiteSpace(searchKeyword))
        {
            return null;
        }

        return searchKeyword.Trim();
    }

    private static bool GroupMatchesSearch(BundleGroupViewModel group, string? searchKeyword) =>
        searchKeyword is not null &&
        group.DisplayName.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase);

    private static bool BundleMatchesSearch(BundleNodeViewModel bundle, string? searchKeyword)
    {
        if (searchKeyword is null)
        {
            return true;
        }

        return bundle.DisplayName.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase) ||
               bundle.LogicalPath.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase) ||
               bundle.BundleId.Value.Contains(searchKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private int GetVisibleBundleCount() => BundleGroups.Sum(static group => group.Bundles.Count);

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private void NotifyPageStateChanged(BundleExplorerBundlePanePageStateChange changeFlags)
    {
        if (changeFlags == BundleExplorerBundlePanePageStateChange.None)
        {
            return;
        }

        PageStateChanged?.Invoke(this, new BundleExplorerPageStateChangedEventArgs<BundleExplorerBundlePanePageStateChange>(changeFlags));
    }
}

