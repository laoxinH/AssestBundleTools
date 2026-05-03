using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载资源列表、资产详情标签、检查器内容与预览/替换动作的编排状态。
/// </summary>
/// <remarks>
/// 这一层聚焦“当前活动资源包下的资产工作流”：
/// 资源筛选、详情标签激活、检查器同步、预览加载以及资产级操作都在这里闭环，
/// 主页面 ViewModel 只保留工作区与资源包级别的导航编排职责。
/// </remarks>
public sealed class BundleExplorerAssetWorkflowViewModel : ObservableObject, IBundleExplorerAssetWorkflowBindingHost
{
    private readonly ILocalizationService _localizationService;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly IBundleExplorerBundlePaneHost _bundlePane;
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action<bool> _setBusyState;
    private readonly Action _requestWorkspaceBrowserStatePersistence;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly BundleExplorerAssetInspectorPresenter _assetInspectorPresenter;
    private readonly BundleExplorerAssetPreviewCoordinator _assetPreviewCoordinator;
    private readonly BundleExplorerAssetDocumentHostCoordinator _assetDocumentHostCoordinator;
    private readonly BundleExplorerAssetDocumentTabCoordinator _assetDocumentTabCoordinator;
    private readonly BundleExplorerAssetPaneCoordinator _assetPaneCoordinator;
    private readonly BundleExplorerAssetCommandCoordinator _assetCommandCoordinator;

    internal event EventHandler<BundleExplorerPageStateChangedEventArgs<BundleExplorerAssetWorkflowPageStateChange>>? PageStateChanged;

    public BundleExplorerAssetWorkflowViewModel(
        IWorkspaceExplorerGateway workspaceExplorerGateway,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IWorkspaceContextService workspaceContextService,
        IBackgroundTaskRunner backgroundTaskRunner,
        IPathNavigationService pathNavigationService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBundleExplorerBundlePaneHost bundlePane,
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        Func<bool> isBusyProvider,
        Action<bool> setBusyState,
        Action requestWorkspaceBrowserStatePersistence)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(workspaceExplorerGateway);
        ArgumentNullException.ThrowIfNull(workspaceAssetInspectionGateway);
        ArgumentNullException.ThrowIfNull(workspaceAssetOperationGateway);
        ArgumentNullException.ThrowIfNull(appNotificationService);
        ArgumentNullException.ThrowIfNull(filePickerService);
        ArgumentNullException.ThrowIfNull(workspaceContextService);
        ArgumentNullException.ThrowIfNull(backgroundTaskRunner);
        ArgumentNullException.ThrowIfNull(pathNavigationService);
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _setBusyState = setBusyState ?? throw new ArgumentNullException(nameof(setBusyState));
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
        _operationFeedbackBridge = new BundleExplorerOperationFeedbackBridge(
            appLogService,
            appNotificationService,
            _localizationService);
        _assetInspectorPresenter = new BundleExplorerAssetInspectorPresenter(
            _bundlePane,
            _localizationService);
        _assetPreviewCoordinator = new BundleExplorerAssetPreviewCoordinator(
            workspaceAssetInspectionGateway,
            workspaceContextService,
            _workspaceShell,
            appLogService,
            _localizationService,
            backgroundTaskRunner);
        var modificationActionHandler = new BundleExplorerAssetModificationActionHandler(
            workspaceAssetOperationGateway,
            appLogService,
            appNotificationService,
            _localizationService,
            filePickerService);
        PrimaryPane = new AssetPaneViewModel();
        _assetDocumentHostCoordinator = new BundleExplorerAssetDocumentHostCoordinator(
            _documentWorkspace,
            _localizationService,
            () => NotifyActionStates(),
            _requestWorkspaceBrowserStatePersistence);

