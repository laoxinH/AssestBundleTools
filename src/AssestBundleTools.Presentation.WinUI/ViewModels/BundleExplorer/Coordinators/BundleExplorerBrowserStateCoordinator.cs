using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Services;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责工作台页面浏览状态的恢复、快照构建与节流持久化。
/// </summary>
internal sealed class BundleExplorerBrowserStateCoordinator : IBundleExplorerBrowserStateCoordinator
{
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly BundleExplorerBundlePaneViewModel _bundlePane;
    private readonly BundleExplorerDocumentWorkspaceViewModel _documentWorkspace;
    private readonly Func<bool> _hasWorkspaceProvider;
    private readonly Action _captureActiveTabStateWithoutPersistence;
    private readonly Func<BundleId, AssetId, string, AssetDocumentTabViewModel> _createAssetDocumentTab;
    private readonly Func<SpineAssetGroupDto, SpineAssetGroupDocumentTabViewModel>? _createSpineGroupDocumentTab;
    private readonly Func<BundleDocumentTabViewModel, Task> _activateBundleTabAsync;
    private readonly Func<IReadOnlyList<WorkspaceAssetEditorWindowStateDto>> _captureOpenAssetEditorWindows;
    private readonly Func<IReadOnlyList<WorkspaceAssetEditorWindowStateDto>> _capturePendingAssetEditorWindows;
    private readonly Action<IReadOnlyList<WorkspaceAssetEditorWindowStateDto>?> _restorePendingAssetEditorWindows;
    private WorkspaceWindowSizePreferencesDto? _windowSizes;
    private bool _isRestoringWorkspaceBrowserState;
    private CancellationTokenSource? _workspaceBrowserStatePersistenceCts;

    public BundleExplorerBrowserStateCoordinator(
        IWorkspaceContextService workspaceContextService,
        BundleExplorerBundlePaneViewModel bundlePane,
        BundleExplorerDocumentWorkspaceViewModel documentWorkspace,
        Func<bool> hasWorkspaceProvider,
        Action captureActiveTabStateWithoutPersistence,
        Func<BundleId, AssetId, string, AssetDocumentTabViewModel> createAssetDocumentTab,
        Func<BundleDocumentTabViewModel, Task> activateBundleTabAsync,
        Func<SpineAssetGroupDto, SpineAssetGroupDocumentTabViewModel>? createSpineGroupDocumentTab = null,
        Func<IReadOnlyList<WorkspaceAssetEditorWindowStateDto>>? captureOpenAssetEditorWindows = null,
        Action<IReadOnlyList<WorkspaceAssetEditorWindowStateDto>?>? restorePendingAssetEditorWindows = null,
        Func<IReadOnlyList<WorkspaceAssetEditorWindowStateDto>>? capturePendingAssetEditorWindows = null)
    {
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _bundlePane = bundlePane ?? throw new ArgumentNullException(nameof(bundlePane));
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _hasWorkspaceProvider = hasWorkspaceProvider ?? throw new ArgumentNullException(nameof(hasWorkspaceProvider));
        _captureActiveTabStateWithoutPersistence = captureActiveTabStateWithoutPersistence ?? throw new ArgumentNullException(nameof(captureActiveTabStateWithoutPersistence));
        _createAssetDocumentTab = createAssetDocumentTab ?? throw new ArgumentNullException(nameof(createAssetDocumentTab));
        _createSpineGroupDocumentTab = createSpineGroupDocumentTab;
        _activateBundleTabAsync = activateBundleTabAsync ?? throw new ArgumentNullException(nameof(activateBundleTabAsync));
        _captureOpenAssetEditorWindows = captureOpenAssetEditorWindows
            ?? (() => Array.Empty<WorkspaceAssetEditorWindowStateDto>());
        _restorePendingAssetEditorWindows = restorePendingAssetEditorWindows
            ?? (_ => { });
        _capturePendingAssetEditorWindows = capturePendingAssetEditorWindows
            ?? (() => Array.Empty<WorkspaceAssetEditorWindowStateDto>());
    }

