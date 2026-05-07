using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 聚合资源包节点上的上下文动作，保持页面门面只暴露入口。
/// </summary>
internal sealed class BundleExplorerBundleActionHandler
{
    private readonly IWorkspaceExplorerGateway _workspaceExplorerGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IPathNavigationService _pathNavigationService;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly Func<bool> _hasWorkspaceProvider;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action<bool> _setBusyState;
    private readonly Func<Task> _reloadCurrentWorkspaceAsync;

    public BundleExplorerBundleActionHandler(
        IWorkspaceExplorerGateway workspaceExplorerGateway,
        IWorkspaceContextService workspaceContextService,
        IPathNavigationService pathNavigationService,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        BundleExplorerOperationFeedbackBridge operationFeedbackBridge,
        Func<bool> hasWorkspaceProvider,
        Func<bool> isBusyProvider,
        Action<bool> setBusyState,
        Func<Task> reloadCurrentWorkspaceAsync)
    {
        _workspaceExplorerGateway = workspaceExplorerGateway ?? throw new ArgumentNullException(nameof(workspaceExplorerGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _pathNavigationService = pathNavigationService ?? throw new ArgumentNullException(nameof(pathNavigationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _operationFeedbackBridge = operationFeedbackBridge ?? throw new ArgumentNullException(nameof(operationFeedbackBridge));
        _hasWorkspaceProvider = hasWorkspaceProvider ?? throw new ArgumentNullException(nameof(hasWorkspaceProvider));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _setBusyState = setBusyState ?? throw new ArgumentNullException(nameof(setBusyState));
        _reloadCurrentWorkspaceAsync = reloadCurrentWorkspaceAsync ?? throw new ArgumentNullException(nameof(reloadCurrentWorkspaceAsync));
    }

    public void OpenBundleSourceLocation(BundleNodeViewModel bundleNode)
    {
        ArgumentNullException.ThrowIfNull(bundleNode);

        if (_pathNavigationService.TryOpenContainingFolder(bundleNode.SourcePath))
        {
            return;
        }

        var message = _localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerNotificationBundleOpenLocationFailed,
            bundleNode.DisplayName);
        _appLogService.AppendWarning(message);
        _operationFeedbackBridge.ShowOperationNotification(
            GetString(ResourceKeys.AppNotificationWarningTitle),
            message,
            AppNotificationSeverity.Warning);
    }

    public async Task RemoveBundleFromWorkspaceAsync(BundleNodeViewModel bundleNode)
    {
        ArgumentNullException.ThrowIfNull(bundleNode);

        if (_isBusyProvider() ||
            !_hasWorkspaceProvider() ||
            !_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) ||
            workspaceEntry is null)
        {
            return;
        }

        _setBusyState(true);
        try
        {
            var result = await _workspaceExplorerGateway.RemoveWorkspaceBundleAsync(
                new RemoveWorkspaceBundleCommand(
                    WorkspaceKey: workspaceEntry.WorkspaceKey,
                    WorkspaceDataRootPath: workspaceEntry.WorkspaceDataRootPath,
                    BundleId: bundleNode.BundleId,
                    BundleSourcePath: bundleNode.SourcePath));

            if (!_operationFeedbackBridge.TryGetValue(result, out _))
            {
                return;
            }

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogBundleRemoved,
                bundleNode.DisplayName));

            _operationFeedbackBridge.ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationBundleRemoved,
                    bundleNode.DisplayName),
                AppNotificationSeverity.Success);

            await _reloadCurrentWorkspaceAsync();
        }
        finally
        {
            _setBusyState(false);
        }
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
