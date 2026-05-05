using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责资产文档标签在主窗口与外部宿主之间的移交。
/// </summary>
internal sealed class BundleExplorerAssetDocumentMoveCoordinator
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
    private readonly Action _requestWorkspaceBrowserStatePersistence;

    public BundleExplorerAssetDocumentMoveCoordinator(
        BundleExplorerWorkspaceShellViewModel workspaceShell,
        BundleExplorerBundlePaneViewModel bundlePane,
        BundleExplorerDocumentWorkspaceViewModel documentWorkspace,
        BundleExplorerBundleTabCoordinator bundleTabCoordinator,
        BundleExplorerAssetWorkflowViewModel assetWorkflow,
        BundleExplorerOperationFeedbackBridge operationFeedbackBridge,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        Func<bool> isBusyProvider,
        Action requestWorkspaceBrowserStatePersistence)
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
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
    }

    public Task<AssetDocumentTabViewModel?> DetachAssetDocumentTabForExternalHostAsync(
        AssetDocumentTabViewModel? assetTab) =>
        _assetWorkflow.DetachAssetDocumentTabForExternalHostAsync(assetTab);

    public async Task<bool> AttachAssetDocumentTabFromExternalHostAsync(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!_workspaceShell.HasWorkspace || _isBusyProvider())
        {
            return false;
        }

        if (!_documentWorkspace.TryGetBundleTab(assetTab.BundleId, out var bundleTab))
        {
            var bundleNode = _bundlePane.FindBundleNode(assetTab.BundleId);
            if (bundleNode is null)
            {
                ShowAssetDocumentMoveBackWarning(assetTab);
                return false;
            }

            await _bundleTabCoordinator.OpenBundleAsync(bundleNode, openAsPinnedTab: true);
            if (!_documentWorkspace.TryGetBundleTab(assetTab.BundleId, out bundleTab))
            {
                ShowAssetDocumentMoveBackWarning(assetTab);
                return false;
            }
        }
        else if (!ReferenceEquals(_documentWorkspace.ActiveBundleTab, bundleTab))
        {
            await _bundleTabCoordinator.ActivateBundleTabAsync(bundleTab);
        }

        if (bundleTab.TryGetAssetTab(assetTab.AssetId, out var existingAssetTab))
        {
            assetTab.CancelPreviewRequest();
            await _assetWorkflow.ActivateAssetDocumentTabAsync(existingAssetTab);
            _requestWorkspaceBrowserStatePersistence();
            return true;
        }

        bundleTab.AddAssetTab(assetTab, activate: false);
        await _assetWorkflow.ActivateAssetDocumentTabAsync(assetTab);
        _requestWorkspaceBrowserStatePersistence();
        return true;
    }

    private void ShowAssetDocumentMoveBackWarning(AssetDocumentTabViewModel assetTab)
    {
        var message = _localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerNotificationResolvedAssetBundleUnavailableFormat,
            string.IsNullOrWhiteSpace(assetTab.DisplayName) ? assetTab.AssetId.Value : assetTab.DisplayName);
        _appLogService.AppendWarning(message);
        _operationFeedbackBridge.ShowOperationNotification(
            GetString(ResourceKeys.AppNotificationWarningTitle),
            message,
            AppNotificationSeverity.Warning);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
