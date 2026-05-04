using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal sealed class BundleExplorerResolvedAssetNavigationCoordinator
{
    private readonly BundleExplorerWorkspaceShellViewModel _workspaceShell;
    private readonly BundleExplorerBundlePaneViewModel _bundlePane;
    private readonly BundleExplorerDocumentWorkspaceViewModel _documentWorkspace;
    private readonly BundleExplorerBundleTabCoordinator _bundleTabCoordinator;
    private readonly BundleExplorerAssetWorkflowViewModel _assetWorkflow;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly Func<bool> _isBusyProvider;

    public BundleExplorerResolvedAssetNavigationCoordinator(
        BundleExplorerWorkspaceShellViewModel workspaceShell,
        BundleExplorerBundlePaneViewModel bundlePane,
        BundleExplorerDocumentWorkspaceViewModel documentWorkspace,
        BundleExplorerBundleTabCoordinator bundleTabCoordinator,
        BundleExplorerAssetWorkflowViewModel assetWorkflow,
        BundleExplorerOperationFeedbackBridge operationFeedbackBridge,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        Func<bool> isBusyProvider)
    {
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _bundleTabCoordinator = bundleTabCoordinator ?? throw new ArgumentNullException(nameof(bundleTabCoordinator));
        _assetWorkflow = assetWorkflow ?? throw new ArgumentNullException(nameof(assetWorkflow));
        _operationFeedbackBridge = operationFeedbackBridge ?? throw new ArgumentNullException(nameof(operationFeedbackBridge));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
    }

    public async Task OpenResolvedAssetAsync(ResolvedAssetNavigationTarget? target)
    {
        if (target is null)
        {
            return;
        }

        if (_isBusyProvider())
        {
            ShowWarning(GetString(ResourceKeys.BundleExplorerNotificationResolvedAssetNavigationBusy));
            return;
        }

        if (!_workspaceShell.HasWorkspace)
        {
            ShowWarning(GetString(ResourceKeys.BundleExplorerNotificationResolvedAssetNavigationNoWorkspace));
            return;
        }

        if (!_documentWorkspace.TryGetBundleTab(target.BundleId, out var bundleTab))
        {
            var bundleNode = _bundlePane.FindBundleNode(target.BundleId);
            if (bundleNode is null)
            {
                ShowWarning(_localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationResolvedAssetBundleUnavailableFormat,
                    ResolveDisplayName(target)));
                return;
            }

            await _bundleTabCoordinator.OpenBundleAsync(bundleNode, openAsPinnedTab: true);
        }
        else if (!ReferenceEquals(_documentWorkspace.ActiveBundleTab, bundleTab))
        {
            await _bundleTabCoordinator.ActivateBundleTabAsync(bundleTab);
        }

        if (!await _assetWorkflow.OpenResolvedAssetAsync(target))
        {
            ShowWarning(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerNotificationResolvedAssetNavigationUnavailableFormat,
                ResolveDisplayName(target)));
        }
    }

    private void ShowWarning(string message)
    {
        _appLogService.AppendWarning(message);
        _operationFeedbackBridge.ShowOperationNotification(
            GetString(ResourceKeys.AppNotificationWarningTitle),
            message,
            AppNotificationSeverity.Warning);
    }

    private static string ResolveDisplayName(ResolvedAssetNavigationTarget target)
    {
        return string.IsNullOrWhiteSpace(target.DisplayName)
            ? target.AssetId.Value
            : target.DisplayName.Trim();
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