    /// <summary>
    /// 尝试从工作区元数据恢复左侧资源包搜索、已打开标签页和当前活动标签页。
    /// 如果没有可恢复状态，则由上层回退到默认首个资源包预览流程。
    /// </summary>
    public async Task<bool> TryRestoreWorkspaceBrowserStateAsync()
    {
        var browserState = _workspaceContextService.GetCurrentWorkspaceBrowserState();
        if (browserState is null)
        {
            _windowSizes = null;
            ClearPendingAssetEditorWindowRestoreState();
            return false;
        }

        _isRestoringWorkspaceBrowserState = true;
        try
        {
            _bundlePane.RestoreSearchState(browserState.BundleSearchKeyword);
            _windowSizes = browserState.WindowSizes;
            _restorePendingAssetEditorWindows(browserState.OpenAssetEditorWindows);

            var restoredTabs = RestoreBundleTabsFromBrowserState(browserState);
            if (restoredTabs.Count == 0)
            {
                return await TryRestoreLastBundleFromBrowserStateAsync(browserState);
            }

            var activeTab = restoredTabs.FirstOrDefault(tab =>
                               string.Equals(tab.BundleId.Value, browserState.ActiveBundleTabId, StringComparison.Ordinal))
                           ?? restoredTabs.FirstOrDefault(tab =>
                               string.Equals(tab.BundleId.Value, browserState.LastBundleId, StringComparison.Ordinal))
                           ?? restoredTabs[0];

            await _activateBundleTabAsync(activeTab);
            return true;
        }
        finally
        {
            _isRestoringWorkspaceBrowserState = false;
            PersistWorkspaceBrowserStateNow();
        }
    }

    /// <summary>
    /// 节流当前工作区浏览状态的落盘动作，避免搜索输入时频繁写文件。
    /// 真实的元数据更新仍通过工作区上下文服务统一完成。
    /// </summary>
    public void RequestWorkspaceBrowserStatePersistence()
    {
        if (!_hasWorkspaceProvider() || _isRestoringWorkspaceBrowserState)
        {
            return;
        }

        CancelWorkspaceBrowserStatePersistence();

        var cancellationSource = new CancellationTokenSource();
        _workspaceBrowserStatePersistenceCts = cancellationSource;
        _ = PersistWorkspaceBrowserStateDebouncedAsync(cancellationSource);
    }

    public void PersistWorkspaceBrowserStateNow()
    {
        if (!_hasWorkspaceProvider() || _isRestoringWorkspaceBrowserState)
        {
            return;
        }

        CancelWorkspaceBrowserStatePersistence();
        PersistWorkspaceBrowserStateCore();
    }

    public void CancelWorkspaceBrowserStatePersistence()
    {
        var cancellationSource = Interlocked.Exchange(
            ref _workspaceBrowserStatePersistenceCts,
            null);
        if (cancellationSource is null)
        {
            return;
        }

        cancellationSource.Cancel();
        cancellationSource.Dispose();
    }

    public void ClearPendingAssetEditorWindowRestoreState()
    {
        _restorePendingAssetEditorWindows(Array.Empty<WorkspaceAssetEditorWindowStateDto>());
    }

    public WorkspaceWindowSizeDto? GetRememberedWindowSize(WorkspaceWindowSizeKind kind) =>
        kind switch
        {
            WorkspaceWindowSizeKind.BatchExtraction => _windowSizes?.BatchExtraction,
            WorkspaceWindowSizeKind.BatchReplacement => _windowSizes?.BatchReplacement,
            WorkspaceWindowSizeKind.Export => _windowSizes?.Export,
            WorkspaceWindowSizeKind.AssetEditor => _windowSizes?.AssetEditor,
            _ => null
        };

    public bool RememberWindowSize(WorkspaceWindowSizeKind kind, WorkspaceWindowSizeDto size)
    {
        ArgumentNullException.ThrowIfNull(size);
        if (!IsValidWindowSize(size) || Equals(GetRememberedWindowSize(kind), size))
        {
            return false;
        }

        var current = _windowSizes ?? new WorkspaceWindowSizePreferencesDto();
        _windowSizes = kind switch
        {
            WorkspaceWindowSizeKind.BatchExtraction => current with { BatchExtraction = size },
            WorkspaceWindowSizeKind.BatchReplacement => current with { BatchReplacement = size },
            WorkspaceWindowSizeKind.Export => current with { Export = size },
            WorkspaceWindowSizeKind.AssetEditor => current with { AssetEditor = size },
            _ => current
        };
        return true;
    }

