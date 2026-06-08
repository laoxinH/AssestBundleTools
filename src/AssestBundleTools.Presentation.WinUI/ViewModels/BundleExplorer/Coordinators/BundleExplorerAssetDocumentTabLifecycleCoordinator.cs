using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责资产文档标签的关闭与跨宿主移出生命周期动作。
/// </summary>
internal sealed class BundleExplorerAssetDocumentTabLifecycleCoordinator
{
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly AssetPaneViewModel _primaryPane;
    private readonly BundleExplorerAssetDocumentHostCoordinator _assetDocumentHostCoordinator;
    private readonly Func<BundleDocumentTabViewModel, AssetDocumentTabViewModel, bool, bool, Task> _setActiveAssetDocumentTabAsync;
    private readonly Action _requestWorkspaceBrowserStatePersistence;
    private readonly Action _notifyActionStates;

    public BundleExplorerAssetDocumentTabLifecycleCoordinator(
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        AssetPaneViewModel primaryPane,
        BundleExplorerAssetDocumentHostCoordinator assetDocumentHostCoordinator,
        Func<BundleDocumentTabViewModel, AssetDocumentTabViewModel, bool, bool, Task> setActiveAssetDocumentTabAsync,
        Action requestWorkspaceBrowserStatePersistence,
        Action notifyActionStates)
    {
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _primaryPane = primaryPane ?? throw new ArgumentNullException(nameof(primaryPane));
        _assetDocumentHostCoordinator = assetDocumentHostCoordinator ?? throw new ArgumentNullException(nameof(assetDocumentHostCoordinator));
        _setActiveAssetDocumentTabAsync = setActiveAssetDocumentTabAsync ?? throw new ArgumentNullException(nameof(setActiveAssetDocumentTabAsync));
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
        _notifyActionStates = notifyActionStates ?? throw new ArgumentNullException(nameof(notifyActionStates));
    }

    public async Task CloseAssetDocumentTabAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return;
        }

        var ownerTab = _documentWorkspace.ActiveBundleTab;
        var removedIndex = ownerTab.AssetTabs.IndexOf(assetTab);
        if (removedIndex < 0)
        {
            return;
        }

        var wasActive = ReferenceEquals(ownerTab.ActiveAssetTab, assetTab);
        ownerTab.RemoveAssetTab(assetTab);

        if (!wasActive)
        {
            NotifyHostChangedAndPersist();
            return;
        }

        if (ownerTab.ActiveAssetTab is not null)
        {
            await _setActiveAssetDocumentTabAsync(ownerTab, ownerTab.ActiveAssetTab, true, false);
            return;
        }

        _primaryPane.ClearSelection();
        NotifyHostChangedAndPersist();
    }

    public async Task CloseOtherAssetDocumentTabsAsync(AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return;
        }

        var ownerTab = _documentWorkspace.ActiveBundleTab;
        if (!ownerTab.AssetTabs.Contains(assetTab))
        {
            return;
        }

        if (ownerTab.AssetTabs.Count <= 1)
        {
            if (!ReferenceEquals(ownerTab.ActiveAssetTab, assetTab))
            {
                await _setActiveAssetDocumentTabAsync(ownerTab, assetTab, true, false);
            }

            return;
        }

        var tabsToRemove = ownerTab.AssetTabs
            .Where(openedTab => !ReferenceEquals(openedTab, assetTab))
            .ToArray();

        foreach (var tabToRemove in tabsToRemove)
        {
            ownerTab.RemoveAssetTab(tabToRemove);
        }

        if (!ReferenceEquals(ownerTab.ActiveAssetTab, assetTab))
        {
            await _setActiveAssetDocumentTabAsync(ownerTab, assetTab, true, false);
            return;
        }

        NotifyHostChangedAndPersist();
    }

    public Task CloseAllAssetDocumentTabsAsync()
    {
        if (_documentWorkspace.ActiveBundleTab is null || _documentWorkspace.ActiveBundleTab.AssetTabs.Count == 0)
        {
            return Task.CompletedTask;
        }

        _assetDocumentHostCoordinator.ClearCurrentInspectorHostState(clearAssetTabs: true);
        _primaryPane.ClearSelection();
        _requestWorkspaceBrowserStatePersistence();
        return Task.CompletedTask;
    }

    public Task<AssetDocumentTabViewModel?> DetachAssetDocumentTabForExternalHostAsync(
        AssetDocumentTabViewModel? assetTab)
    {
        if (assetTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return Task.FromResult<AssetDocumentTabViewModel?>(null);
        }

        var ownerTab = _documentWorkspace.ActiveBundleTab;
        if (!ownerTab.AssetTabs.Contains(assetTab))
        {
            return Task.FromResult<AssetDocumentTabViewModel?>(null);
        }

        var wasActive = ReferenceEquals(ownerTab.ActiveAssetTab, assetTab);
        if (!ownerTab.DetachAssetTab(assetTab))
        {
            return Task.FromResult<AssetDocumentTabViewModel?>(null);
        }

        if (!wasActive)
        {
            NotifyHostChangedAndPersist();
            return Task.FromResult<AssetDocumentTabViewModel?>(assetTab);
        }

        if (ownerTab.ActiveAssetTab is not null)
        {
            QueueFallbackAssetDocumentActivation(ownerTab, ownerTab.ActiveAssetTab);
            return Task.FromResult<AssetDocumentTabViewModel?>(assetTab);
        }

        _primaryPane.ClearSelection();
        NotifyHostChangedAndPersist();
        return Task.FromResult<AssetDocumentTabViewModel?>(assetTab);
    }

    private void QueueFallbackAssetDocumentActivation(
        BundleDocumentTabViewModel ownerTab,
        AssetDocumentTabViewModel assetTab)
    {
        NotifyHostChangedAndPersist();
        _ = ActivateFallbackAssetDocumentAsync(ownerTab, assetTab);
    }

    private async Task ActivateFallbackAssetDocumentAsync(
        BundleDocumentTabViewModel ownerTab,
        AssetDocumentTabViewModel assetTab)
    {
        try
        {
            await Task.Yield();

            if (!ownerTab.AssetTabs.Contains(assetTab))
            {
                return;
            }

            await _setActiveAssetDocumentTabAsync(ownerTab, assetTab, true, false);
        }
        catch
        {
            // 迁出标签的宿主状态已经完成同步；剩余标签刷新失败不应阻塞窗口化。
        }
    }

    public Task<AssetDocumentGroupTransfer?> DetachActiveAssetDocumentGroupForExternalHostAsync()
    {
        if (_documentWorkspace.ActiveBundleTab is not { } ownerTab)
        {
            return Task.FromResult<AssetDocumentGroupTransfer?>(null);
        }

        var transfer = ownerTab.DetachDocumentTabs();
        if (transfer is null)
        {
            return Task.FromResult<AssetDocumentGroupTransfer?>(null);
        }

        NotifyHostChangedAndPersist();
        return Task.FromResult<AssetDocumentGroupTransfer?>(transfer);
    }

    private void NotifyHostChangedAndPersist()
    {
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
        _notifyActionStates();
    }
}
