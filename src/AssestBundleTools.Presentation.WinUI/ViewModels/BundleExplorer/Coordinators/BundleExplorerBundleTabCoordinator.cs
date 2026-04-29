using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责资源包标签页的打开、激活、关闭与顺序编排。
/// </summary>
/// <remarks>
/// 这里集中承接页面级“资源包浏览动作”，
/// 避免主页面 ViewModel 同时堆积工作区编排和标签导航两类长流程。
/// </remarks>
internal sealed class BundleExplorerBundleTabCoordinator
{
    private readonly BundleExplorerBundlePaneViewModel _bundlePane;
    private readonly BundleExplorerDocumentWorkspaceViewModel _documentWorkspace;
    private readonly BundleExplorerAssetWorkflowViewModel _assetWorkflow;
    private readonly Func<bool> _hasWorkspaceProvider;
    private readonly Action _requestWorkspaceBrowserStatePersistence;

    public BundleExplorerBundleTabCoordinator(
        BundleExplorerBundlePaneViewModel bundlePane,
        BundleExplorerDocumentWorkspaceViewModel documentWorkspace,
        BundleExplorerAssetWorkflowViewModel assetWorkflow,
        Func<bool> hasWorkspaceProvider,
        Action requestWorkspaceBrowserStatePersistence)
    {
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _assetWorkflow = assetWorkflow ?? throw new ArgumentNullException(nameof(assetWorkflow));
        _hasWorkspaceProvider = hasWorkspaceProvider ?? throw new ArgumentNullException(nameof(hasWorkspaceProvider));
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
    }

    public Task OpenBundleAsync(BundleNodeViewModel bundleNode, bool openAsPinnedTab)
    {
        ArgumentNullException.ThrowIfNull(bundleNode);

        if (!_hasWorkspaceProvider())
        {
            return Task.CompletedTask;
        }

        var targetTab = _documentWorkspace.OpenBundle(bundleNode, openAsPinnedTab);
        return SetActiveBundleTabAsync(targetTab, writeSelectionLog: false);
    }

    public async Task ActivateBundleTabAsync(BundleDocumentTabViewModel? tab)
    {
        if (tab is null || !_hasWorkspaceProvider())
        {
            return;
        }

        if (ReferenceEquals(_documentWorkspace.ActiveBundleTab, tab))
        {
            _bundlePane.SetSelectedBundle(_bundlePane.FindBundleNode(tab.BundleId));
            return;
        }

        await SetActiveBundleTabAsync(tab, writeSelectionLog: false);
    }

    /// <summary>
    /// 浏览状态恢复与页面内标签切换共用同一条激活链路，
    /// 这样资产筛选同步、列表高亮和检查器刷新都能保持一致。
    /// </summary>
    public async Task SetActiveBundleTabAsync(BundleDocumentTabViewModel tab, bool writeSelectionLog)
    {
        ArgumentNullException.ThrowIfNull(tab);

        _assetWorkflow.CaptureCurrentBundleTabStateBeforeActivation(tab);
        _documentWorkspace.BeginAssetDocumentHostSynchronization();
        try
        {
            _documentWorkspace.SetActiveBundleTab(tab);
            var bundleNode = _bundlePane.FindBundleNode(tab.BundleId);
            _bundlePane.SetSelectedBundle(bundleNode);
            await _assetWorkflow.LoadActiveBundleTabAsync(tab, bundleNode, writeSelectionLog);
        }
        finally
        {
            _documentWorkspace.EndAssetDocumentHostSynchronization();
        }
    }

    public void ReorderBundleTabs(IReadOnlyList<BundleDocumentTabViewModel> orderedTabs)
    {
        ArgumentNullException.ThrowIfNull(orderedTabs);

        if (!_documentWorkspace.ReorderBundleTabs(orderedTabs))
        {
            return;
        }

        _requestWorkspaceBrowserStatePersistence();
    }

    public async Task CloseBundleTabAsync(BundleDocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        var removedIndex = _documentWorkspace.BundleTabs.IndexOf(tab);
        if (removedIndex < 0)
        {
            return;
        }

        var wasActive = ReferenceEquals(_documentWorkspace.ActiveBundleTab, tab);

        _documentWorkspace.BundleTabs.RemoveAt(removedIndex);

        if (_documentWorkspace.BundleTabs.Count == 0)
        {
            _assetWorkflow.ClearActiveTabState();
            return;
        }

        if (!wasActive)
        {
            _requestWorkspaceBrowserStatePersistence();
            return;
        }

        var nextIndex = Math.Min(removedIndex, _documentWorkspace.BundleTabs.Count - 1);
        await SetActiveBundleTabAsync(_documentWorkspace.BundleTabs[nextIndex], writeSelectionLog: false);
    }

    /// <summary>
    /// 关闭其他资源包标签时，统一由这里保证“保留目标”和“活动焦点”保持一致。
    /// </summary>
    public async Task CloseOtherBundleTabsAsync(BundleDocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        var retainedTab = _documentWorkspace.BundleTabs.FirstOrDefault(existingTab => ReferenceEquals(existingTab, tab));
        if (retainedTab is null)
        {
            return;
        }

        if (_documentWorkspace.BundleTabs.Count <= 1)
        {
            if (!ReferenceEquals(_documentWorkspace.ActiveBundleTab, retainedTab) && _hasWorkspaceProvider())
            {
                await SetActiveBundleTabAsync(retainedTab, writeSelectionLog: false);
            }

            return;
        }

        var tabsToRemove = _documentWorkspace.BundleTabs
            .Where(existingTab => !ReferenceEquals(existingTab, retainedTab))
            .ToArray();

        foreach (var tabToRemove in tabsToRemove)
        {
            _documentWorkspace.BundleTabs.Remove(tabToRemove);
        }

        if (!ReferenceEquals(_documentWorkspace.ActiveBundleTab, retainedTab))
        {
            await SetActiveBundleTabAsync(retainedTab, writeSelectionLog: false);
            return;
        }

        _requestWorkspaceBrowserStatePersistence();
    }

    public Task CloseAllBundleTabsAsync()
    {
        if (_documentWorkspace.BundleTabs.Count == 0)
        {
            return Task.CompletedTask;
        }

        _assetWorkflow.CaptureActiveTabState();
        _documentWorkspace.BundleTabs.Clear();
        _assetWorkflow.ClearActiveTabState();
        return Task.CompletedTask;
    }
}
