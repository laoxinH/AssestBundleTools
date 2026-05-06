using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示主工作区中一个已打开的资源包标签页。
/// </summary>
/// <remarks>
/// 标签页只保存“当前资源包 + 当前列表浏览状态”这些页面级瞬时信息。
/// 不直接承载真正的资源数据和替换结果，避免把工作区事实来源重新拆散。
/// </remarks>
public sealed class BundleDocumentTabViewModel : ObservableObject
{
    private BundleId _bundleId;
    private string _displayName;
    private string _logicalPath;
    private string _searchKeyword = string.Empty;
    private string? _selectedAssetTypeFilterKey;
    private string? _selectedAssetId;
    private bool _showModifiedOnly;
    private bool _showPreviewableOnly;
    private bool _isPreview;
    private AssetDocumentTabViewModel? _activeAssetTab;

    public BundleDocumentTabViewModel(
        BundleId bundleId,
        string displayName,
        string logicalPath,
        bool isPreview)
    {
        _bundleId = bundleId;
        _displayName = displayName;
        _logicalPath = logicalPath;
        _isPreview = isPreview;
    }

    public BundleId BundleId
    {
        get => _bundleId;
        private set => SetProperty(ref _bundleId, value);
    }

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string LogicalPath
    {
        get => _logicalPath;
        private set => SetProperty(ref _logicalPath, value);
    }

    public string SearchKeyword
    {
        get => _searchKeyword;
        private set => SetProperty(ref _searchKeyword, value);
    }

    public string? SelectedAssetTypeFilterKey
    {
        get => _selectedAssetTypeFilterKey;
        private set
        {
            if (SetProperty(ref _selectedAssetTypeFilterKey, value))
            {
                OnPropertyChanged(nameof(SelectedAssetType));
            }
        }
    }

    public AssetType? SelectedAssetType =>
        AssetTypeOptionViewModel.TryParseFilterKey(
            SelectedAssetTypeFilterKey,
            out var assetType,
            out _)
            ? assetType
            : null;

    public string? SelectedAssetId
    {
        get => _selectedAssetId;
        private set => SetProperty(ref _selectedAssetId, value);
    }

    public bool ShowModifiedOnly
    {
        get => _showModifiedOnly;
        private set => SetProperty(ref _showModifiedOnly, value);
    }

    public bool ShowPreviewableOnly
    {
        get => _showPreviewableOnly;
        private set => SetProperty(ref _showPreviewableOnly, value);
    }

    /// <summary>
    /// 预览标签页当前主要保留给初始化或后续恢复流程使用。
    /// 用户从左侧资源包列表打开的标签页默认都会固定下来，不再走预览复用主路径。
    /// </summary>
    public bool IsPreview
    {
        get => _isPreview;
        private set => SetProperty(ref _isPreview, value);
    }

    /// <summary>
    /// 右侧资产详情标签页属于当前资源包标签页的页面级子状态。
    /// 这样在切换资源包标签时，可以完整保留其对应的资产详情浏览上下文。
    /// </summary>
    public ObservableCollection<AssetDocumentTabViewModel> AssetTabs { get; } = new();

    public AssetDocumentTabViewModel? ActiveAssetTab
    {
        get => _activeAssetTab;
        private set => SetProperty(ref _activeAssetTab, value);
    }

    public void CaptureFromPane(AssetPaneViewModel pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        SearchKeyword = pane.SearchKeyword;
        SelectedAssetTypeFilterKey = pane.SelectedAssetTypeFilterKey;
        SelectedAssetId = pane.SelectedAssetId?.Value;
        ShowModifiedOnly = pane.ShowModifiedOnly;
        ShowPreviewableOnly = pane.ShowPreviewableOnly;
    }

    /// <summary>
    /// 从工作区元数据恢复当前标签页的浏览状态。
    /// 这样应用重启后仍能尽量回到上次浏览上下文。
    /// </summary>
    public void RestoreViewState(
        string searchKeyword,
        string? selectedAssetTypeFilterKey,
        string? selectedAssetId,
        bool showModifiedOnly,
        bool showPreviewableOnly)
    {
        SearchKeyword = searchKeyword;
        SelectedAssetTypeFilterKey = selectedAssetTypeFilterKey;
        SelectedAssetId = selectedAssetId;
        ShowModifiedOnly = showModifiedOnly;
        ShowPreviewableOnly = showPreviewableOnly;
    }

    public void ApplyToPane(AssetPaneViewModel pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        pane.RestoreViewState(
            searchKeyword: SearchKeyword,
            selectedAssetTypeFilterKey: SelectedAssetTypeFilterKey,
            showModifiedOnly: ShowModifiedOnly,
            showPreviewableOnly: ShowPreviewableOnly);
    }

