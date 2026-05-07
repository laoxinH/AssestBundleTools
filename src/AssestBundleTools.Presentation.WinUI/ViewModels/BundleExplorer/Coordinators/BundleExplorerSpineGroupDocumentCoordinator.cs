using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责 Spine 资源组文档的打开、激活与右侧检查器投影。
/// </summary>
internal sealed class BundleExplorerSpineGroupDocumentCoordinator
{
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly AssetPaneViewModel _primaryPane;
    private readonly BundleExplorerAssetDocumentHostCoordinator _assetDocumentHostCoordinator;
    private readonly SpineAssetGroupInspectorPresenter _spineGroupInspectorPresenter;
    private readonly ISpineAssetGroupDetailsLoader _spineGroupDetailsLoader;
    private readonly Action _requestWorkspaceBrowserStatePersistence;
    private readonly Action _notifyActionStates;
    private int _spineGroupDocumentStateSynchronizationDepth;

    public BundleExplorerSpineGroupDocumentCoordinator(
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        AssetPaneViewModel primaryPane,
        BundleExplorerAssetDocumentHostCoordinator assetDocumentHostCoordinator,
        SpineAssetGroupInspectorPresenter spineGroupInspectorPresenter,
        ISpineAssetGroupDetailsLoader spineGroupDetailsLoader,
        Action requestWorkspaceBrowserStatePersistence,
        Action notifyActionStates)
    {
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _primaryPane = primaryPane ?? throw new ArgumentNullException(nameof(primaryPane));
        _assetDocumentHostCoordinator = assetDocumentHostCoordinator ?? throw new ArgumentNullException(nameof(assetDocumentHostCoordinator));
        _spineGroupInspectorPresenter = spineGroupInspectorPresenter ?? throw new ArgumentNullException(nameof(spineGroupInspectorPresenter));
        _spineGroupDetailsLoader = spineGroupDetailsLoader ?? throw new ArgumentNullException(nameof(spineGroupDetailsLoader));
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
        _notifyActionStates = notifyActionStates ?? throw new ArgumentNullException(nameof(notifyActionStates));
    }

    public bool IsSynchronizingSpineGroupDocumentState => _spineGroupDocumentStateSynchronizationDepth > 0;

    public Task HandlePrimaryPaneSelectionChangedAsync(
        bool isSynchronizingPrimaryPaneState,
        Action captureActiveTabState)
    {
        ArgumentNullException.ThrowIfNull(captureActiveTabState);

        if (isSynchronizingPrimaryPaneState)
        {
            return Task.CompletedTask;
        }

        if (IsSynchronizingSpineGroupDocumentState)
        {
            captureActiveTabState();
            return Task.CompletedTask;
        }

        if (_primaryPane.ViewMode != AssetPaneViewMode.SpineGroups)
        {
            captureActiveTabState();
            return Task.CompletedTask;
        }

        if (_primaryPane.SelectedSpineGroup is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return Task.CompletedTask;
        }

        return OpenOrActivateSpineGroupDocumentTabAsync(
            _documentWorkspace.ActiveBundleTab,
            _primaryPane.SelectedSpineGroup,
            synchronizePaneSelection: false);
    }

    public async Task SynchronizeInspectorWithActiveBundleTabAsync()
    {
        var activeBundleTab = _documentWorkspace.ActiveBundleTab;
        if (activeBundleTab is null)
        {
            return;
        }

        if (_primaryPane.ViewMode != AssetPaneViewMode.SpineGroups)
        {
            return;
        }

        var preferredGroupTab = ResolvePreferredSpineGroupDocumentTab(activeBundleTab);
        if (preferredGroupTab is not null)
        {
            await SetActiveSpineGroupDocumentTabAsync(
                activeBundleTab,
                preferredGroupTab,
                synchronizePaneSelection: true);
            return;
        }

        if (_primaryPane.SelectedSpineGroup is null)
        {
            ClearActiveSpineGroupDocument(activeBundleTab);
            return;
        }

        await OpenOrActivateSpineGroupDocumentTabAsync(
            activeBundleTab,
            _primaryPane.SelectedSpineGroup,
            synchronizePaneSelection: false);
    }

