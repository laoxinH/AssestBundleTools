using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责主资产窗格的资源加载、筛选刷新与空态回退编排。
/// </summary>
/// <remarks>
/// 这里集中承接“当前活动资源包下的资产列表如何装入主窗格”这条链路，
/// 让资产工作流主 ViewModel 退回到命令入口与文档/检查器协同层。
/// </remarks>
internal sealed class BundleExplorerAssetPaneCoordinator
{
    private readonly IWorkspaceExplorerGateway _workspaceExplorerGateway;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly IBundleExplorerBundlePaneHost _bundlePane;
    private readonly BundleExplorerAssetInspectorPresenter _assetInspectorPresenter;
    private readonly AssetPaneViewModel _primaryPane;
    private readonly Func<BundleDocumentTabViewModel?> _getActiveBundleTab;
    private readonly Func<Result<AssetListResultDto>, AssetListResultDto?> _unwrapAssetListResult;
    private readonly Func<Result<SpineAssetGroupListResultDto>, SpineAssetGroupListResultDto?> _unwrapSpineAssetGroupListResult;
    private readonly Func<bool, Task> _synchronizeInspectorWithActiveBundleTabAsync;
    private readonly Action<bool> _clearCurrentInspectorHostState;
    private readonly Action _clearActiveTabState;
    private readonly Action _requestWorkspaceBrowserStatePersistence;
    private int _primaryPaneStateSynchronizationDepth;

    public BundleExplorerAssetPaneCoordinator(
        IWorkspaceExplorerGateway workspaceExplorerGateway,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBundleExplorerBundlePaneHost bundlePane,
        BundleExplorerAssetInspectorPresenter assetInspectorPresenter,
        AssetPaneViewModel primaryPane,
        Func<BundleDocumentTabViewModel?> getActiveBundleTab,
        Func<Result<AssetListResultDto>, AssetListResultDto?> unwrapAssetListResult,
        Func<Result<SpineAssetGroupListResultDto>, SpineAssetGroupListResultDto?> unwrapSpineAssetGroupListResult,
        Func<bool, Task> synchronizeInspectorWithActiveBundleTabAsync,
        Action<bool> clearCurrentInspectorHostState,
        Action clearActiveTabState,
        Action requestWorkspaceBrowserStatePersistence)
    {
        _workspaceExplorerGateway = workspaceExplorerGateway ?? throw new ArgumentNullException(nameof(workspaceExplorerGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _assetInspectorPresenter = assetInspectorPresenter ?? throw new ArgumentNullException(nameof(assetInspectorPresenter));
        _primaryPane = primaryPane ?? throw new ArgumentNullException(nameof(primaryPane));
        _getActiveBundleTab = getActiveBundleTab ?? throw new ArgumentNullException(nameof(getActiveBundleTab));
        _unwrapAssetListResult = unwrapAssetListResult ?? throw new ArgumentNullException(nameof(unwrapAssetListResult));
        _unwrapSpineAssetGroupListResult = unwrapSpineAssetGroupListResult ?? throw new ArgumentNullException(nameof(unwrapSpineAssetGroupListResult));
        _synchronizeInspectorWithActiveBundleTabAsync = synchronizeInspectorWithActiveBundleTabAsync ?? throw new ArgumentNullException(nameof(synchronizeInspectorWithActiveBundleTabAsync));
        _clearCurrentInspectorHostState = clearCurrentInspectorHostState ?? throw new ArgumentNullException(nameof(clearCurrentInspectorHostState));
        _clearActiveTabState = clearActiveTabState ?? throw new ArgumentNullException(nameof(clearActiveTabState));
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
    }

    public bool IsSynchronizingPrimaryPaneState => _primaryPaneStateSynchronizationDepth > 0;

    public async Task LoadActiveBundleTabAsync(
        BundleDocumentTabViewModel tab,
        BundleNodeViewModel? bundleNode,
        bool writeSelectionLog)
    {
        ArgumentNullException.ThrowIfNull(tab);

        BeginPrimaryPaneStateSynchronization();
        try
        {
            tab.ApplyToPane(_primaryPane);

            if (bundleNode is null)
            {
                _clearActiveTabState();
                return;
            }

            await LoadBundleIntoPaneAsync(
                _primaryPane,
                bundleNode,
                clearInspectorOnEmpty: true,
                preferredSelectedAssetId: tab.SelectedAssetId);
        }
        finally
        {
            EndPrimaryPaneStateSynchronization();
        }

        await _synchronizeInspectorWithActiveBundleTabAsync(writeSelectionLog);

        if (!writeSelectionLog)
        {
            _requestWorkspaceBrowserStatePersistence();
            return;
        }

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogBundleLoaded,
            _primaryPane.PaneTitle,
            bundleNode.DisplayName,
            _primaryPane.IsSpineGroupViewMode ? _primaryPane.SpineGroupItems.Count : _primaryPane.Items.Count));
        _requestWorkspaceBrowserStatePersistence();
    }

    public async Task RefreshPrimaryPaneAsync(
        Action captureActiveTabState,
        bool preserveCurrentDocumentTab = false)
    {
        ArgumentNullException.ThrowIfNull(captureActiveTabState);

        if (IsSynchronizingPrimaryPaneState)
        {
            return;
        }

        captureActiveTabState();
        BeginPrimaryPaneStateSynchronization();
        try
        {
            await RefreshPaneAsync(
                _primaryPane,
                clearInspectorOnEmpty: true,
                preserveCurrentDocumentTab);
        }
        finally
        {
            EndPrimaryPaneStateSynchronization();
        }

        captureActiveTabState();
    }

