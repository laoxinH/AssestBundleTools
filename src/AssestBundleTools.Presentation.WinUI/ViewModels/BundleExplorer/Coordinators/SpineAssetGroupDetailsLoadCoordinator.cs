using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal sealed class SpineAssetGroupDetailsLoadCoordinator
{
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly ISpineAssetGroupDetailsLoader _detailsLoader;
    private readonly SpineAssetGroupInspectorPresenter _presenter;
    private readonly Action _requestWorkspaceBrowserStatePersistence;
    private readonly Action _notifyActionStates;
    private readonly Dictionary<SpineAssetGroupDocumentTabViewModel, PendingLoad> _pendingLoads = new();

    public SpineAssetGroupDetailsLoadCoordinator(
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        ISpineAssetGroupDetailsLoader detailsLoader,
        SpineAssetGroupInspectorPresenter presenter,
        Action requestWorkspaceBrowserStatePersistence,
        Action notifyActionStates)
    {
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _detailsLoader = detailsLoader ?? throw new ArgumentNullException(nameof(detailsLoader));
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
        _notifyActionStates = notifyActionStates ?? throw new ArgumentNullException(nameof(notifyActionStates));
    }

    public async Task LoadAsync(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupDocumentTabViewModel groupTab,
        bool forceReload,
        bool requireActive)
    {
        if (!forceReload && groupTab.Details is not null)
        {
            return;
        }

        if (!forceReload &&
            _pendingLoads.TryGetValue(groupTab, out var pendingLoad) &&
            ReferenceEquals(pendingLoad.GroupSnapshot, groupTab.Item))
        {
            await pendingLoad.LoadTask;
            return;
        }

        var loadTask = LoadCoreAsync(
            bundleTab,
            groupTab,
            requireActive,
            CancellationToken.None);
        if (!forceReload)
        {
            var trackedLoad = new PendingLoad(groupTab.Item, loadTask);
            _pendingLoads[groupTab] = trackedLoad;
            try
            {
                await loadTask;
            }
            finally
            {
                if (_pendingLoads.TryGetValue(groupTab, out var currentLoad) &&
                    ReferenceEquals(currentLoad, trackedLoad))
                {
                    _pendingLoads.Remove(groupTab);
                }
            }

            return;
        }

        await loadTask;
    }

    public Task RefreshFromRequestAsync(SpineAssetGroupDetailsRefreshRequest request)
    {
        var bundleTab = _documentWorkspace.OpenBundleTabs
            .FirstOrDefault(tab => tab.SpineGroupTabs.Contains(request.GroupTab));
        return bundleTab is null || !request.IsCurrent
            ? Task.CompletedTask
            : LoadCoreAsync(
                bundleTab,
                request.GroupTab,
                requireActive: false,
                request.CancellationToken,
                () => request.IsCurrent);
    }

    private async Task LoadCoreAsync(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupDocumentTabViewModel groupTab,
        bool requireActive,
        CancellationToken cancellationToken,
        Func<bool>? isCurrent = null)
    {
        var expectedGroupId = groupTab.GroupId;
        var expectedBundleId = groupTab.BundleId;
        var expectedGroup = groupTab.Item;
        var loadResult = await _detailsLoader.LoadAsync(groupTab, cancellationToken);
        if (!bundleTab.SpineGroupTabs.Contains(groupTab) ||
            !expectedBundleId.Equals(groupTab.BundleId) ||
            !string.Equals(expectedGroupId, groupTab.GroupId, StringComparison.Ordinal) ||
            !ReferenceEquals(expectedGroup, groupTab.Item) ||
            (requireActive && !ReferenceEquals(bundleTab.ActiveSpineGroupTab, groupTab)) ||
            isCurrent?.Invoke() == false)
        {
            return;
        }

        var isCurrentVisibleTab = IsCurrentVisibleSpineGroupTab(bundleTab, groupTab);
        if (loadResult is null)
        {
            _presenter.PresentSpineGroupDocumentTab(groupTab);
            if (isCurrentVisibleTab)
            {
                _documentWorkspace.NotifyAssetDocumentHostStateChanged();
                _notifyActionStates();
            }

            return;
        }

        groupTab.ApplyDetails(
            loadResult.Details,
            loadResult.TexturePreview,
            loadResult.AnimationTextures);
        _presenter.PresentSpineGroupDocumentTab(groupTab);
        if (isCurrentVisibleTab)
        {
            _documentWorkspace.NotifyAssetDocumentHostStateChanged();
            _requestWorkspaceBrowserStatePersistence();
            _notifyActionStates();
        }
    }

    private bool IsCurrentVisibleSpineGroupTab(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupDocumentTabViewModel groupTab) =>
        ReferenceEquals(_documentWorkspace.ActiveBundleTab, bundleTab) &&
        ReferenceEquals(bundleTab.ActiveSpineGroupTab, groupTab);

    private sealed record PendingLoad(
        SpineAssetGroupDto GroupSnapshot,
        Task LoadTask);
}