        PrimaryPane.SupportsBatchExtractionQuickAction = true;
        PrimaryPane.ReplaceAssetTypeOptions(_assetInspectorPresenter.CreateAssetTypeOptions());
        PrimaryPane.ConfigurePane(
            PaneId.New(),
            GetString(ResourceKeys.BundleExplorerPrimaryPaneTitle),
            GetString(ResourceKeys.BundleExplorerNoAssets));
        _assetDocumentTabCoordinator = new BundleExplorerAssetDocumentTabCoordinator(
            workspaceAssetInspectionGateway,
            _workspaceShell,
            _documentWorkspace,
            _assetInspectorPresenter,
            _assetPreviewCoordinator,
            appLogService,
            _localizationService,
            backgroundTaskRunner,
            PrimaryPane,
            _assetDocumentHostCoordinator,
            () => IsBusy,
            _requestWorkspaceBrowserStatePersistence,
            () => NotifyActionStates(),
            _operationFeedbackBridge.AppendResultFailure,
            _operationFeedbackBridge.FormatFirstErrorMessage);
        _assetPaneCoordinator = new BundleExplorerAssetPaneCoordinator(
            workspaceExplorerGateway,
            appLogService,
            _localizationService,
            _workspaceShell,
            _bundlePane,
            _assetInspectorPresenter,
            PrimaryPane,
            getActiveBundleTab: () => ActiveBundleTab,
            unwrapAssetListResult: UnwrapAssetListResult,
            synchronizeInspectorWithActiveBundleTabAsync: _assetDocumentTabCoordinator.SynchronizeInspectorWithActiveBundleTabAsync,
            clearCurrentInspectorHostState: _assetDocumentHostCoordinator.ClearCurrentInspectorHostState,
            clearActiveTabState: ClearActiveTabState,
            requestWorkspaceBrowserStatePersistence: _requestWorkspaceBrowserStatePersistence);
        _assetCommandCoordinator = new BundleExplorerAssetCommandCoordinator(
            appLogService,
            _localizationService,
            pathNavigationService,
            _workspaceShell,
            PrimaryPane,
            modificationActionHandler,
            _operationFeedbackBridge,
            () => IsBusy,
            _setBusyState,
            () => Inspector,
            () => ActiveAssetDocumentTab,
            RefreshPrimaryPaneAsync,
            ReapplyActiveAssetDocumentTab);
        _documentWorkspace.AssetDocumentHostStateChanged += OnAssetDocumentHostStateChanged;
    }

    public AssetPaneViewModel PrimaryPane { get; }

    public InspectorPanelViewModel Inspector => _assetDocumentHostCoordinator.CurrentInspector;

    public bool HasSelectedAsset => _assetCommandCoordinator.HasSelectedAsset;

    public bool CanExtractSelectedAsset => _assetCommandCoordinator.CanExtractSelectedAsset;

    public bool CanSelectFileReplacementSource => _assetCommandCoordinator.CanSelectFileReplacementSource;

    public bool CanSelectWorkspaceReplacementSource => _assetCommandCoordinator.CanSelectWorkspaceReplacementSource;

    private bool IsBusy => _isBusyProvider();

    private BundleDocumentTabViewModel? ActiveBundleTab => _documentWorkspace.ActiveBundleTab;

    private AssetDocumentTabViewModel? ActiveAssetDocumentTab => _documentWorkspace.ActiveAssetDocumentTab;

    private bool IsSynchronizingPrimaryPaneState => _assetPaneCoordinator.IsSynchronizingPrimaryPaneState;

    private bool IsSynchronizingAssetDocumentState => _assetDocumentTabCoordinator.IsSynchronizingAssetDocumentState;

    public void UpdateBusyState(bool isBusy)
    {
        PrimaryPane.IsBusy = isBusy;
        NotifyActionStates();
    }

    public AssetDocumentTabViewModel CreateAssetDocumentTab(
        BundleId bundleId,
        AssetId assetId,
        string assetName)
    {
        return _assetDocumentHostCoordinator.CreateAssetDocumentTab(bundleId, assetId, assetName);
    }

    public void CaptureCurrentBundleTabStateBeforeActivation(BundleDocumentTabViewModel nextTab)
    {
        ArgumentNullException.ThrowIfNull(nextTab);

        if (ActiveBundleTab is null || ReferenceEquals(ActiveBundleTab, nextTab))
        {
            return;
        }

        CaptureActiveTabState(requestPersistence: false);
    }

    /// <summary>
    /// 在资源包标签切换、工作区保存前统一抓取当前资产筛选与选中态。
    /// 这样“列表筛选状态”和“打开的详情标签状态”总是同源持久化，避免来回切换后被旧状态覆盖。
    /// </summary>
    public void CaptureActiveTabState(bool requestPersistence = true)
    {
        ActiveBundleTab?.CaptureFromPane(PrimaryPane);

        if (requestPersistence)
        {
            _requestWorkspaceBrowserStatePersistence();
        }
    }

    public Task LoadActiveBundleTabAsync(
        BundleDocumentTabViewModel tab,
        BundleNodeViewModel? bundleNode,
        bool writeSelectionLog) =>
        _assetPaneCoordinator.LoadActiveBundleTabAsync(tab, bundleNode, writeSelectionLog);

    public void ClearActiveTabState()
    {
        _documentWorkspace.SetActiveBundleTab(null);
        _bundlePane.SetSelectedBundle(null);
        _assetDocumentHostCoordinator.ResetEmptyInspector();
        PrimaryPane.ClearItems(ResolvePrimaryPaneEmptyStateText());
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
    }

    public async Task HandlePrimaryPaneSelectionChangedAsync()
    {
        await _assetDocumentTabCoordinator.HandlePrimaryPaneSelectionChangedAsync(
            IsSynchronizingPrimaryPaneState,
            () => CaptureActiveTabState());
        CaptureActiveTabState();
    }

    public Task ActivateAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab) =>
        _assetDocumentTabCoordinator.ActivateAssetDocumentTabAsync(assetTab);

    public Task CloseAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab) =>
        _assetDocumentTabCoordinator.CloseAssetDocumentTabAsync(assetTab);

    /// <summary>
    /// 在当前活动资源包下，仅保留指定资产详情标签。
    /// 这样可以快速收拢右侧详情区，同时保留用户明确选中的目标上下文。
    /// </summary>
    public Task CloseOtherAssetDocumentTabsAsync(AssetDocumentTabViewModel? assetTab) =>
        _assetDocumentTabCoordinator.CloseOtherAssetDocumentTabsAsync(assetTab);

    /// <summary>
    /// 清空当前资源包下所有已打开的资产详情标签。
    /// 关闭后同时清空列表焦点，避免旧选中项立刻把详情标签重新打开。
    /// </summary>
    public Task CloseAllAssetDocumentTabsAsync() => _assetDocumentTabCoordinator.CloseAllAssetDocumentTabsAsync();

    public Task RefreshPrimaryPaneAsync() =>
        _assetPaneCoordinator.RefreshPrimaryPaneAsync(() => CaptureActiveTabState());

    public void OpenCurrentPreviewLocation() => _assetCommandCoordinator.OpenCurrentPreviewLocation();

    public Task ExtractSelectedAssetAsync() => _assetCommandCoordinator.ExtractSelectedAssetAsync();

    public Task ExtractSelectedAssetsAsync() => _assetCommandCoordinator.ExtractSelectedAssetsAsync();

    public Task SelectWorkspaceReplacementSourceAsync() => _assetCommandCoordinator.SelectWorkspaceReplacementSourceAsync();

    public Task SelectFileReplacementSourceAsync() => _assetCommandCoordinator.SelectFileReplacementSourceAsync();

    public Task ApplyPendingReplacementAsync() => _assetCommandCoordinator.ApplyPendingReplacementAsync();

    public Task UndoAppliedReplacementAsync() => _assetCommandCoordinator.UndoAppliedReplacementAsync();

    private void ReapplyActiveAssetDocumentTab(AssetDocumentTabViewModel assetTab, AssetDetailsDto details) =>
        _assetDocumentTabCoordinator.ReapplyAssetDocumentTab(assetTab, details);

    private string ResolvePrimaryPaneEmptyStateText()
        => _bundlePane.ResolvePrimaryPaneEmptyStateText(
            noWorkspaceText: GetString(ResourceKeys.BundleExplorerNoWorkspace),
            emptyWorkspaceText: GetString(ResourceKeys.BundleExplorerEmptyWorkspace),
            noOpenTabsText: GetString(ResourceKeys.BundleExplorerNoOpenTabs));

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private AssetListResultDto? UnwrapAssetListResult(Result<AssetListResultDto> result) =>
        _operationFeedbackBridge.TryGetValue(result, out var assetList) ? assetList : null;

    private void OnAssetDocumentHostStateChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(Inspector));
        NotifyActionStates(
            BundleExplorerAssetWorkflowPageStateChange.Inspector |
            BundleExplorerAssetWorkflowPageStateChange.AssetCommandState);
    }

    private void NotifyActionStates(
        BundleExplorerAssetWorkflowPageStateChange pageStateChange = BundleExplorerAssetWorkflowPageStateChange.AssetCommandState)
    {
        if (_assetCommandCoordinator.TryGetActiveAssetDocumentState(out var assetTab, out var details))
        {
            _assetInspectorPresenter.SyncInspectorActionStates(Inspector, assetTab, details, IsBusy);
        }
        else
        {
            _assetInspectorPresenter.SyncInspectorActionStates(Inspector, null, null, IsBusy);
        }

        OnPropertyChanged(nameof(HasSelectedAsset));
        OnPropertyChanged(nameof(CanExtractSelectedAsset));
        OnPropertyChanged(nameof(CanSelectFileReplacementSource));
        OnPropertyChanged(nameof(CanSelectWorkspaceReplacementSource));
        NotifyPageStateChanged(pageStateChange);
    }

    private void NotifyPageStateChanged(BundleExplorerAssetWorkflowPageStateChange changeFlags)
    {
        if (changeFlags == BundleExplorerAssetWorkflowPageStateChange.None)
        {
            return;
        }

        PageStateChanged?.Invoke(this, new BundleExplorerPageStateChangedEventArgs<BundleExplorerAssetWorkflowPageStateChange>(changeFlags));
    }
}
