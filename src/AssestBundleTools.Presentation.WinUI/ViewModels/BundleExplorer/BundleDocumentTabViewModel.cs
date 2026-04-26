using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 表示主工作区中一个已打开的资源包标签页。
/// </summary>
/// <remarks>
/// 标签页只保存“当前资源包 + 当前列表浏览状态”这些页面级瞬时信息，
/// 不直接承载真正的资源数据和替换结果，避免把工作区事实来源重新拆散。
/// </remarks>
public sealed class BundleDocumentTabViewModel : ObservableObject
{
    private BundleId _bundleId;
    private string _displayName;
    private string _logicalPath;
    private string _searchKeyword = string.Empty;
    private AssetType? _selectedAssetType;
    private string? _selectedAssetId;
    private bool _showModifiedOnly;
    private bool _showPreviewableOnly;
    private bool _isPreview;

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

    public AssetType? SelectedAssetType
    {
        get => _selectedAssetType;
        private set => SetProperty(ref _selectedAssetType, value);
    }

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

    public void CaptureFromPane(AssetPaneViewModel pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        SearchKeyword = pane.SearchKeyword;
        SelectedAssetType = pane.SelectedAssetType;
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
        AssetType? selectedAssetType,
        string? selectedAssetId,
        bool showModifiedOnly,
        bool showPreviewableOnly)
    {
        SearchKeyword = searchKeyword;
        SelectedAssetType = selectedAssetType;
        SelectedAssetId = selectedAssetId;
        ShowModifiedOnly = showModifiedOnly;
        ShowPreviewableOnly = showPreviewableOnly;
    }

    public void ApplyToPane(AssetPaneViewModel pane)
    {
        ArgumentNullException.ThrowIfNull(pane);

        pane.RestoreViewState(
            searchKeyword: SearchKeyword,
            selectedAssetType: SelectedAssetType,
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
        SelectedAssetType = null;
        SelectedAssetId = null;
        ShowModifiedOnly = false;
        ShowPreviewableOnly = false;
        IsPreview = isPreview;
    }

    public void PromoteToPinnedTab()
    {
        if (!IsPreview)
        {
            return;
        }

        IsPreview = false;
    }
}
