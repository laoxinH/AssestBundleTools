using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 维护工作台页面的整体展示状态与页面级交互流程。
/// </summary>
/// <remarks>
/// 当前 ViewModel 主要承担页面级外观与协调职责。
/// 负责工作区加载、恢复、资源包标签导航与浏览状态持久化。
/// 而资产窗格、检查器和预览、替换动作已分别下沉到更细粒度的子 ViewModel。
/// </remarks>
public sealed class BundleExplorerViewModel : ObservableObject
{
    private readonly BundleExplorerBrowserStateCoordinator _browserStateCoordinator;
    private readonly BundleExplorerBundleTabCoordinator _bundleTabCoordinator;
    private readonly BundleExplorerResolvedAssetNavigationCoordinator _resolvedAssetNavigationCoordinator;
    private readonly BundleExplorerAssetDocumentMoveCoordinator _assetDocumentMoveCoordinator;
    private readonly AssetEditorWindowDraftRestorePlanCoordinator _assetEditorWindowDraftRestorePlanCoordinator;
    private readonly BundleExplorerWorkspaceImportActionHandler _workspaceImportActionHandler;
    private readonly BundleExplorerBundleActionHandler _bundleActionHandler;
    private readonly BundleExplorerWorkspaceBindingCoordinator _workspaceBindingCoordinator;
    private readonly BundleExplorerWorkspaceShellViewModel _workspaceShell;
    private readonly BundleExplorerBundlePaneViewModel _bundlePane;
    private readonly BundleExplorerDocumentWorkspaceViewModel _documentWorkspace;
    private readonly BundleExplorerAssetWorkflowViewModel _assetWorkflow;
    private readonly WorkspaceImportProgressViewModel _workspaceImportProgress;
    private readonly AssetEditorWindowRestorePromptViewModel _assetEditorWindowRestorePrompt;
    private Func<IReadOnlyList<WorkspaceAssetEditorWindowStateDto>> _captureOpenAssetEditorWindows = static () => Array.Empty<WorkspaceAssetEditorWindowStateDto>();
    private bool _isInitialized;
    private bool _isBusy;