    private List<BundleDocumentTabViewModel> RestoreBundleTabsFromBrowserState(
        WorkspaceBrowserStateSnapshotDto browserState)
    {
        var restoredTabs = new List<BundleDocumentTabViewModel>();
        var restoredBundleIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tabState in browserState.OpenBundleTabs)
        {
            if (string.IsNullOrWhiteSpace(tabState.BundleId) || !restoredBundleIds.Add(tabState.BundleId))
            {
                continue;
            }

            var bundleNode = _bundlePane.FindBundleNode(new BundleId(tabState.BundleId)) ??
                             _bundlePane.FindBundleNodeByLogicalPath(tabState.LogicalPath);
            if (bundleNode is null)
            {
                continue;
            }

            var tab = new BundleDocumentTabViewModel(
                bundleNode.BundleId,
                bundleNode.DisplayName,
                bundleNode.LogicalPath,
                tabState.IsPreview);
            tab.RestoreViewState(
                searchKeyword: tabState.SearchKeyword,
                selectedAssetTypeFilterKey: tabState.AssetTypeFilter,
                selectedAssetId: tabState.SelectedAssetId,
                showModifiedOnly: tabState.ShowModifiedOnly,
                showPreviewableOnly: tabState.ShowPreviewableOnly,
                assetPaneViewMode: tabState.AssetListViewMode);
            tab.RestoreAssetTabs(
                RestoreAssetTabsForBundle(bundleNode.BundleId, tabState.OpenAssetTabs),
                tabState.ActiveAssetTabId);
            tab.RestoreSpineGroupTabs(
                RestoreSpineGroupTabsForBundle(bundleNode.BundleId, tabState.OpenSpineGroupTabs),
                tabState.ActiveSpineGroupTabId);

            _documentWorkspace.BundleTabs.Add(tab);
            restoredTabs.Add(tab);
        }

