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

    public Task<AssetDocumentTabItemViewModel?> DetachDocumentTabForExternalHostAsync(
        AssetDocumentTabItemViewModel? documentTab) =>
        _assetWorkflow.DetachDocumentTabForExternalHostAsync(documentTab);

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

    public Task<bool> AttachDocumentTabFromExternalHostAsync(AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);

        return AttachAssetDocumentGroupFromExternalHostAsync(
            new AssetDocumentGroupTransfer([documentTab], documentTab));
    }

    public async Task<bool> AttachAssetDocumentGroupFromExternalHostAsync(AssetDocumentGroupTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);

        if (!_workspaceShell.HasWorkspace || _isBusyProvider() || !CanResolveAllBundleTabs(transfer))
        {
            return false;
        }

        AssetDocumentTabItemViewModel? activeDocumentTab = null;
        foreach (var documentTab in transfer.DocumentTabs)
        {
            var attachedDocumentTab = await AttachDocumentTabWithoutActivationAsync(documentTab);
            if (ReferenceEquals(documentTab, transfer.ActiveDocumentTab))
            {
                activeDocumentTab = attachedDocumentTab;
            }
        }

        if (activeDocumentTab is not null &&
            _documentWorkspace.TryGetBundleTab(activeDocumentTab.BundleId, out var activeBundleTab))
        {
            await _bundleTabCoordinator.ActivateBundleTabAsync(activeBundleTab);
            if (activeDocumentTab.AssetTab is { } activeAssetTab)
            {
                await _assetWorkflow.ActivateAssetDocumentTabAsync(activeAssetTab);
            }
            else if (activeDocumentTab.SpineGroupTab is { } activeSpineGroupTab)
            {
                await _assetWorkflow.ActivateSpineGroupDocumentTabAsync(activeSpineGroupTab);
            }
        }

        _requestWorkspaceBrowserStatePersistence();
        return true;
    }

    private bool CanResolveAllBundleTabs(AssetDocumentGroupTransfer transfer)
    {
        foreach (var bundleId in transfer.DocumentTabs.Select(static tab => tab.BundleId).Distinct())
        {
            if (_documentWorkspace.TryGetBundleTab(bundleId, out _) ||
                _bundlePane.FindBundleNode(bundleId) is not null)
            {
                continue;
            }

            ShowDocumentMoveBackWarning(transfer.ActiveDocumentTab);
            return false;
        }

        return true;
    }

    private async Task<AssetDocumentTabItemViewModel> AttachDocumentTabWithoutActivationAsync(
        AssetDocumentTabItemViewModel documentTab)
    {
        if (documentTab.AssetTab is { } assetTab)
        {
            var attachedAssetTab = await AttachAssetDocumentTabWithoutActivationAsync(assetTab);
            return _documentWorkspace.TryGetBundleTab(attachedAssetTab.BundleId, out var assetBundleTab) &&
                   assetBundleTab.DocumentTabs.FirstOrDefault(tab => tab.Wraps(attachedAssetTab)) is { } attachedDocumentTab
                ? attachedDocumentTab
                : documentTab;
        }

        if (documentTab.SpineGroupTab is { } spineGroupTab)
        {
            var attachedSpineGroupTab = await AttachSpineGroupDocumentTabWithoutActivationAsync(spineGroupTab);
            return _documentWorkspace.TryGetBundleTab(attachedSpineGroupTab.BundleId, out var spineBundleTab) &&
                   spineBundleTab.DocumentTabs.FirstOrDefault(tab => tab.Wraps(attachedSpineGroupTab)) is { } attachedDocumentTab
                ? attachedDocumentTab
                : documentTab;
        }

        return documentTab;
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

    private async Task<SpineAssetGroupDocumentTabViewModel> AttachSpineGroupDocumentTabWithoutActivationAsync(
        SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        if (!_documentWorkspace.TryGetBundleTab(spineGroupTab.BundleId, out var bundleTab))
        {
            var bundleNode = _bundlePane.FindBundleNode(spineGroupTab.BundleId);
            if (bundleNode is not null)
            {
                await _bundleTabCoordinator.OpenBundleAsync(bundleNode, openAsPinnedTab: true);
            }

            _documentWorkspace.TryGetBundleTab(spineGroupTab.BundleId, out bundleTab);
        }

        if (bundleTab is null)
        {
            return spineGroupTab;
        }

        if (bundleTab.TryGetSpineGroupTab(spineGroupTab.GroupId, out var existingSpineGroupTab))
        {
            return existingSpineGroupTab;
        }

        bundleTab.AddSpineGroupTab(spineGroupTab, activate: false);
        return spineGroupTab;
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

    private void ShowDocumentMoveBackWarning(AssetDocumentTabItemViewModel documentTab)
    {
        var message = _localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerNotificationResolvedAssetBundleUnavailableFormat,
            string.IsNullOrWhiteSpace(documentTab.DisplayName) ? documentTab.DocumentId : documentTab.DisplayName);
        _appLogService.AppendWarning(message);
        _operationFeedbackBridge.ShowOperationNotification(
            GetString(ResourceKeys.AppNotificationWarningTitle),
            message,
            AppNotificationSeverity.Warning);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