    private async Task RefreshPaneAsync(
        AssetPaneViewModel pane,
        bool clearInspectorOnEmpty,
        bool preserveCurrentDocumentTab = false)
    {
        if (_workspaceShell.WorkspaceSummary is null)
        {
            return;
        }

        var bundleNode = _bundlePane.FindBundleNodeByDisplayName(pane.CurrentBundleDisplayName);
        if (bundleNode is null)
        {
            ClearPaneItems(pane, clearInspectorOnEmpty);
            return;
        }

        var preferredSelectedAssetId = ReferenceEquals(pane, _primaryPane)
            ? _getActiveBundleTab()?.SelectedAssetId
            : null;

        await LoadBundleIntoPaneAsync(
            pane,
            bundleNode,
            clearInspectorOnEmpty,
            preferredSelectedAssetId);

        if (ReferenceEquals(pane, _primaryPane) && !preserveCurrentDocumentTab)
        {
            await _synchronizeInspectorWithActiveBundleTabAsync(false);
        }

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogFilterApplied,
            pane.PaneTitle,
            bundleNode.DisplayName));
    }

    private async Task LoadBundleIntoPaneAsync(
        AssetPaneViewModel pane,
        BundleNodeViewModel bundleNode,
        bool clearInspectorOnEmpty,
        string? preferredSelectedAssetId = null)
    {
        var workspaceSummary = _workspaceShell.WorkspaceSummary;
        if (workspaceSummary is null)
        {
            return;
        }

        if (pane.IsSpineGroupViewMode)
        {
            await LoadSpineGroupsIntoPaneAsync(pane, bundleNode, workspaceSummary.WorkspaceId);
            return;
        }

        var assetList = _unwrapAssetListResult(await _workspaceExplorerGateway.LoadAssetListAsync(
            new LoadAssetListQuery(
                WorkspaceId: workspaceSummary.WorkspaceId,
                BundleId: bundleNode.BundleId,
                SearchKeyword: NormalizeSearchKeyword(pane.SearchKeyword),
                AssetTypeFilter: pane.SelectedAssetType,
                AssetTypeDisplayNameFilter: pane.SelectedAssetTypeDisplayNameFilter)));

        if (assetList is null)
        {
            ClearPaneItems(pane, clearInspectorOnEmpty);
            return;
        }

        pane.ReplaceAssetTypeOptions(_assetInspectorPresenter.CreateAssetTypeOptions(assetList.TypeFacets));

        var entries = assetList.Items
            .Select(_assetInspectorPresenter.CreateAssetEntry)
            .ToArray();

        pane.ReplaceItems(
            currentBundleDisplayName: bundleNode.DisplayName,
            items: entries,
            emptyStateText: GetString(ResourceKeys.BundleExplorerNoAssets),
            preferredSelectedAssetId: string.IsNullOrWhiteSpace(preferredSelectedAssetId)
                ? null
                : new AssetId(preferredSelectedAssetId));

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogBundleLoaded,
            pane.PaneTitle,
            bundleNode.DisplayName,
            assetList.FilteredCount));
    }

    private async Task LoadSpineGroupsIntoPaneAsync(
        AssetPaneViewModel pane,
        BundleNodeViewModel bundleNode,
        WorkspaceId workspaceId)
    {
        var spineGroupList = _unwrapSpineAssetGroupListResult(await _workspaceExplorerGateway.LoadSpineAssetGroupsAsync(
            new LoadSpineAssetGroupsQuery(
                WorkspaceId: workspaceId,
                BundleId: bundleNode.BundleId,
                SearchKeyword: NormalizeSearchKeyword(pane.SearchKeyword))));

        if (spineGroupList is null)
        {
            ClearPaneItems(pane, clearInspectorOnEmpty: false);
            return;
        }

        var entries = spineGroupList.Items
            .Select(_assetInspectorPresenter.CreateSpineAssetGroupEntry)
            .ToArray();

        pane.ReplaceSpineGroups(
            currentBundleDisplayName: bundleNode.DisplayName,
            items: entries,
            emptyStateText: GetString(ResourceKeys.BundleExplorerNoSpineGroups));

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogBundleLoaded,
            pane.PaneTitle,
            bundleNode.DisplayName,
            spineGroupList.FilteredCount));
    }

    private void ClearPaneItems(AssetPaneViewModel pane, bool clearInspectorOnEmpty)
    {
        pane.ClearItems(GetString(
            pane.IsSpineGroupViewMode
                ? ResourceKeys.BundleExplorerNoSpineGroups
                : ResourceKeys.BundleExplorerNoAssets));

        if (clearInspectorOnEmpty && ReferenceEquals(pane, _primaryPane) && pane.IsAssetViewMode)
        {
            _clearCurrentInspectorHostState(true);
        }
    }

    private static string? NormalizeSearchKeyword(string searchKeyword) =>
        string.IsNullOrWhiteSpace(searchKeyword) ? null : searchKeyword.Trim();

    private void BeginPrimaryPaneStateSynchronization()
    {
        _primaryPaneStateSynchronizationDepth++;
    }

    private void EndPrimaryPaneStateSynchronization()
    {
        if (_primaryPaneStateSynchronizationDepth <= 0)
        {
            _primaryPaneStateSynchronizationDepth = 0;
            return;
        }

        _primaryPaneStateSynchronizationDepth--;
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