        return restoredTabs;
    }

    private async Task<bool> TryRestoreLastBundleFromBrowserStateAsync(WorkspaceBrowserStateSnapshotDto browserState)
    {
        if (string.IsNullOrWhiteSpace(browserState.LastBundleId))
        {
            return false;
        }

        var bundleNode = _bundlePane.FindBundleNode(new BundleId(browserState.LastBundleId));
        if (bundleNode is null)
        {
            return false;
        }

        var restoredTab = new BundleDocumentTabViewModel(
            bundleNode.BundleId,
            bundleNode.DisplayName,
            bundleNode.LogicalPath,
            isPreview: true);
        restoredTab.RestoreViewState(
            searchKeyword: browserState.AssetSearchKeyword,
            selectedAssetTypeFilterKey: browserState.AssetTypeFilter,
            selectedAssetId: browserState.LastAssetId,
            showModifiedOnly: browserState.ShowModifiedOnly,
            showPreviewableOnly: browserState.ShowPreviewableOnly);

        _documentWorkspace.BundleTabs.Add(restoredTab);
        await _activateBundleTabAsync(restoredTab);
        return true;
    }

    private async Task PersistWorkspaceBrowserStateDebouncedAsync(CancellationTokenSource cancellationSource)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(260), cancellationSource.Token);
            PersistWorkspaceBrowserStateCore();
        }
        catch (OperationCanceledException)
        {
            // 连续状态变化时允许新的保存请求接管，取消旧请求属于预期行为。
        }
        finally
        {
            if (ReferenceEquals(_workspaceBrowserStatePersistenceCts, cancellationSource))
            {
                _workspaceBrowserStatePersistenceCts = null;
            }

            cancellationSource.Dispose();
        }
    }

    private void PersistWorkspaceBrowserStateCore()
    {
        if (!_hasWorkspaceProvider())
        {
            return;
        }

        _captureActiveTabStateWithoutPersistence();
        var snapshot = BuildWorkspaceBrowserStateSnapshot();
        _workspaceContextService.UpdateCurrentWorkspaceBrowserState(snapshot);
    }

    private WorkspaceBrowserStateSnapshotDto BuildWorkspaceBrowserStateSnapshot()
    {
        var activeTab = _documentWorkspace.ActiveBundleTab;

        return new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: _bundlePane.BundleSearchKeyword,
            AssetSearchKeyword: activeTab?.SearchKeyword ?? string.Empty,
            AssetTypeFilter: activeTab?.SelectedAssetTypeFilterKey,
            ShowModifiedOnly: activeTab?.ShowModifiedOnly ?? false,
            ShowPreviewableOnly: activeTab?.ShowPreviewableOnly ?? false,
            OpenBundleTabs: _documentWorkspace.BundleTabs
                .Select(CreateBundleTabMetadata)
                .ToArray(),
            ActiveBundleTabId: activeTab?.BundleId.Value,
            LastBundleId: activeTab?.BundleId.Value,
            LastAssetId: activeTab?.ActiveAssetTab?.AssetId.Value ?? activeTab?.SelectedAssetId,
            OpenAssetEditorWindows: CaptureAssetEditorWindowsForPersistence(),
            WindowSizes: _windowSizes);
    }

    private static bool IsValidWindowSize(WorkspaceWindowSizeDto size) =>
        size.Width > 0d &&
        size.Height > 0d &&
        double.IsFinite(size.Width) &&
        double.IsFinite(size.Height);

    private IReadOnlyList<WorkspaceAssetEditorWindowStateDto> CaptureAssetEditorWindowsForPersistence()
    {
        var openWindows = _captureOpenAssetEditorWindows();
        var pendingWindows = _capturePendingAssetEditorWindows();
        if (openWindows.Count == 0)
        {
            return pendingWindows;
        }

        if (pendingWindows.Count == 0)
        {
            return openWindows;
        }

        return openWindows
            .Concat(pendingWindows)
            .ToArray();
    }

    private static WorkspaceMetadataBundleTabDto CreateBundleTabMetadata(BundleDocumentTabViewModel tab)
    {
        return new WorkspaceMetadataBundleTabDto(
            BundleId: tab.BundleId.Value,
            LogicalPath: tab.LogicalPath,
            IsPreview: tab.IsPreview,
            SelectedAssetId: tab.SelectedAssetId,
            SearchKeyword: tab.SearchKeyword,
            AssetTypeFilter: tab.SelectedAssetTypeFilterKey,
            ShowModifiedOnly: tab.ShowModifiedOnly,
            ShowPreviewableOnly: tab.ShowPreviewableOnly,
            OpenAssetTabs: tab.AssetTabs
                .Select(CreateAssetTabMetadata)
                .ToArray(),
            ActiveAssetTabId: tab.ActiveAssetTab?.DocumentId,
            AssetListViewMode: AssetPaneViewModePersistence.ToPersistenceKey(tab.AssetPaneViewMode),
            OpenSpineGroupTabs: tab.SpineGroupTabs
                .Select(CreateSpineGroupTabMetadata)
                .ToArray(),
            ActiveSpineGroupTabId: tab.ActiveSpineGroupTab?.DocumentId);
    }

    private IReadOnlyList<AssetDocumentTabViewModel> RestoreAssetTabsForBundle(
        BundleId bundleId,
        IReadOnlyList<WorkspaceMetadataAssetTabDto>? assetTabStates)
    {
        if (assetTabStates is null || assetTabStates.Count == 0)
        {
            return Array.Empty<AssetDocumentTabViewModel>();
        }

        var restoredTabs = new List<AssetDocumentTabViewModel>(assetTabStates.Count);
        var restoredAssetIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assetTabState in assetTabStates)
        {
            if (string.IsNullOrWhiteSpace(assetTabState.AssetId) ||
                !restoredAssetIds.Add(assetTabState.AssetId))
            {
                continue;
            }

            var assetTab = _createAssetDocumentTab(
                bundleId,
                new AssetId(assetTabState.AssetId),
                assetTabState.AssetName);
            assetTab.RestoreSelectedInspectorSection(assetTabState.SelectedInspectorSection);
            assetTab.RestorePreviewImageDisplayOptions(
                assetTabState.PreviewImageBackgroundMode,
                assetTabState.SpritePreviewDisplayMode,
                assetTabState.PreviewImageZoom,
                assetTabState.PreviewImagePanX,
                assetTabState.PreviewImagePanY);
            assetTab.RestorePreviewMeshDisplayOptions(
                assetTabState.MeshPreviewDisplayMode,
                assetTabState.MeshPreviewTextureOverlaySelected,
                assetTabState.MeshPreviewGridSelected,
                assetTabState.MeshPreviewZoom,
                assetTabState.MeshPreviewRotationX,
                assetTabState.MeshPreviewRotationY);
            assetTab.RestoreEditDraftReference(
                AssetDocumentEditDraftReferenceMapper.ToViewModel(assetTabState.EditDraftReference));
            assetTab.RestoreInspectorScrollState(assetTabState.InspectorScrollState);
            restoredTabs.Add(assetTab);
        }

        return restoredTabs;
    }

    private IReadOnlyList<SpineAssetGroupDocumentTabViewModel> RestoreSpineGroupTabsForBundle(
        BundleId bundleId,
        IReadOnlyList<WorkspaceMetadataSpineGroupTabDto>? spineGroupTabStates)
    {
        if (_createSpineGroupDocumentTab is null ||
            spineGroupTabStates is null ||
            spineGroupTabStates.Count == 0)
        {
            return Array.Empty<SpineAssetGroupDocumentTabViewModel>();
        }

        var restoredTabs = new List<SpineAssetGroupDocumentTabViewModel>(spineGroupTabStates.Count);
        var restoredGroupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spineGroupTabState in spineGroupTabStates)
        {
            if (string.IsNullOrWhiteSpace(spineGroupTabState.GroupId) ||
                !restoredGroupIds.Add(spineGroupTabState.GroupId))
            {
                continue;
            }

            var spineGroupTab = _createSpineGroupDocumentTab(CreateRestoredSpineGroup(
                bundleId,
                spineGroupTabState));
            spineGroupTab.RestorePreviewState(spineGroupTabState.PreviewState);
            spineGroupTab.RestoreInspectorScrollState(spineGroupTabState.InspectorScrollState);
            restoredTabs.Add(spineGroupTab);
        }

        return restoredTabs;
    }

    private static WorkspaceMetadataAssetTabDto CreateAssetTabMetadata(AssetDocumentTabViewModel tab)
    {
        var imageDisplayOptions = tab.Inspector.Preview.Image.DisplayOptions;
        var imageViewState = tab.Inspector.Preview.Image;
        var meshDisplayOptions = tab.Inspector.Preview.Mesh;
        return new WorkspaceMetadataAssetTabDto(
            AssetId: tab.AssetId.Value,
            AssetName: tab.DisplayName,
            SelectedInspectorSection: tab.Inspector.SelectedSectionKey,
            PreviewImageBackgroundMode: imageDisplayOptions.BackgroundModePersistenceKey,
            SpritePreviewDisplayMode: imageDisplayOptions.SpriteDisplayModePersistenceKey,
            MeshPreviewDisplayMode: meshDisplayOptions.DisplayModePersistenceKey,
            MeshPreviewTextureOverlaySelected: meshDisplayOptions.IsTextureOverlaySelected,
            MeshPreviewGridSelected: meshDisplayOptions.IsMeshGridSelected,
            MeshPreviewZoom: meshDisplayOptions.Zoom,
            MeshPreviewRotationX: meshDisplayOptions.RotationX,
            MeshPreviewRotationY: meshDisplayOptions.RotationY,
            PreviewImageZoom: imageViewState.Zoom,
            PreviewImagePanX: imageViewState.PanX,
            PreviewImagePanY: imageViewState.PanY,
            EditDraftReference: AssetDocumentEditDraftReferenceMapper.ToDto(tab.EditDraftReference),
            InspectorScrollState: tab.Inspector.CaptureInspectorScrollState());
    }

    private static WorkspaceMetadataSpineGroupTabDto CreateSpineGroupTabMetadata(
        SpineAssetGroupDocumentTabViewModel tab)
    {
        return new WorkspaceMetadataSpineGroupTabDto(
            GroupId: tab.GroupId,
            DisplayName: tab.DisplayName,
            GroupKey: tab.Item.GroupKey,
            PreviewState: tab.CapturePreviewState(),
            InspectorScrollState: tab.Inspector.CaptureInspectorScrollState());
    }

    private static SpineAssetGroupDto CreateRestoredSpineGroup(
        BundleId bundleId,
        WorkspaceMetadataSpineGroupTabDto tabState)
    {
        var groupId = tabState.GroupId.Trim();
        return new SpineAssetGroupDto(
            GroupId: groupId,
            BundleId: bundleId,
            DisplayName: string.IsNullOrWhiteSpace(tabState.DisplayName)
                ? groupId
                : tabState.DisplayName.Trim(),
            GroupKey: string.IsNullOrWhiteSpace(tabState.GroupKey)
                ? groupId
                : tabState.GroupKey.Trim(),
            SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
            AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
            TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
    }
}