    /// <summary>
    /// 当系统仍需要复用预览态标签页时，
    /// 可以通过这里把该标签页重定向到新的资源包。
    /// </summary>
    public void RebindBundle(
        BundleId bundleId,
        string displayName,
        string logicalPath,
        bool isPreview)
    {
        BundleId = bundleId;
        DisplayName = displayName;
        LogicalPath = logicalPath;
        SearchKeyword = string.Empty;
        SelectedAssetTypeFilterKey = null;
        SelectedAssetId = null;
        ShowModifiedOnly = false;
        ShowPreviewableOnly = false;
        IsPreview = isPreview;
        ClearAssetTabs();
    }

    public void PromoteToPinnedTab()
    {
        if (!IsPreview)
        {
            return;
        }

        IsPreview = false;
    }

    public bool TryGetAssetTab(
        AssetId assetId,
        [NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab)
    {
        assetTab = AssetTabs.FirstOrDefault(tab => tab.AssetId.Equals(assetId));
        return assetTab is not null;
    }

    public void AddAssetTab(AssetDocumentTabViewModel assetTab, bool activate)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!AssetTabs.Contains(assetTab))
        {
            AssetTabs.Add(assetTab);
        }

        if (activate)
        {
            SetActiveAssetTab(assetTab);
        }
    }

    public void SetActiveAssetTab(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is not null && !AssetTabs.Contains(assetTab))
        {
            AssetTabs.Add(assetTab);
        }

        var previousActiveAssetTab = ActiveAssetTab;
        ActiveAssetTab = assetTab;
        if (!ReferenceEquals(previousActiveAssetTab, ActiveAssetTab))
        {
            previousActiveAssetTab?.CancelPreviewRequest();
        }
    }

    public void RestoreAssetTabs(
        IEnumerable<AssetDocumentTabViewModel> assetTabs,
        string? activeAssetTabId)
    {
        ArgumentNullException.ThrowIfNull(assetTabs);

        CancelAllAssetPreviewRequests();
        AssetTabs.Clear();

        AssetDocumentTabViewModel? activeAssetTab = null;
        foreach (var assetTab in assetTabs)
        {
            AssetTabs.Add(assetTab);
            if (activeAssetTab is null &&
                string.Equals(assetTab.DocumentId, activeAssetTabId, StringComparison.Ordinal))
            {
                activeAssetTab = assetTab;
            }
        }

        ActiveAssetTab = activeAssetTab ?? AssetTabs.FirstOrDefault();
    }

    /// <summary>
    /// 资产文档标签的展示顺序属于当前资源包标签页的页面级状态。
    /// 拖拽重排后在这里统一回写到底层集合，便于后续随工作区元数据一起恢复。
    /// </summary>
    public bool ReorderAssetTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedAssetTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedAssetTabs);

        return ObservableCollectionOrderHelper.Reorder(AssetTabs, orderedAssetTabs);
    }

    public void RemoveAssetTab(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!AssetTabs.Remove(assetTab))
        {
            return;
        }

        assetTab.CancelPreviewRequest();
        if (ReferenceEquals(ActiveAssetTab, assetTab))
        {
            ActiveAssetTab = AssetTabs.FirstOrDefault();
        }
    }

    /// <summary>
    /// 将资产文档标签移交给独立资源编辑窗口时使用。
    /// 移交过程不取消目标标签自身的预览请求，页面级预览状态会跟随标签移动。
    /// </summary>
    public bool DetachAssetTab(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!AssetTabs.Remove(assetTab))
        {
            return false;
        }

        if (ReferenceEquals(ActiveAssetTab, assetTab))
        {
            ActiveAssetTab = AssetTabs.FirstOrDefault();
        }

        return true;
    }

    public AssetDocumentGroupTransfer? DetachAssetTabs()
    {
        if (AssetTabs.Count == 0)
        {
            return null;
        }

        var transfer = new AssetDocumentGroupTransfer(AssetTabs.ToArray(), ActiveAssetTab);
        AssetTabs.Clear();
        ActiveAssetTab = null;
        return transfer;
    }

    public void ClearAssetTabs()
    {
        CancelAllAssetPreviewRequests();
        AssetTabs.Clear();
        ActiveAssetTab = null;
    }

    public void CancelAllAssetPreviewRequests()
    {
        foreach (var assetTab in AssetTabs)
        {
            assetTab.CancelPreviewRequest();
        }
    }
}

