using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责工作台页面的工作区加载、重绑定与首屏恢复编排。
/// </summary>
/// <remarks>
/// 这里集中承接“进入工作区之后，页面该如何完成壳层绑定、左侧树重建、
/// 标签恢复和默认首屏选择”的流程控制，避免主页面 ViewModel 继续堆积长链路编排代码。
/// </remarks>
internal sealed class BundleExplorerWorkspaceBindingCoordinator
{
    private readonly IWorkspaceExplorerGateway _workspaceExplorerGateway;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly BundleExplorerWorkspaceShellViewModel _workspaceShell;
    private readonly BundleExplorerBundlePaneViewModel _bundlePane;
    private readonly BundleExplorerDocumentWorkspaceViewModel _documentWorkspace;
    private readonly IBundleExplorerAssetWorkflowBindingHost _assetWorkflow;
    private readonly IBundleExplorerBrowserStateCoordinator _browserStateCoordinator;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action<bool> _setBusyState;
    private readonly Func<BundleNodeViewModel, bool, Task> _openBundleAsync;
    private readonly record struct WorkspaceRestorePoint(
        WorkspaceSummaryDto WorkspaceSummary,
        WorkspaceNavigationRequest WorkspaceNavigationRequest);

    public BundleExplorerWorkspaceBindingCoordinator(
        IWorkspaceExplorerGateway workspaceExplorerGateway,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        IWorkspaceContextService workspaceContextService,
        BundleExplorerOperationFeedbackBridge operationFeedbackBridge,
        IBackgroundTaskRunner backgroundTaskRunner,
        BundleExplorerWorkspaceShellViewModel workspaceShell,
        BundleExplorerBundlePaneViewModel bundlePane,
        BundleExplorerDocumentWorkspaceViewModel documentWorkspace,
        IBundleExplorerAssetWorkflowBindingHost assetWorkflow,
        IBundleExplorerBrowserStateCoordinator browserStateCoordinator,
        Func<bool> isBusyProvider,
        Action<bool> setBusyState,
        Func<BundleNodeViewModel, bool, Task> openBundleAsync)
    {
        _workspaceExplorerGateway = workspaceExplorerGateway ?? throw new ArgumentNullException(nameof(workspaceExplorerGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _operationFeedbackBridge = operationFeedbackBridge ?? throw new ArgumentNullException(nameof(operationFeedbackBridge));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _assetWorkflow = assetWorkflow ?? throw new ArgumentNullException(nameof(assetWorkflow));
        _browserStateCoordinator = browserStateCoordinator ?? throw new ArgumentNullException(nameof(browserStateCoordinator));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _setBusyState = setBusyState ?? throw new ArgumentNullException(nameof(setBusyState));
        _openBundleAsync = openBundleAsync ?? throw new ArgumentNullException(nameof(openBundleAsync));
    }

    public async Task LoadWorkspaceAsync(
        bool clearExistingLogs,
        WorkspaceNavigationRequest? workspaceNavigationRequest,
        IProgress<WorkspaceImportProgressDto>? importProgress = null,
        bool showLoadedNotification = true,
        bool allowBusyReentry = false)
    {
        if (_isBusyProvider() && !allowBusyReentry)
        {
            return;
        }

        var managesBusyState = !_isBusyProvider();
        if (managesBusyState)
        {
            _setBusyState(true);
        }

        var restorePoint = workspaceNavigationRequest?.ForceNewWorkspaceSession == true
            ? null
            : CaptureWorkspaceRestorePoint();
        try
        {
            if (clearExistingLogs)
            {
                _appLogService.Clear();
            }

            var resolvedNavigationRequest = _workspaceShell.ResolveNavigationRequest(workspaceNavigationRequest);
            if (resolvedNavigationRequest is null)
            {
                await RestoreWorkspaceAsync(restorePoint);
                return;
            }

            _appLogService.AppendInfo(GetString(ResourceKeys.BundleExplorerLogInitializing));

            var openWorkspaceCommand = new OpenWorkspaceCommand(
                WorkspacePath: resolvedNavigationRequest.SourcePath,
                WorkspaceDataRootPath: resolvedNavigationRequest.WorkspaceDataRootPath,
                WorkspaceDisplayName: resolvedNavigationRequest.WorkspaceDisplayName,
                InitialMode: WorkspaceMode.SinglePane,
                ImportProgress: importProgress);
            var openResult = await _backgroundTaskRunner.RunAsync(
                token => _workspaceExplorerGateway.OpenWorkspaceAsync(openWorkspaceCommand, token));

            if (!_operationFeedbackBridge.TryGetValue(openResult, out var workspaceSummary))
            {
                await RestoreWorkspaceAsync(restorePoint);
                return;
            }

            if (!await ApplyWorkspaceAsync(workspaceSummary, resolvedNavigationRequest, showLoadedNotification))
            {
                await RestoreWorkspaceAsync(restorePoint);
            }
        }
        finally
        {
            if (managesBusyState)
            {
                _setBusyState(false);
            }
        }
    }

    /// <summary>
    /// 在不重新创建工作区会话的前提下，把页面重新绑定到已有活动工作区。
    /// 该流程主要服务于页面回切恢复与当前会话刷新。
    /// </summary>
    public async Task BindWorkspaceAsync(
        bool clearExistingLogs,
        WorkspaceSummaryDto workspaceSummary,
        WorkspaceNavigationRequest? workspaceNavigationRequest,
        bool showLoadedNotification = true)
    {
        if (_isBusyProvider())
        {
            return;
        }

        _setBusyState(true);
        var restorePoint = CaptureWorkspaceRestorePoint();
        try
        {
            if (clearExistingLogs)
            {
                _appLogService.Clear();
            }

            _appLogService.AppendInfo(GetString(ResourceKeys.BundleExplorerLogInitializing));

            var resolvedNavigationRequest = _workspaceShell.ResolveNavigationRequest(workspaceNavigationRequest);
            if (resolvedNavigationRequest is null)
            {
                await RestoreWorkspaceAsync(restorePoint);
                return;
            }

            if (!await ApplyWorkspaceAsync(workspaceSummary, resolvedNavigationRequest, showLoadedNotification))
            {
                await RestoreWorkspaceAsync(restorePoint);
            }
        }
        finally
        {
            _setBusyState(false);
        }
    }

    public async Task ReloadCurrentWorkspaceAsync(
        bool showLoadedNotification,
        bool allowBusyReentry = false)
    {
        if (_workspaceShell.ActiveWorkspaceNavigationRequest is null)
        {
            return;
        }

        await LoadWorkspaceAsync(
            clearExistingLogs: false,
            workspaceNavigationRequest: _workspaceShell.ActiveWorkspaceNavigationRequest,
            importProgress: null,
            showLoadedNotification: showLoadedNotification,
            allowBusyReentry: allowBusyReentry);
    }

    public void ResetWorkspaceToEmptyState()
    {
        _browserStateCoordinator.CancelWorkspaceBrowserStatePersistence();
        _workspaceShell.Reset();
        _workspaceContextService.ClearCurrentWorkspace();
        _bundlePane.Clear();
        _documentWorkspace.Clear();
        _assetWorkflow.ClearActiveTabState();
    }

    private async Task<bool> ApplyWorkspaceAsync(
        WorkspaceSummaryDto workspaceSummary,
        WorkspaceNavigationRequest resolvedNavigationRequest,
        bool showLoadedNotification)
    {
        var bundleTreeResult = await _workspaceExplorerGateway.LoadBundleTreeAsync(
            new LoadBundleTreeQuery(workspaceSummary.WorkspaceId));

        if (!_operationFeedbackBridge.TryGetValue(bundleTreeResult, out var bundleTree))
        {
            return false;
        }

        var appliedNavigationRequest = _workspaceShell.ApplyWorkspace(workspaceSummary, resolvedNavigationRequest);
        _workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            appliedNavigationRequest.WorkspaceDataRootPath,
            appliedNavigationRequest.AssetBundleDecryptorId);

        _assetWorkflow.PrimaryPane.ConfigurePane(
            workspaceSummary.ActivePaneId,
            GetString(ResourceKeys.BundleExplorerPrimaryPaneTitle),
            GetString(ResourceKeys.BundleExplorerNoAssets));
        _bundlePane.RebuildBundleGroups(bundleTree);
        _documentWorkspace.Clear();

        if (!await _browserStateCoordinator.TryRestoreWorkspaceBrowserStateAsync())
        {
            var firstBundle = _bundlePane.GetFirstVisibleBundle();
            if (firstBundle is not null)
            {
                await _openBundleAsync(firstBundle, false);
            }
            else
            {
                _assetWorkflow.ClearActiveTabState();
            }
        }

        if (!showLoadedNotification)
        {
            return true;
        }

        var workspaceLoadedMessage = _localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogWorkspaceLoaded,
            workspaceSummary.DisplayName,
            workspaceSummary.BundleCount,
            workspaceSummary.AssetCount);

        _appLogService.AppendInfo(workspaceLoadedMessage);
        _operationFeedbackBridge.ShowOperationNotification(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerNotificationWorkspaceLoaded,
                workspaceSummary.DisplayName,
                workspaceSummary.BundleCount,
                workspaceSummary.AssetCount),
            AppNotificationSeverity.Success);
        return true;
    }

    private WorkspaceRestorePoint? CaptureWorkspaceRestorePoint()
    {
        if (_workspaceShell.WorkspaceSummary is not null &&
            _workspaceShell.ActiveWorkspaceNavigationRequest is not null)
        {
            return new WorkspaceRestorePoint(
                _workspaceShell.WorkspaceSummary,
                _workspaceShell.ActiveWorkspaceNavigationRequest);
        }

        if (_workspaceShell.TryGetCurrentWorkspaceSessionContext(out var workspaceSummary, out var workspaceNavigationRequest))
        {
            return new WorkspaceRestorePoint(workspaceSummary, workspaceNavigationRequest);
        }

        return null;
    }

    /// <summary>
    /// 工作区切换失败时，优先回到进入本轮加载前已经稳定存在的页面/会话上下文；
    /// 只有确实没有可恢复目标时，才退回空态。
    /// </summary>
    private async Task RestoreWorkspaceAsync(WorkspaceRestorePoint? restorePoint)
    {
        if (restorePoint is null)
        {
            ResetWorkspaceToEmptyState();
            return;
        }

        if (IsCurrentWorkspace(restorePoint.Value))
        {
            _workspaceContextService.RegisterOrActivateWorkspace(
                restorePoint.Value.WorkspaceSummary,
                restorePoint.Value.WorkspaceNavigationRequest.WorkspaceDataRootPath,
                restorePoint.Value.WorkspaceNavigationRequest.AssetBundleDecryptorId);
            return;
        }

        if (!await ApplyWorkspaceAsync(
                restorePoint.Value.WorkspaceSummary,
                restorePoint.Value.WorkspaceNavigationRequest,
                showLoadedNotification: false))
        {
            ResetWorkspaceToEmptyState();
        }
    }

    private bool IsCurrentWorkspace(WorkspaceRestorePoint restorePoint)
    {
        return string.Equals(
            _workspaceShell.WorkspaceSummary?.WorkspaceId.Value,
            restorePoint.WorkspaceSummary.WorkspaceId.Value,
            StringComparison.Ordinal);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
