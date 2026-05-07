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
    private AssetPaneViewMode _assetPaneViewMode;
    private bool _showModifiedOnly;
    private bool _showPreviewableOnly;
    private bool _isPreview;
    private readonly AssetDocumentGroupViewModel _documentGroup = new();

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

    public AssetPaneViewMode AssetPaneViewMode
    {
        get => _assetPaneViewMode;
        private set => SetProperty(ref _assetPaneViewMode, value);
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
    public AssetDocumentGroupViewModel DocumentGroup => _documentGroup;

    public ObservableCollection<AssetDocumentTabItemViewModel> DocumentTabs => DocumentGroup.DocumentTabs;

    public ObservableCollection<AssetDocumentTabViewModel> AssetTabs => DocumentGroup.AssetTabs;

    public ObservableCollection<SpineAssetGroupDocumentTabViewModel> SpineGroupTabs => DocumentGroup.SpineGroupTabs;

    public AssetDocumentTabItemViewModel? ActiveDocumentTab => DocumentGroup.ActiveDocumentTab;

    public AssetDocumentTabViewModel? ActiveAssetTab => DocumentGroup.ActiveAssetTab;

    public SpineAssetGroupDocumentTabViewModel? ActiveSpineGroupTab => DocumentGroup.ActiveSpineGroupTab;

    public void CaptureFromPane(AssetPaneViewModel pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        SearchKeyword = pane.SearchKeyword;
        SelectedAssetTypeFilterKey = pane.SelectedAssetTypeFilterKey;
        SelectedAssetId = pane.SelectedAssetId?.Value;
        AssetPaneViewMode = pane.ViewMode;
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
        bool showPreviewableOnly,
        string? assetPaneViewMode = null)
    {
        SearchKeyword = searchKeyword;
        SelectedAssetTypeFilterKey = selectedAssetTypeFilterKey;
        SelectedAssetId = selectedAssetId;
        AssetPaneViewMode = AssetPaneViewModePersistence.FromPersistenceKey(assetPaneViewMode);
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
            showPreviewableOnly: ShowPreviewableOnly,
            viewMode: AssetPaneViewMode);
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
        AssetPaneViewMode = AssetPaneViewMode.Assets;
        ShowModifiedOnly = false;
        ShowPreviewableOnly = false;
        IsPreview = isPreview;
        ClearAssetTabs();
        ClearSpineGroupTabs();
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
        DocumentGroup.AddAssetTab(assetTab, activate);
    }

    public void SetActiveAssetTab(AssetDocumentTabViewModel? assetTab)
    {
        DocumentGroup.SetActiveAssetTab(assetTab);
    }

    public void RestoreAssetTabs(
        IEnumerable<AssetDocumentTabViewModel> assetTabs,
        string? activeAssetTabId)
    {
        DocumentGroup.RestoreAssetTabs(assetTabs, activeAssetTabId);
    }

    public void RestoreSpineGroupTabs(
        IEnumerable<SpineAssetGroupDocumentTabViewModel> spineGroupTabs,
        string? activeSpineGroupTabId)
    {
        DocumentGroup.RestoreSpineGroupTabs(spineGroupTabs, activeSpineGroupTabId);
    }

    /// <summary>
    /// 资产文档标签的展示顺序属于当前资源包标签页的页面级状态。
    /// 拖拽重排后在这里统一回写到底层集合，便于后续随工作区元数据一起恢复。
    /// </summary>
    public bool ReorderAssetTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedAssetTabs)
    {
        return DocumentGroup.ReorderAssetTabs(orderedAssetTabs);
    }

    public bool ReorderDocumentTabs(IReadOnlyList<AssetDocumentTabItemViewModel> orderedDocumentTabs)
    {
        return DocumentGroup.ReorderDocumentTabs(orderedDocumentTabs);
    }

    public bool TryGetSpineGroupTab(
        string groupId,
        [NotNullWhen(true)] out SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        return DocumentGroup.TryGetSpineGroupTab(BundleId, groupId, out spineGroupTab);
    }

    public void AddSpineGroupTab(SpineAssetGroupDocumentTabViewModel spineGroupTab, bool activate)
    {
        DocumentGroup.AddSpineGroupTab(spineGroupTab, activate);
    }

    public void SetActiveSpineGroupTab(SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        DocumentGroup.SetActiveSpineGroupTab(spineGroupTab);
    }

    public bool ReorderSpineGroupTabs(IReadOnlyList<SpineAssetGroupDocumentTabViewModel> orderedSpineGroupTabs)
    {
        return DocumentGroup.ReorderSpineGroupTabs(orderedSpineGroupTabs);
    }

    public void RemoveSpineGroupTab(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        DocumentGroup.RemoveSpineGroupTab(spineGroupTab);
    }

    public bool DetachSpineGroupTab(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        return DocumentGroup.DetachSpineGroupTab(spineGroupTab);
    }

    public void RemoveAssetTab(AssetDocumentTabViewModel assetTab)
    {
        DocumentGroup.RemoveAssetTab(assetTab);
    }

    /// <summary>
    /// 将资产文档标签移交给独立资源编辑窗口时使用。
    /// 移交过程不取消目标标签自身的预览请求，页面级预览状态会跟随标签移动。
    /// </summary>
    public bool DetachAssetTab(AssetDocumentTabViewModel assetTab)
    {
        return DocumentGroup.DetachAssetTab(assetTab);
    }

    public bool DetachDocumentTab(AssetDocumentTabItemViewModel documentTab)
    {
        return DocumentGroup.DetachDocumentTab(documentTab);
    }

    public AssetDocumentGroupTransfer? DetachAssetTabs()
    {
        return DocumentGroup.DetachAssetTabs();
    }

    public AssetDocumentGroupTransfer? DetachDocumentTabs()
    {
        return DocumentGroup.DetachDocumentTabs();
    }

    public void ClearAssetTabs()
    {
        DocumentGroup.ClearAssetTabs();
    }

    public void ClearSpineGroupTabs()
    {
        DocumentGroup.ClearSpineGroupTabs();
    }

    public void CancelAllAssetPreviewRequests()
    {
        DocumentGroup.CancelAllAssetPreviewRequests();
    }
}