    public BundleExplorerViewModel(
        IWorkspaceExplorerGateway workspaceExplorerGateway,
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAssetEditDraftRestorePlanGateway assetEditDraftRestorePlanGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService,
        IWorkspaceContextService workspaceContextService,
        IWorkspaceImportCoordinatorService workspaceImportCoordinatorService,
        IBackgroundTaskRunner backgroundTaskRunner,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        IPathNavigationService pathNavigationService)
    {
        ArgumentNullException.ThrowIfNull(workspaceExplorerGateway);
        ArgumentNullException.ThrowIfNull(workspaceAssetInspectionGateway);
        ArgumentNullException.ThrowIfNull(workspaceAssetOperationGateway);
        ArgumentNullException.ThrowIfNull(appLogService);
        ArgumentNullException.ThrowIfNull(appNotificationService);
        ArgumentNullException.ThrowIfNull(localizationService);
        ArgumentNullException.ThrowIfNull(workspaceContextService);
        ArgumentNullException.ThrowIfNull(workspaceImportCoordinatorService);
        ArgumentNullException.ThrowIfNull(pathNavigationService);
        var operationFeedbackBridge = new BundleExplorerOperationFeedbackBridge(
            appLogService,
            appNotificationService,
            localizationService);
        _workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry)));
        _workspaceShell.PageStateChanged += OnWorkspaceShellPageStateChanged;
        _bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        _bundlePane.BrowserStateChanged += OnBundlePaneBrowserStateChanged;
        _documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        _workspaceImportProgress = new WorkspaceImportProgressViewModel(localizationService);
        _assetEditorWindowRestorePrompt = new AssetEditorWindowRestorePromptViewModel(localizationService);
        _assetEditorWindowDraftRestorePlanCoordinator = new AssetEditorWindowDraftRestorePlanCoordinator(
            assetEditDraftRestorePlanGateway ?? throw new ArgumentNullException(nameof(assetEditDraftRestorePlanGateway)),
            workspaceContextService,
            _assetEditorWindowRestorePrompt);
        _assetWorkflow = new BundleExplorerAssetWorkflowViewModel(
            workspaceExplorerGateway,
            workspaceAssetInspectionGateway,
            workspaceAssetOperationGateway,
            appLogService,
            appNotificationService,
            localizationService,
            filePickerService ?? throw new ArgumentNullException(nameof(filePickerService)),
            workspaceContextService,
            backgroundTaskRunner,
            pathNavigationService,
            _workspaceShell,
            _bundlePane,
            _documentWorkspace,
            isBusyProvider: () => IsBusy,
            setBusyState: value => IsBusy = value,
            requestWorkspaceBrowserStatePersistence: RequestWorkspaceBrowserStatePersistence);
        _bundleTabCoordinator = new BundleExplorerBundleTabCoordinator(
            _bundlePane,
            _documentWorkspace,
            _assetWorkflow,
            hasWorkspaceProvider: () => _workspaceShell.HasWorkspace,
            requestWorkspaceBrowserStatePersistence: RequestWorkspaceBrowserStatePersistence);
        _resolvedAssetNavigationCoordinator = new BundleExplorerResolvedAssetNavigationCoordinator(
            _workspaceShell,
            _bundlePane,
            _documentWorkspace,
            _bundleTabCoordinator,
            _assetWorkflow,
            operationFeedbackBridge,
            appLogService,
            localizationService,
            isBusyProvider: () => IsBusy);
        _assetDocumentMoveCoordinator = new BundleExplorerAssetDocumentMoveCoordinator(
            _workspaceShell,
            _bundlePane,
            _documentWorkspace,
            _bundleTabCoordinator,
            _assetWorkflow,
            operationFeedbackBridge,
            appLogService,
            localizationService,
            isBusyProvider: () => IsBusy,
            requestWorkspaceBrowserStatePersistence: RequestWorkspaceBrowserStatePersistence);
        _browserStateCoordinator = new BundleExplorerBrowserStateCoordinator(
            workspaceContextService,
            _bundlePane,
            _documentWorkspace,
            hasWorkspaceProvider: () => _workspaceShell.WorkspaceSummary is not null,
            captureActiveTabStateWithoutPersistence: () => _assetWorkflow.CaptureActiveTabState(requestPersistence: false),
            createAssetDocumentTab: _assetWorkflow.CreateAssetDocumentTab,
            activateBundleTabAsync: tab => _bundleTabCoordinator.SetActiveBundleTabAsync(tab, writeSelectionLog: false),
            createSpineGroupDocumentTab: _assetWorkflow.CreateSpineGroupDocumentTab,
            captureOpenAssetEditorWindows: () => _captureOpenAssetEditorWindows(),
            restorePendingAssetEditorWindows: snapshots => _assetEditorWindowRestorePrompt.ReplacePendingSnapshots(snapshots),
            capturePendingAssetEditorWindows: () => _assetEditorWindowRestorePrompt.PendingWindows);
        _workspaceBindingCoordinator = new BundleExplorerWorkspaceBindingCoordinator(
            workspaceExplorerGateway,
            appLogService,
            localizationService,
            workspaceContextService,
            operationFeedbackBridge,
            backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner)),
            _workspaceShell,
            _bundlePane,
            _documentWorkspace,
            _assetWorkflow,
            _browserStateCoordinator,
            isBusyProvider: () => IsBusy,
            setBusyState: value => IsBusy = value,
            openBundleAsync: _bundleTabCoordinator.OpenBundleAsync);
        _workspaceImportActionHandler = new BundleExplorerWorkspaceImportActionHandler(
            workspaceImportCoordinatorService,
            _workspaceImportProgress,
            _workspaceBindingCoordinator,
            hasWorkspaceProvider: () => _workspaceShell.HasWorkspace,
            isBusyProvider: () => IsBusy,
            setBusyState: value => IsBusy = value);
        _bundleActionHandler = new BundleExplorerBundleActionHandler(
            workspaceExplorerGateway,
            workspaceContextService,
            pathNavigationService,
            appLogService,
            localizationService,
            operationFeedbackBridge,
            hasWorkspaceProvider: () => _workspaceShell.HasWorkspace,
            isBusyProvider: () => IsBusy,
            setBusyState: value => IsBusy = value,
            reloadCurrentWorkspaceAsync: () => _workspaceBindingCoordinator.ReloadCurrentWorkspaceAsync(
                showLoadedNotification: false,
                allowBusyReentry: true));

        RefreshCommand = new AsyncRelayCommand(
            executeAsync: RefreshAsync,
            canExecute: () => _workspaceShell.HasWorkspace && !IsBusy);
        _bundlePane.UpdateInteractionState(_workspaceShell.HasWorkspace, IsBusy);
    }

    public BundleExplorerWorkspaceShellViewModel WorkspaceShell => _workspaceShell;

    public BundleExplorerBundlePaneViewModel BundlePane => _bundlePane;

    public BundleExplorerDocumentWorkspaceViewModel DocumentWorkspace => _documentWorkspace;

    public BundleExplorerAssetWorkflowViewModel AssetWorkflow => _assetWorkflow;

    public WorkspaceImportProgressViewModel WorkspaceImportProgress => _workspaceImportProgress;

    public AssetEditorWindowRestorePromptViewModel AssetEditorWindowRestorePrompt => _assetEditorWindowRestorePrompt;

    public AsyncRelayCommand RefreshCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _assetWorkflow.UpdateBusyState(value);
                _bundlePane.UpdateInteractionState(_workspaceShell.HasWorkspace, value);
                NotifyCommandStates();
            }
        }
    }

    /// <summary>
    /// 向页面层提供“当前实际已绑定工作区”的稳定导航参数。
    /// 这样窗口注册逻辑可以依赖成功加载后的结果，而不是依赖页面传入的临时请求。
    /// </summary>
    public bool TryGetCurrentWorkspaceNavigationRequest([NotNullWhen(true)] out WorkspaceNavigationRequest? workspaceNavigationRequest) =>
        _workspaceShell.TryGetCurrentWorkspaceNavigationRequest(out workspaceNavigationRequest);

    public async Task InitializeAsync(WorkspaceNavigationRequest? workspaceNavigationRequest = null)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        if (workspaceNavigationRequest is not null)
        {
            await _workspaceImportActionHandler.LoadWorkspaceWithImportOverlayAsync(clearExistingLogs: true, workspaceNavigationRequest);
            return;
        }

        if (_workspaceShell.TryGetCurrentWorkspaceSessionContext(out var currentWorkspaceSummary, out var currentNavigationRequest))
        {
            await _workspaceBindingCoordinator.BindWorkspaceAsync(
                clearExistingLogs: true,
                workspaceSummary: currentWorkspaceSummary,
                workspaceNavigationRequest: currentNavigationRequest);
            return;
        }

        _workspaceBindingCoordinator.ResetWorkspaceToEmptyState();
    }

    public async Task RefreshAsync()
    {
        if (_workspaceShell.WorkspaceSummary is not null)
        {
            await _workspaceBindingCoordinator.BindWorkspaceAsync(
                clearExistingLogs: false,
                workspaceSummary: _workspaceShell.WorkspaceSummary,
                workspaceNavigationRequest: _workspaceShell.ActiveWorkspaceNavigationRequest);
            return;
        }

        if (_workspaceShell.TryGetCurrentWorkspaceSessionContext(out var currentWorkspaceSummary, out var currentNavigationRequest))
        {
            await _workspaceBindingCoordinator.BindWorkspaceAsync(
                clearExistingLogs: true,
                workspaceSummary: currentWorkspaceSummary,
                workspaceNavigationRequest: currentNavigationRequest);
            return;
        }

        if (_workspaceShell.ActiveWorkspaceNavigationRequest is not null)
        {
            await _workspaceImportActionHandler.LoadWorkspaceWithImportOverlayAsync(
                clearExistingLogs: true,
                _workspaceShell.ActiveWorkspaceNavigationRequest);
            return;
        }

        _workspaceBindingCoordinator.ResetWorkspaceToEmptyState();
    }

    public async Task OpenBundleInTabAsync(BundleNodeViewModel bundleNode)
    {
        await _bundleTabCoordinator.OpenBundleAsync(bundleNode, openAsPinnedTab: true);
    }

    public async Task HandlePrimaryPaneSelectionChangedAsync()
    {
        await _assetWorkflow.HandlePrimaryPaneSelectionChangedAsync();
    }

    public Task ActivateAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab) =>
        _assetWorkflow.ActivateAssetDocumentTabAsync(assetTab);

    public Task ActivateSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab) =>
        _assetWorkflow.ActivateSpineGroupDocumentTabAsync(spineGroupTab);

    public Task SelectActiveSpineGroupAtlasPageAsync(SpineAtlasPageOptionViewModel? atlasPageOption) =>
        _assetWorkflow.SelectActiveSpineGroupAtlasPageAsync(atlasPageOption);

    public Task OpenResolvedAssetAsync(ResolvedAssetNavigationTarget? target) =>
        _resolvedAssetNavigationCoordinator.OpenResolvedAssetAsync(target);

    public Task CloseAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab) =>
        _assetWorkflow.CloseAssetDocumentTabAsync(assetTab);

    public Task CloseSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab) =>
        _assetWorkflow.CloseSpineGroupDocumentTabAsync(spineGroupTab);

    public Task<SpineAssetGroupDocumentTabViewModel?> DetachSpineGroupDocumentTabForEditorWindowAsync(
        SpineAssetGroupDocumentTabViewModel? spineGroupTab) =>
        _assetWorkflow.DetachSpineGroupDocumentTabForExternalHostAsync(spineGroupTab);

    public Task RestoreDetachedSpineGroupDocumentTabFromEditorWindowAsync(
        SpineAssetGroupDocumentTabViewModel spineGroupTab) =>
        _assetWorkflow.RestoreDetachedSpineGroupDocumentTabAsync(spineGroupTab);

    /// <summary>
    /// 在当前活动资源包下，仅保留指定资产详情标签。
    /// 这样可以快速收拢右侧详情区，同时保留用户明确选中的目标上下文。
    /// </summary>
    public Task CloseOtherAssetDocumentTabsAsync(AssetDocumentTabViewModel? assetTab) =>
        _assetWorkflow.CloseOtherAssetDocumentTabsAsync(assetTab);

    /// <summary>
    /// 清空当前活动资源包下所有已打开的资产详情标签。
    /// 关闭后同时清空列表焦点，避免旧选中项立刻把详情标签重新打开。
    /// </summary>
    public Task CloseAllAssetDocumentTabsAsync() => _assetWorkflow.CloseAllAssetDocumentTabsAsync();

    public Task<AssetDocumentTabViewModel?> DetachAssetDocumentTabForEditorWindowAsync(
        AssetDocumentTabViewModel? assetTab) =>
        _assetDocumentMoveCoordinator.DetachAssetDocumentTabForExternalHostAsync(assetTab);

    public Task<bool> AttachAssetDocumentTabFromEditorWindowAsync(AssetDocumentTabViewModel assetTab) =>
        _assetDocumentMoveCoordinator.AttachAssetDocumentTabFromExternalHostAsync(assetTab);

    public Task<AssetDocumentTabItemViewModel?> DetachDocumentTabForEditorWindowAsync(
        AssetDocumentTabItemViewModel? documentTab) =>
        _assetDocumentMoveCoordinator.DetachDocumentTabForExternalHostAsync(documentTab);

    public Task<bool> AttachDocumentTabFromEditorWindowAsync(AssetDocumentTabItemViewModel documentTab) =>
        _assetDocumentMoveCoordinator.AttachDocumentTabFromExternalHostAsync(documentTab);

    public Task<AssetDocumentGroupTransfer?> DetachActiveAssetDocumentGroupForEditorWindowAsync() =>
        _assetDocumentMoveCoordinator.DetachActiveAssetDocumentGroupForExternalHostAsync();

    public Task<bool> AttachAssetDocumentGroupFromEditorWindowAsync(AssetDocumentGroupTransfer transfer) =>
        _assetDocumentMoveCoordinator.AttachAssetDocumentGroupFromExternalHostAsync(transfer);

    public Task RefreshPrimaryPaneAsync(bool preserveCurrentDocumentTab = false) =>
        _assetWorkflow.RefreshPrimaryPaneAsync(preserveCurrentDocumentTab);

    public void FlushWorkspaceBrowserState()
    {
        _browserStateCoordinator.PersistWorkspaceBrowserStateNow();
    }

    public void SetAssetEditorWindowStateSnapshotProvider(
        Func<IReadOnlyList<WorkspaceAssetEditorWindowStateDto>>? snapshotProvider)
    {
        _captureOpenAssetEditorWindows = snapshotProvider
            ?? (() => Array.Empty<WorkspaceAssetEditorWindowStateDto>());
    }

    public void RequestAssetEditorWindowStatePersistence()
    {
        RequestWorkspaceBrowserStatePersistence();
    }

    public Task RefreshAssetEditorWindowDraftRestorePlanAsync(CancellationToken cancellationToken = default) =>
        _assetEditorWindowDraftRestorePlanCoordinator.RefreshAsync(cancellationToken);

    public async Task ActivateBundleTabAsync(BundleDocumentTabViewModel? tab)
    {
        await _bundleTabCoordinator.ActivateBundleTabAsync(tab);
    }

    /// <summary>
    /// 资源包标签顺序属于工作台页面级浏览状态。
    /// 视图层拖拽完成后只上报最终顺序，真正的集合重排和后续状态持久化在这里统一处理。
    /// </summary>
    public void ReorderBundleTabs(IReadOnlyList<BundleDocumentTabViewModel> orderedTabs)
    {
        _bundleTabCoordinator.ReorderBundleTabs(orderedTabs);
    }

    /// <summary>
    /// 右侧资产详情标签顺序归属当前活动资源包标签页。
    /// 拖拽重排后保持当前活动标签不变，只更新集合顺序并写回工作区浏览状态。
    /// </summary>
    public void ReorderActiveAssetDocumentTabs(IReadOnlyList<AssetDocumentTabViewModel> orderedTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedTabs);

        if (!_documentWorkspace.ReorderActiveAssetTabs(orderedTabs))
        {
            return;
        }

        RequestWorkspaceBrowserStatePersistence();
    }

    public void ReorderActiveSpineGroupDocumentTabs(IReadOnlyList<SpineAssetGroupDocumentTabViewModel> orderedTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedTabs);

        if (!_documentWorkspace.ReorderActiveSpineGroupTabs(orderedTabs))
        {
            return;
        }

        RequestWorkspaceBrowserStatePersistence();
    }

    public void ReorderActiveDocumentTabs(IReadOnlyList<AssetDocumentTabItemViewModel> orderedTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedTabs);

        if (!_documentWorkspace.ReorderActiveDocumentTabs(orderedTabs))
        {
            return;
        }

        RequestWorkspaceBrowserStatePersistence();
    }

    public async Task CloseBundleTabAsync(BundleDocumentTabViewModel? tab)
    {
        await _bundleTabCoordinator.CloseBundleTabAsync(tab);
    }

    /// <summary>
    /// 保留指定资源包标签，关闭当前工作区中的其他资源包标签。
    /// 如果保留目标当前并非活动标签，也会一并切换过去，避免关闭后焦点落到不可预期的位置。
    /// </summary>
    public async Task CloseOtherBundleTabsAsync(BundleDocumentTabViewModel? tab)
    {
        await _bundleTabCoordinator.CloseOtherBundleTabsAsync(tab);
    }

    /// <summary>
    /// 关闭当前工作区中所有资源包标签，并把页面浏览上下文重置回“未打开标签”的稳定状态。
    /// </summary>
    public Task CloseAllBundleTabsAsync()
    {
        return _bundleTabCoordinator.CloseAllBundleTabsAsync();
    }

    /// <summary>
    /// 切换左侧资源包搜索框的展开状态。
    /// 收起时同步清空关键字，确保列表快速回到完整浏览状态。
    /// </summary>
    public void ToggleBundleSearch()
    {
        _bundlePane.ToggleBundleSearch();
    }

    /// <summary>
    /// 在完整浏览视图与紧凑摘要窄条之间切换左栏形态。
    /// 收缩时同步关闭搜索，避免隐藏输入态残留在页面状态里。
    /// </summary>
    public void ToggleBundlePaneCollapsed()
    {
        _bundlePane.ToggleBundlePaneCollapsed();
    }

    /// <summary>
    /// 资源包导入入口统一交给共享协调服务处理。
    /// 当前阶段只收集来源并记录标准化反馈，为后续真实导入链路预留稳定边界。
    /// </summary>
    public Task RequestBundleFileImportAsync(CancellationToken cancellationToken = default) =>
        _workspaceImportActionHandler.RequestBundleFileImportAsync(cancellationToken);

    /// <summary>
    /// 目录导入与文件导入共用同一套协调链路，避免页面散落各自的导入前置逻辑。
    /// </summary>
    public Task RequestBundleDirectoryImportAsync(CancellationToken cancellationToken = default) =>
        _workspaceImportActionHandler.RequestBundleDirectoryImportAsync(cancellationToken);

    public Task RequestDragDropImportAsync(
        IReadOnlyList<string> droppedPaths,
        CancellationToken cancellationToken = default) =>
        _workspaceImportActionHandler.RequestDragDropImportAsync(droppedPaths, cancellationToken);

    public void OpenBundleSourceLocation(BundleNodeViewModel bundleNode) =>
        _bundleActionHandler.OpenBundleSourceLocation(bundleNode);

    /// <summary>
    /// 预览文件定位属于当前详情标签页的通用预览动作。
    /// 这里统一读取当前检查器持有的预览缓存 payload 路径，避免图片、音频、文本各自复制一套打开逻辑。
    /// </summary>
    public void OpenCurrentPreviewLocation() => _assetWorkflow.OpenCurrentPreviewLocation();

    public Task RemoveBundleFromWorkspaceAsync(BundleNodeViewModel bundleNode) =>
        _bundleActionHandler.RemoveBundleFromWorkspaceAsync(bundleNode);

    public Task ExtractSelectedAssetAsync() => _assetWorkflow.ExtractSelectedAssetAsync();

    /// <summary>
    /// 处理资产列表中的多选快捷提取。
    /// 当前先复用单资源提取用例逐项执行，并在页面层汇总结果提示，
    /// 后续如果批量提取演进为独立用例，再把编排逻辑下沉到 Application。
    /// </summary>
    public Task ExtractSelectedAssetsAsync() => _assetWorkflow.ExtractSelectedAssetsAsync();

    public Task SelectWorkspaceReplacementSourceAsync() => _assetWorkflow.SelectWorkspaceReplacementSourceAsync();

    public Task SelectFileReplacementSourceAsync() => _assetWorkflow.SelectFileReplacementSourceAsync();

    public Task ApplyPendingReplacementAsync() => _assetWorkflow.ApplyPendingReplacementAsync();

    public Task UndoAppliedReplacementAsync() => _assetWorkflow.UndoAppliedReplacementAsync();

    /// <summary>
    /// 页面级交互仍有多处历史入口会请求浏览状态持久化。
    /// 这里保留一层薄封装，把触发统一转交给专用协调对象。
    /// </summary>
    private void RequestWorkspaceBrowserStatePersistence()
    {
        _browserStateCoordinator.RequestWorkspaceBrowserStatePersistence();
    }

    private void OnWorkspaceShellPageStateChanged(
        object? sender,
        BundleExplorerPageStateChangedEventArgs<BundleExplorerWorkspaceShellPageStateChange> eventArgs)
    {
        if (!eventArgs.ChangeFlags.HasFlag(BundleExplorerWorkspaceShellPageStateChange.WorkspaceAvailability))
        {
            return;
        }

        _bundlePane.UpdateInteractionState(_workspaceShell.HasWorkspace, IsBusy);
        NotifyCommandStates();
    }

    private void OnBundlePaneBrowserStateChanged(object? sender, EventArgs eventArgs)
    {
        RequestWorkspaceBrowserStatePersistence();
    }

    private void NotifyCommandStates()
    {
        RefreshCommand.NotifyCanExecuteChanged();
    }
}

