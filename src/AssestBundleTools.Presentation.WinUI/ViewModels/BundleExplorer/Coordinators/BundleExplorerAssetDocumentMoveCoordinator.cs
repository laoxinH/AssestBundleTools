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

    public Task<AssetDocumentGroupTransfer?> DetachActiveAssetDocumentGroupForExternalHostAsync()
    {
        if (_isBusyProvider())
        {
            return Task.FromResult<AssetDocumentGroupTransfer?>(null);
        }

        return _assetWorkflow.DetachActiveAssetDocumentGroupForExternalHostAsync();
    }

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

    public async Task<bool> AttachAssetDocumentGroupFromExternalHostAsync(AssetDocumentGroupTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        if (!_workspaceShell.HasWorkspace || _isBusyProvider() || !CanResolveAllBundleTabs(transfer))
        {
            return false;
        }

        AssetDocumentTabViewModel? activeAssetTab = null;
        foreach (var assetTab in transfer.AssetTabs)
        {
            var attachedAssetTab = await AttachAssetDocumentTabWithoutActivationAsync(assetTab);
            if (ReferenceEquals(assetTab, transfer.ActiveAssetTab))
            {
                activeAssetTab = attachedAssetTab;
            }
        }

        if (activeAssetTab is not null &&
            _documentWorkspace.TryGetBundleTab(activeAssetTab.BundleId, out var activeBundleTab))
        {
            await _bundleTabCoordinator.ActivateBundleTabAsync(activeBundleTab);
            await _assetWorkflow.ActivateAssetDocumentTabAsync(activeAssetTab);
        }

        _requestWorkspaceBrowserStatePersistence();
        return true;
    }

    private bool CanResolveAllBundleTabs(AssetDocumentGroupTransfer transfer)
    {
        foreach (var bundleId in transfer.AssetTabs.Select(static tab => tab.BundleId).Distinct())
        {
            if (_documentWorkspace.TryGetBundleTab(bundleId, out _) ||
                _bundlePane.FindBundleNode(bundleId) is not null)
            {
                continue;
            }

            ShowAssetDocumentMoveBackWarning(transfer.ActiveAssetTab);
            return false;
        }

        return true;
    }

    private async Task<AssetDocumentTabViewModel> AttachAssetDocumentTabWithoutActivationAsync(
        AssetDocumentTabViewModel assetTab)
    {
        if (!_documentWorkspace.TryGetBundleTab(assetTab.BundleId, out var bundleTab))
        {
            var bundleNode = _bundlePane.FindBundleNode(assetTab.BundleId);
            if (bundleNode is not null)
            {
                await _bundleTabCoordinator.OpenBundleAsync(bundleNode, openAsPinnedTab: true);
            }

            _documentWorkspace.TryGetBundleTab(assetTab.BundleId, out bundleTab);
        }

        if (bundleTab is null)
        {
            return assetTab;
        }

        if (bundleTab.TryGetAssetTab(assetTab.AssetId, out var existingAssetTab))
        {
            assetTab.CancelPreviewRequest();
            return existingAssetTab;
        }

        bundleTab.AddAssetTab(assetTab, activate: false);
        return assetTab;
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
