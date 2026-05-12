using AssestBundleTools.Application.Assets.Dtos;
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

        if (activeBundleTab.ActiveDocumentTab?.IsAsset == true)
        {
            _notifyActionStates();
            _requestWorkspaceBrowserStatePersistence();
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

    public Task RefreshSpineGroupDocumentTabAsync(SpineAssetGroupDocumentTabViewModel? spineGroupTab)
    {
        if (spineGroupTab is null)
        {
            return Task.CompletedTask;
        }

        var bundleTab = _documentWorkspace.OpenBundleTabs
            .FirstOrDefault(tab => tab.SpineGroupTabs.Contains(spineGroupTab));
        if (bundleTab is null)
        {
            return Task.CompletedTask;
        }

        RefreshSpineGroupTabFromPrimaryPaneIfChanged(spineGroupTab);
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        if (IsCurrentVisibleSpineGroupTab(bundleTab, spineGroupTab))
        {
            _notifyActionStates();
        }

        return LoadSpineGroupDetailsAsync(bundleTab, spineGroupTab, forceReload: true, requireActive: false);
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

        RefreshSpineGroupTabFromPrimaryPaneIfChanged(spineGroupTab);

        var wasAlreadyActiveGroupTab = ReferenceEquals(bundleTab.ActiveSpineGroupTab, spineGroupTab);
        bundleTab.SetActiveSpineGroupTab(spineGroupTab);
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        if (spineGroupTab.Details is null)
        {
            _spineGroupInspectorPresenter.PresentSpineGroupPreviewLoading(spineGroupTab);
        }

        if (!wasAlreadyActiveGroupTab)
        {
            _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        }

        if (synchronizePaneSelection)
        {
            BeginSpineGroupDocumentStateSynchronization();
            try
            {
                if (!_primaryPane.TrySelectSpineGroup(spineGroupTab.GroupId))
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
        await LoadSpineGroupDetailsAsync(bundleTab, spineGroupTab, forceReload: false, requireActive: true);
    }

    private void RefreshSpineGroupTabFromPrimaryPaneIfChanged(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        var refreshedGroup = _primaryPane.SpineGroupItems
            .FirstOrDefault(entry => string.Equals(
                entry.Item.GroupId,
                spineGroupTab.GroupId,
                StringComparison.Ordinal))
            ?.Item;
        if (refreshedGroup is null ||
            HasSameSpineGroupSnapshot(spineGroupTab.Item, refreshedGroup))
        {
            return;
        }

        spineGroupTab.ApplyGroup(refreshedGroup);
    }

    private static bool HasSameSpineGroupSnapshot(
        SpineAssetGroupDto left,
        SpineAssetGroupDto right)
    {
        return string.Equals(left.GroupId, right.GroupId, StringComparison.Ordinal) &&
               left.BundleId.Equals(right.BundleId) &&
               string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
               string.Equals(left.GroupKey, right.GroupKey, StringComparison.Ordinal) &&
               HasSameParts(left.SkeletonAssets, right.SkeletonAssets) &&
               HasSameParts(left.AtlasAssets, right.AtlasAssets) &&
               HasSameParts(left.TexturePageAssets, right.TexturePageAssets) &&
               HasSameDiagnostics(left.Diagnostics, right.Diagnostics);
    }

    private static bool HasSameParts(
        IReadOnlyList<SpineAssetGroupPartDto> left,
        IReadOnlyList<SpineAssetGroupPartDto> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!HasSamePart(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSamePart(
        SpineAssetGroupPartDto left,
        SpineAssetGroupPartDto right)
    {
        return left.Role == right.Role &&
               left.BundleId.Equals(right.BundleId) &&
               left.AssetId.Equals(right.AssetId) &&
               left.AssetType == right.AssetType &&
               string.Equals(left.BundleDisplayName, right.BundleDisplayName, StringComparison.Ordinal) &&
               string.Equals(left.Name, right.Name, StringComparison.Ordinal) &&
               string.Equals(left.AssetPath, right.AssetPath, StringComparison.Ordinal) &&
               string.Equals(left.AssetTypeDisplayName, right.AssetTypeDisplayName, StringComparison.Ordinal) &&
               Nullable.Equals(left.LastExtractedAtUtc, right.LastExtractedAtUtc);
    }

    private static bool HasSameDiagnostics(
        IReadOnlyList<SpineAssetGroupDiagnosticDto> left,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (left[index].Kind != right[index].Kind ||
                !string.Equals(left[index].GroupKey, right[index].GroupKey, StringComparison.Ordinal) ||
                !string.Equals(left[index].AtlasPageName, right[index].AtlasPageName, StringComparison.Ordinal) ||
                left[index].AtlasPageWidth != right[index].AtlasPageWidth ||
                left[index].AtlasPageHeight != right[index].AtlasPageHeight ||
                left[index].TextureWidth != right[index].TextureWidth ||
                left[index].TextureHeight != right[index].TextureHeight ||
                left[index].AnimationUnsupportedFeature != right[index].AnimationUnsupportedFeature ||
                !string.Equals(left[index].SpineVersion, right[index].SpineVersion, StringComparison.Ordinal) ||
                !string.Equals(left[index].RuntimeAdapterKey, right[index].RuntimeAdapterKey, StringComparison.Ordinal) ||
                !HasSameParts(left[index].RelatedParts, right[index].RelatedParts))
            {
                return false;
            }
        }

        return true;
    }

    private async Task LoadSpineGroupDetailsAsync(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        bool forceReload,
        bool requireActive)
    {
        if (!forceReload && spineGroupTab.Details is not null)
        {
            return;
        }

        var expectedGroupId = spineGroupTab.GroupId;
        var expectedBundleId = spineGroupTab.BundleId;
        var expectedGroup = spineGroupTab.Item;
        var loadResult = await _spineGroupDetailsLoader.LoadAsync(spineGroupTab);
        if (!expectedBundleId.Equals(spineGroupTab.BundleId) ||
            !string.Equals(expectedGroupId, spineGroupTab.GroupId, StringComparison.Ordinal) ||
            !ReferenceEquals(expectedGroup, spineGroupTab.Item) ||
            (requireActive && !ReferenceEquals(bundleTab.ActiveSpineGroupTab, spineGroupTab)))
        {
            return;
        }

        var isCurrentVisibleTab = IsCurrentVisibleSpineGroupTab(bundleTab, spineGroupTab);
        if (loadResult is null)
        {
            _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
            if (isCurrentVisibleTab)
            {
                _documentWorkspace.NotifyAssetDocumentHostStateChanged();
                _notifyActionStates();
            }

            return;
        }

        spineGroupTab.ApplyDetails(
            loadResult.Details,
            loadResult.TexturePreview,
            loadResult.AnimationTextures);
        _spineGroupInspectorPresenter.PresentSpineGroupDocumentTab(spineGroupTab);
        if (isCurrentVisibleTab)
        {
            _documentWorkspace.NotifyAssetDocumentHostStateChanged();
            _requestWorkspaceBrowserStatePersistence();
            _notifyActionStates();
        }
    }

    private bool IsCurrentVisibleSpineGroupTab(
        BundleDocumentTabViewModel bundleTab,
        SpineAssetGroupDocumentTabViewModel spineGroupTab) =>
        ReferenceEquals(_documentWorkspace.ActiveBundleTab, bundleTab) &&
        ReferenceEquals(bundleTab.ActiveSpineGroupTab, spineGroupTab);

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