    public async Task ActivateSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        if (spineGroupTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return;
        }

        await SetActiveSpineGroupDocumentTabAsync(
            _documentWorkspace.ActiveBundleTab,
            spineGroupTab,
            synchronizePaneSelection: true);
    }

    public async Task SelectActiveAtlasPageAsync(SpineAtlasPageOptionViewModel? atlasPageOption)
    {
        if (atlasPageOption is null ||
            _documentWorkspace.ActiveBundleTab is not { ActiveSpineGroupTab: { } spineGroupTab } bundleTab ||
            !spineGroupTab.SelectAtlasPageOption(atlasPageOption))
        {
            return;
        }

        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _notifyActionStates();

        var texturePreview = await _spineGroupDetailsLoader.LoadTexturePreviewAsync(
            spineGroupTab,
            atlasPageOption);
        if (!ReferenceEquals(bundleTab.ActiveSpineGroupTab, spineGroupTab) ||
            !ReferenceEquals(spineGroupTab.SelectedAtlasPageOption, atlasPageOption))
        {
            return;
        }

        spineGroupTab.ApplyTexturePreview(texturePreview);
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _notifyActionStates();
    }

    public async Task CloseSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        if (spineGroupTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return;
        }

        var ownerTab = _documentWorkspace.ActiveBundleTab;
        var wasActive = ReferenceEquals(ownerTab.ActiveSpineGroupTab, spineGroupTab);
        ownerTab.RemoveSpineGroupTab(spineGroupTab);

        if (wasActive && ownerTab.ActiveSpineGroupTab is not null)
        {
            await SetActiveSpineGroupDocumentTabAsync(
                ownerTab,
                ownerTab.ActiveSpineGroupTab,
                synchronizePaneSelection: true);
            return;
        }

        if (wasActive)
        {
            _primaryPane.SelectedSpineGroup = null;
        }

        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
        _notifyActionStates();
    }

    public async Task<SpineAssetGroupDocumentTabViewModel?> DetachSpineGroupDocumentTabForExternalHostAsync(
        SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        if (spineGroupTab is null || _documentWorkspace.ActiveBundleTab is null)
        {
            return null;
        }

        var ownerTab = _documentWorkspace.ActiveBundleTab;
        var wasActive = ReferenceEquals(ownerTab.ActiveSpineGroupTab, spineGroupTab);
        if (!ownerTab.DetachSpineGroupTab(spineGroupTab))
        {
            return null;
        }

        if (wasActive && ownerTab.ActiveSpineGroupTab is not null)
        {
            await SetActiveSpineGroupDocumentTabAsync(
                ownerTab,
                ownerTab.ActiveSpineGroupTab,
                synchronizePaneSelection: true);
        }
        else if (wasActive)
        {
            _primaryPane.SelectedSpineGroup = null;
        }

        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
        _notifyActionStates();
        return spineGroupTab;
    }

    public Task RestoreDetachedSpineGroupDocumentTabAsync(
        SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTab);

        return _documentWorkspace.ActiveBundleTab is null
            ? Task.CompletedTask
            : SetActiveSpineGroupDocumentTabAsync(
                _documentWorkspace.ActiveBundleTab,
                spineGroupTab,
                synchronizePaneSelection: true);
    }

    private SpineAssetGroupDocumentTabViewModel? ResolvePreferredSpineGroupDocumentTab(
        BundleDocumentTabViewModel bundleTab)
    {
        if (bundleTab.ActiveSpineGroupTab is not null)
        {
            return bundleTab.ActiveSpineGroupTab;
        }

        if (_primaryPane.SelectedSpineGroup is not null &&
            bundleTab.TryGetSpineGroupTab(_primaryPane.SelectedSpineGroup.Item.GroupId, out var existingGroupTab))
        {
            return existingGroupTab;
        }

        return bundleTab.SpineGroupTabs.FirstOrDefault();
    }

    private async Task OpenOrActivateSpineGroupDocumentTabAsync(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupEntryViewModel entry,
        bool synchronizePaneSelection)
    {
        ArgumentNullException.ThrowIfNull(bundleTab);
        ArgumentNullException.ThrowIfNull(entry);

        if (!bundleTab.TryGetSpineGroupTab(entry.Item.GroupId, out var spineGroupTab))
        {
            spineGroupTab = _assetDocumentHostCoordinator.CreateSpineGroupDocumentTab(entry.Item);
            bundleTab.AddSpineGroupTab(spineGroupTab, activate: false);
        }
        else
        {
            spineGroupTab.ApplyGroup(entry.Item);
        }

        await SetActiveSpineGroupDocumentTabAsync(
            bundleTab,
            spineGroupTab,
            synchronizePaneSelection);
    }

    private async Task SetActiveSpineGroupDocumentTabAsync(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        bool synchronizePaneSelection)
    {
        ArgumentNullException.ThrowIfNull(bundleTab);
        ArgumentNullException.ThrowIfNull(spineGroupTab);

        var wasAlreadyActiveGroupTab = ReferenceEquals(bundleTab.ActiveSpineGroupTab, spineGroupTab);
        bundleTab.SetActiveSpineGroupTab(spineGroupTab);
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);

        if (!wasAlreadyActiveGroupTab)
        {
            _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        }

        if (synchronizePaneSelection)
        {
            BeginSpineGroupDocumentStateSynchronization();
            try
            {
                if (!TrySelectSpineGroup(spineGroupTab.GroupId))
                {
                    _primaryPane.SelectedSpineGroup = null;
                }
            }
            finally
            {
                EndSpineGroupDocumentStateSynchronization();
            }
        }

        _requestWorkspaceBrowserStatePersistence();
        _notifyActionStates();
        await LoadSpineGroupDetailsAsync(bundleTab, spineGroupTab);
    }

    private async Task LoadSpineGroupDetailsAsync(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        if (spineGroupTab.Details is not null)
        {
            return;
        }

        var expectedGroupId = spineGroupTab.GroupId;
        var expectedBundleId = spineGroupTab.BundleId;
        var expectedGroup = spineGroupTab.Item;
        var loadResult = await _spineGroupDetailsLoader.LoadAsync(spineGroupTab);
        if (loadResult is null ||
            !expectedBundleId.Equals(spineGroupTab.BundleId) ||
            !string.Equals(expectedGroupId, spineGroupTab.GroupId, StringComparison.Ordinal) ||
            !ReferenceEquals(expectedGroup, spineGroupTab.Item) ||
            !ReferenceEquals(bundleTab.ActiveSpineGroupTab, spineGroupTab))
        {
            return;
        }

        spineGroupTab.ApplyDetails(loadResult.Details, loadResult.TexturePreview);
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _requestWorkspaceBrowserStatePersistence();
        _notifyActionStates();
    }

    private void ClearActiveSpineGroupDocument(BundleDocumentTabViewModel bundleTab)
    {
        bundleTab.SetActiveSpineGroupTab(null);
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _notifyActionStates();
    }

    private void BeginSpineGroupDocumentStateSynchronization()
    {
        _spineGroupDocumentStateSynchronizationDepth++;
    }

    private bool TrySelectSpineGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return false;
        }

        var target = _primaryPane.SpineGroupItems.FirstOrDefault(item =>
            string.Equals(item.Item.GroupId, groupId, StringComparison.Ordinal));
        if (target is null)
        {
            return false;
        }

        _primaryPane.SelectedSpineGroup = target;
        return true;
    }

    private void EndSpineGroupDocumentStateSynchronization()
    {
        if (_spineGroupDocumentStateSynchronizationDepth <= 0)
        {
            _spineGroupDocumentStateSynchronizationDepth = 0;
            return;
        }

        _spineGroupDocumentStateSynchronizationDepth--;
    }
}
