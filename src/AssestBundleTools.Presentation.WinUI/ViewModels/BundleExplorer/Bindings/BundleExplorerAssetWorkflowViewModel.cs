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
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载资源列表、资产详情标签、检查器内容与预览/替换动作的编排状态。
/// </summary>
/// <remarks>
/// 这一层聚焦“当前活动资源包下的资产工作流”：
/// 资源筛选、详情标签激活、检查器同步、预览加载以及资产级操作都在这里闭环，
/// 主页面 ViewModel 只保留工作区与资源包级别的导航编排职责。
/// </remarks>
public sealed class BundleExplorerAssetWorkflowViewModel : ObservableObject, IBundleExplorerAssetWorkflowBindingHost, IDisposable
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
    private readonly SpineAssetGroupInspectorPresenter _spineGroupInspectorPresenter;
    private readonly BundleExplorerAssetPreviewCoordinator _assetPreviewCoordinator;
    private readonly BundleExplorerAssetDocumentHostCoordinator _assetDocumentHostCoordinator;
    private readonly BundleExplorerAssetDocumentTabCoordinator _assetDocumentTabCoordinator;
    private readonly BundleExplorerSpineGroupDocumentCoordinator _spineGroupDocumentCoordinator;
    private readonly BundleExplorerAssetPaneCoordinator _assetPaneCoordinator;
    private readonly BundleExplorerActiveDocumentLocator _activeDocumentLocator;
    private readonly BundleExplorerAssetCommandCoordinator _assetCommandCoordinator;
    private readonly WorkspaceOperationChangeRefreshCoordinator _operationChangeRefreshCoordinator;
    private readonly WorkspaceOperationPendingProjectionCoordinator _pendingProjectionCoordinator;

    internal event EventHandler<BundleExplorerPageStateChangedEventArgs<BundleExplorerAssetWorkflowPageStateChange>>? PageStateChanged;

    public BundleExplorerAssetWorkflowViewModel(
        IWorkspaceExplorerGateway workspaceExplorerGateway,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IWorkspaceOperationChangeNotifier workspaceOperationChangeNotifier,
        IFilePickerService filePickerService,
        IWorkspaceBatchOperationsGateway workspaceBatchOperationsGateway,
        IWorkspaceContextService workspaceContextService,
        IBackgroundTaskRunner backgroundTaskRunner,
        IPathNavigationService pathNavigationService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IBundleExplorerBundlePaneHost bundlePane,
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        Func<bool> isBusyProvider,
        Action<bool> setBusyState,
        Action requestWorkspaceBrowserStatePersistence,
        IAppBackgroundTaskStatusService? backgroundTaskStatusService = null,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null,
        Func<int>? previewWorkerCountProvider = null)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(workspaceExplorerGateway);
        ArgumentNullException.ThrowIfNull(workspaceAssetInspectionGateway);
        ArgumentNullException.ThrowIfNull(workspaceAssetOperationGateway);
        ArgumentNullException.ThrowIfNull(workspaceOperationChangeNotifier);
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
        OperationPendingTracker = new WorkspaceOperationPendingTracker();
        _assetInspectorPresenter = new BundleExplorerAssetInspectorPresenter(
            _bundlePane,
            _localizationService,
            OperationPendingTracker);
        _spineGroupInspectorPresenter = new SpineAssetGroupInspectorPresenter(
            _bundlePane,
            _localizationService,
            OperationPendingTracker,
            () => _workspaceShell.WorkspaceSummary?.WorkspaceId);
        _assetPreviewCoordinator = new BundleExplorerAssetPreviewCoordinator(
            workspaceAssetInspectionGateway,
            workspaceContextService,
            _workspaceShell,
            appLogService,
            _localizationService,
            backgroundTaskRunner,
            previewWorkerCountProvider: previewWorkerCountProvider,
            backgroundWorkScheduler: backgroundWorkScheduler);
        var modificationActionHandler = new BundleExplorerAssetModificationActionHandler(
            workspaceAssetOperationGateway,
            appLogService,
            appNotificationService,
            _localizationService,
            filePickerService,
            backgroundTaskRunner,
            backgroundTaskStatusService,
            workspaceOperationChangeNotifier,
            workspaceBatchOperationsGateway,
            backgroundWorkScheduler,
            OperationPendingTracker);
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
        _spineGroupDocumentCoordinator = new BundleExplorerSpineGroupDocumentCoordinator(
            _documentWorkspace,
            PrimaryPane,
            _assetDocumentHostCoordinator,
            _spineGroupInspectorPresenter,
            new SpineAssetGroupDetailsLoader(
                workspaceAssetInspectionGateway,
                workspaceContextService,
                _workspaceShell,
                backgroundTaskRunner,
                backgroundWorkScheduler,
                _localizationService),
            _requestWorkspaceBrowserStatePersistence,
            () => NotifyActionStates());
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
            unwrapSpineAssetGroupListResult: UnwrapSpineAssetGroupListResult,
            backgroundTaskRunner,
            synchronizeInspectorWithActiveBundleTabAsync: SynchronizeInspectorWithActiveBundleTabAsync,
            clearCurrentInspectorHostState: _assetDocumentHostCoordinator.ClearCurrentInspectorHostState,
            clearActiveTabState: ClearActiveTabState,
            requestWorkspaceBrowserStatePersistence: _requestWorkspaceBrowserStatePersistence);
        _activeDocumentLocator = new BundleExplorerActiveDocumentLocator(
            _documentWorkspace,
            PrimaryPane,
            preserveCurrentDocumentTab => RefreshPrimaryPaneAsync(preserveCurrentDocumentTab),
            () => CaptureActiveTabState());
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
            () => _documentWorkspace.ActiveSpineGroupDocumentTab,
            ReapplyActiveAssetDocumentTab);
        var operationProjectionRefreshCoordinator = new AssociatedAssetOperationProjectionRefreshCoordinator(
            workspaceAssetInspectionGateway,
            backgroundTaskRunner,
            _spineGroupInspectorPresenter,
            () => _workspaceShell.WorkspaceSummary?.WorkspaceId,
            () => IsBusy,
            _operationFeedbackBridge.AppendResultFailure);
        var operationReadModelRefreshHandler = new BundleExplorerOperationReadModelRefreshHandler(
            _documentWorkspace,
            _assetDocumentTabCoordinator,
            _spineGroupDocumentCoordinator,
            operationProjectionRefreshCoordinator);
        _operationChangeRefreshCoordinator = new WorkspaceOperationChangeRefreshCoordinator(
            workspaceOperationChangeNotifier,
            () => _workspaceShell.WorkspaceSummary?.WorkspaceId,
            () => IsBusy,
            operationReadModelRefreshHandler.RefreshAsync);
        _pendingProjectionCoordinator = new WorkspaceOperationPendingProjectionCoordinator(
            OperationPendingTracker,
            _documentWorkspace,
            () => _workspaceShell.WorkspaceSummary?.WorkspaceId);
        _documentWorkspace.AssetDocumentHostStateChanged += OnAssetDocumentHostStateChanged;
    }

    public AssetPaneViewModel PrimaryPane { get; }

    public WorkspaceOperationPendingTracker OperationPendingTracker { get; }

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
        _operationChangeRefreshCoordinator.HandleBusyStateChanged(isBusy);
    }

    public void Dispose()
    {
        _documentWorkspace.AssetDocumentHostStateChanged -= OnAssetDocumentHostStateChanged;
        _pendingProjectionCoordinator.Dispose();
        _operationChangeRefreshCoordinator.Dispose();
        _spineGroupDocumentCoordinator.Dispose();
    }

    public AssetDocumentTabViewModel CreateAssetDocumentTab(
        BundleId bundleId,
        AssetId assetId,
        string assetName) =>
        _assetDocumentHostCoordinator.CreateAssetDocumentTab(bundleId, assetId, assetName);

    public SpineAssetGroupDocumentTabViewModel CreateSpineGroupDocumentTab(SpineAssetGroupDto spineGroup) =>
        _assetDocumentHostCoordinator.CreateSpineGroupDocumentTab(spineGroup);

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
        if (PrimaryPane.ViewMode == AssetPaneViewMode.SpineGroups)
        {
            await _spineGroupDocumentCoordinator.HandlePrimaryPaneSelectionChangedAsync(
                IsSynchronizingPrimaryPaneState,
                () => CaptureActiveTabState());
        }
        else
        {
            await _assetDocumentTabCoordinator.HandlePrimaryPaneSelectionChangedAsync(
                IsSynchronizingPrimaryPaneState,
                () => CaptureActiveTabState());
        }

        CaptureActiveTabState();
    }

    public Task ActivateAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab) =>
        _assetDocumentTabCoordinator.ActivateAssetDocumentTabAsync(assetTab);

    public Task ActivateSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab) =>
        _spineGroupDocumentCoordinator.ActivateSpineGroupDocumentTabAsync(spineGroupTab);

    public Task SelectActiveSpineGroupAtlasPageAsync(SpineAtlasPageOptionViewModel? atlasPageOption) =>
        _spineGroupDocumentCoordinator.SelectActiveAtlasPageAsync(atlasPageOption);

    public async Task<bool> OpenResolvedAssetAsync(ResolvedAssetNavigationTarget? target)
    {
        if (target is null ||
            ActiveBundleTab is null ||
            !ActiveBundleTab.BundleId.Equals(target.BundleId))
        {
            return false;
        }

        if (!ActiveBundleTab.TryGetAssetTab(target.AssetId, out var assetTab))
        {
            assetTab = _assetDocumentHostCoordinator.CreateAssetDocumentTab(
                target.BundleId,
                target.AssetId,
                target.DisplayName);
            ActiveBundleTab.AddAssetTab(assetTab, activate: false);
        }

        await _assetDocumentTabCoordinator.ActivateAssetDocumentTabAsync(assetTab);
        return true;
    }

    public Task CloseAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab) =>
        _assetDocumentTabCoordinator.CloseAssetDocumentTabAsync(assetTab);

    public Task CloseSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab) =>
        _spineGroupDocumentCoordinator.CloseSpineGroupDocumentTabAsync(spineGroupTab);

    public Task<SpineAssetGroupDocumentTabViewModel?> DetachSpineGroupDocumentTabForExternalHostAsync(
        SpineAssetGroupDocumentTabViewModel? spineGroupTab) =>
        _spineGroupDocumentCoordinator.DetachSpineGroupDocumentTabForExternalHostAsync(spineGroupTab);

    public Task RestoreDetachedSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel spineGroupTab) =>
        _spineGroupDocumentCoordinator.RestoreDetachedSpineGroupDocumentTabAsync(spineGroupTab);

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

    public Task<AssetDocumentTabViewModel?> DetachAssetDocumentTabForExternalHostAsync(
        AssetDocumentTabViewModel? assetTab) =>
        _assetDocumentTabCoordinator.DetachAssetDocumentTabForExternalHostAsync(assetTab);

    public Task<AssetDocumentTabItemViewModel?> DetachDocumentTabForExternalHostAsync(
        AssetDocumentTabItemViewModel? documentTab)
    {
        if (documentTab is null || ActiveBundleTab is null || !ActiveBundleTab.DocumentTabs.Contains(documentTab))
        {
            return Task.FromResult<AssetDocumentTabItemViewModel?>(null);
        }

        var ownerTab = ActiveBundleTab;
        var wasActive = ReferenceEquals(ownerTab.ActiveDocumentTab, documentTab);
        if (!ownerTab.DetachDocumentTab(documentTab))
        {
            return Task.FromResult<AssetDocumentTabItemViewModel?>(null);
        }

        if (wasActive && ownerTab.ActiveDocumentTab is { } activeDocumentTab)
        {
            QueueFallbackDocumentActivation(ownerTab, activeDocumentTab);
            return Task.FromResult<AssetDocumentTabItemViewModel?>(documentTab);
        }

        if (wasActive)
        {
            PrimaryPane.ClearSelection();
        }

        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
        return Task.FromResult<AssetDocumentTabItemViewModel?>(documentTab);
    }

    private void QueueFallbackDocumentActivation(
        BundleDocumentTabViewModel ownerTab,
        AssetDocumentTabItemViewModel documentTab)
    {
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
        _ = ActivateFallbackDocumentTabAsync(ownerTab, documentTab);
    }

    private async Task ActivateFallbackDocumentTabAsync(
        BundleDocumentTabViewModel ownerTab,
        AssetDocumentTabItemViewModel documentTab)
    {
        try
        {
            await Task.Yield();

            if (!ownerTab.DocumentTabs.Contains(documentTab))
            {
                return;
            }

            await ActivateDocumentTabAsync(documentTab);
        }
        catch
        {
            // 窗口化迁出应先完成；剩余标签刷新失败留给后续用户激活恢复。
        }
    }

    public Task<AssetDocumentGroupTransfer?> DetachActiveAssetDocumentGroupForExternalHostAsync() =>
        _assetDocumentTabCoordinator.DetachActiveAssetDocumentGroupForExternalHostAsync();

    public Task RefreshPrimaryPaneAsync(bool preserveCurrentDocumentTab = false) =>
        _assetPaneCoordinator.RefreshPrimaryPaneAsync(
            () => CaptureActiveTabState(),
            preserveCurrentDocumentTab);

    public void OpenCurrentPreviewLocation() => _assetCommandCoordinator.OpenCurrentPreviewLocation();

    public Task<bool> LocateActiveDocumentInPrimaryPaneAsync() =>
        _activeDocumentLocator.LocateActiveDocumentAsync();

    public Task ExtractSelectedAssetAsync() => _assetCommandCoordinator.ExtractSelectedAssetAsync();

    public Task ExtractSelectedAssetsAsync() => _assetCommandCoordinator.ExtractSelectedAssetsAsync();

    public Task SelectWorkspaceReplacementSourceAsync() => _assetCommandCoordinator.SelectWorkspaceReplacementSourceAsync();

    public Task SelectFileReplacementSourceAsync() => _assetCommandCoordinator.SelectFileReplacementSourceAsync();

    public Task ModifyFromFileReplacementSourceAsync() => _assetCommandCoordinator.ModifyFromFileReplacementSourceAsync();

    public Task SelectSpriteTextureReplacementSourceAsync() =>
        _assetCommandCoordinator.SelectSpriteTextureReplacementSourceAsync();

    public Task ApplyPendingReplacementAsync() => _assetCommandCoordinator.ApplyPendingReplacementAsync();

    public Task UndoAppliedReplacementAsync(string? replacementOperationId = null) =>
        _assetCommandCoordinator.UndoAppliedReplacementAsync(replacementOperationId);

    public Task RedoAppliedReplacementAsync(string? replacementOperationId = null) =>
        _assetCommandCoordinator.RedoAppliedReplacementAsync(replacementOperationId);

    public Task DeleteChangeRecordAsync(string? operationId) =>
        _assetCommandCoordinator.DeleteChangeRecordAsync(operationId);

    private void ReapplyActiveAssetDocumentTab(AssetDocumentTabViewModel assetTab, AssetDetailsDto details) =>
        _assetDocumentTabCoordinator.ReapplyAssetDocumentTab(assetTab, details);

    private Task ActivateDocumentTabAsync(AssetDocumentTabItemViewModel documentTab)
    {
        if (documentTab.AssetTab is { } assetTab)
        {
            return ActivateAssetDocumentTabAsync(assetTab);
        }

        return documentTab.SpineGroupTab is { } spineGroupTab
            ? ActivateSpineGroupDocumentTabAsync(spineGroupTab)
            : Task.CompletedTask;
    }

    private Task SynchronizeInspectorWithActiveBundleTabAsync(bool writeLog)
    {
        if (ActiveBundleTab?.ActiveDocumentTab?.SpineGroupTab is { } activeSpineGroupTab)
        {
            return _spineGroupDocumentCoordinator.ActivateSpineGroupDocumentTabAsync(activeSpineGroupTab);
        }

        if (ActiveBundleTab?.ActiveDocumentTab?.AssetTab is not null)
        {
            return _assetDocumentTabCoordinator.SynchronizeInspectorWithActiveBundleTabAsync(writeLog);
        }

        return PrimaryPane.ViewMode == AssetPaneViewMode.SpineGroups
            ? _spineGroupDocumentCoordinator.SynchronizeInspectorWithActiveBundleTabAsync()
            : _assetDocumentTabCoordinator.SynchronizeInspectorWithActiveBundleTabAsync(writeLog);
    }

    private string ResolvePrimaryPaneEmptyStateText()
        => _bundlePane.ResolvePrimaryPaneEmptyStateText(
            noWorkspaceText: GetString(ResourceKeys.BundleExplorerNoWorkspace),
            emptyWorkspaceText: GetString(ResourceKeys.BundleExplorerEmptyWorkspace),
            noOpenTabsText: GetString(ResourceKeys.BundleExplorerNoOpenTabs));

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private AssetListResultDto? UnwrapAssetListResult(Result<AssetListResultDto> result) =>
        _operationFeedbackBridge.TryGetValue(result, out var assetList) ? assetList : null;

    private SpineAssetGroupListResultDto? UnwrapSpineAssetGroupListResult(Result<SpineAssetGroupListResultDto> result) =>
        _operationFeedbackBridge.TryGetValue(result, out var spineGroupList) ? spineGroupList : null;

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
        else if (_assetCommandCoordinator.TryGetActiveSpineGroupDocumentState(out var spineGroupTab))
        {
            _spineGroupInspectorPresenter.SyncInspectorActionStates(Inspector, spineGroupTab, IsBusy);
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
