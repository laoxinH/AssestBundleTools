using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerBrowserStateCoordinatorTests
{
    [Fact]
    public void PersistWorkspaceBrowserStateNow_ShouldIncludeAssetPreviewDisplayOptions()
    {
        var context = CreateContext();
        var assetTab = CreateAssetTab(
            new BundleId("bundle-ui"),
            new AssetId("asset-button"),
            "Button",
            context.LocalizationService);
        assetTab.Inspector.Preview.Image.DisplayOptions.UseSolidBackground();
        assetTab.Inspector.Preview.Image.DisplayOptions.UseSpriteCropResult();
        assetTab.RestorePreviewImageDisplayOptions(
            "Solid",
            "CropResult",
            zoom: 1.75d,
            panX: 18d,
            panY: -12d);
        assetTab.RestorePreviewMeshDisplayOptions(
            "Uv",
            textureOverlaySelected: false,
            meshGridSelected: false,
            zoom: 1.5d,
            rotationX: 24d,
            rotationY: -72d);
        context.BundleTab.AddAssetTab(assetTab, activate: true);

        context.Coordinator.PersistWorkspaceBrowserStateNow();

        Assert.NotNull(context.WorkspaceContextService.CurrentBrowserState);
        var browserState = context.WorkspaceContextService.CurrentBrowserState!;
        var assetTabState = Assert.Single(Assert.Single(browserState.OpenBundleTabs).OpenAssetTabs!);
        Assert.Equal("Solid", assetTabState.PreviewImageBackgroundMode);
        Assert.Equal("CropResult", assetTabState.SpritePreviewDisplayMode);
        Assert.Equal(1.75d, assetTabState.PreviewImageZoom);
        Assert.Equal(18d, assetTabState.PreviewImagePanX);
        Assert.Equal(-12d, assetTabState.PreviewImagePanY);
        Assert.Equal("Uv", assetTabState.MeshPreviewDisplayMode);
        Assert.False(assetTabState.MeshPreviewTextureOverlaySelected);
        Assert.False(assetTabState.MeshPreviewGridSelected);
        Assert.Equal(1.5d, assetTabState.MeshPreviewZoom);
        Assert.Equal(24d, assetTabState.MeshPreviewRotationX);
        Assert.Equal(-72d, assetTabState.MeshPreviewRotationY);
    }

    [Fact]
    public void PersistWorkspaceBrowserStateNow_ShouldRoundTripDynamicAssetTypeFilterKey()
    {
        var context = CreateContext();
        context.BundleTab.RestoreViewState(
            searchKeyword: "shader",
            selectedAssetTypeFilterKey: "Unknown:Shader",
            selectedAssetId: "asset-shader",
            showModifiedOnly: true,
            showPreviewableOnly: false);

        context.Coordinator.PersistWorkspaceBrowserStateNow();

        Assert.NotNull(context.WorkspaceContextService.CurrentBrowserState);
        var browserState = context.WorkspaceContextService.CurrentBrowserState!;
        Assert.Equal("Unknown:Shader", browserState.AssetTypeFilter);
        var bundleTabState = Assert.Single(browserState.OpenBundleTabs);
        Assert.Equal("Unknown:Shader", bundleTabState.AssetTypeFilter);
        Assert.Equal(AssetType.Unknown, context.BundleTab.SelectedAssetType);
    }

    [Fact]
    public void PersistWorkspaceBrowserStateNow_ShouldIncludeAssetEditorWindowSnapshots()
    {
        var editorWindows = new[]
        {
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 4,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-ui", "asset-button", "Button")
                ],
                ActiveBundleId: "bundle-ui",
                ActiveAssetId: "asset-button")
        };
        var context = CreateContext(editorWindows);

        context.Coordinator.PersistWorkspaceBrowserStateNow();

        Assert.Same(editorWindows, context.WorkspaceContextService.CurrentBrowserState!.OpenAssetEditorWindows);
    }

    [Fact]
    public async Task TryRestoreWorkspaceBrowserStateAsync_ShouldRestoreAssetPreviewDisplayOptions()
    {
        var context = CreateContext();
        context.WorkspaceContextService.CurrentBrowserState = new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: "Unknown:Shader",
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs:
            [
                new WorkspaceMetadataBundleTabDto(
                    BundleId: "bundle-ui",
                    LogicalPath: "ui.ab",
                    IsPreview: false,
                    SelectedAssetId: "asset-button",
                    SearchKeyword: string.Empty,
                    AssetTypeFilter: "Unknown:Shader",
                    ShowModifiedOnly: false,
                    ShowPreviewableOnly: false,
                    OpenAssetTabs:
                    [
                        new WorkspaceMetadataAssetTabDto(
                            AssetId: "asset-button",
                            AssetName: "Button",
                            SelectedInspectorSection: "details",
                            PreviewImageBackgroundMode: "Solid",
                            SpritePreviewDisplayMode: "CropResult",
                            MeshPreviewDisplayMode: "Uv",
                            MeshPreviewTextureOverlaySelected: false,
                            MeshPreviewGridSelected: false,
                            MeshPreviewZoom: 1.5d,
                            MeshPreviewRotationX: 24d,
                            MeshPreviewRotationY: -72d,
                            PreviewImageZoom: 1.75d,
                            PreviewImagePanX: 18d,
                            PreviewImagePanY: -12d)
                    ],
                    ActiveAssetTabId: "asset-button")
            ],
            ActiveBundleTabId: "bundle-ui",
            LastBundleId: "bundle-ui",
            LastAssetId: "asset-button");

        var restored = await context.Coordinator.TryRestoreWorkspaceBrowserStateAsync();

        Assert.True(restored);
        Assert.Equal("Unknown:Shader", context.DocumentWorkspace.ActiveBundleTab!.SelectedAssetTypeFilterKey);
        Assert.Equal(AssetType.Unknown, context.DocumentWorkspace.ActiveBundleTab.SelectedAssetType);
        var restoredAssetTab = Assert.Single(context.DocumentWorkspace.ActiveBundleTab!.AssetTabs);
        Assert.True(restoredAssetTab.Inspector.Preview.Image.DisplayOptions.IsSolidBackgroundSelected);
        Assert.True(restoredAssetTab.Inspector.Preview.Image.DisplayOptions.IsSpriteCropResultSelected);
        Assert.Equal(1.75d, restoredAssetTab.Inspector.Preview.Image.Zoom);
        Assert.Equal(18d, restoredAssetTab.Inspector.Preview.Image.PanX);
        Assert.Equal(-12d, restoredAssetTab.Inspector.Preview.Image.PanY);
        restoredAssetTab.Inspector.Preview.Mesh.Apply(
            CreateMeshGeometry(),
            new MeshPreviewDisplayState("wire", "uv"));
        restoredAssetTab.Inspector.Preview.Mesh.ApplyTextureOverlay([1, 2, 3, 4]);
        Assert.True(restoredAssetTab.Inspector.Preview.Mesh.IsUvSelected);
        Assert.False(restoredAssetTab.Inspector.Preview.Mesh.IsTextureOverlaySelected);
        Assert.False(restoredAssetTab.Inspector.Preview.Mesh.IsMeshGridSelected);
        Assert.Equal(1.5d, restoredAssetTab.Inspector.Preview.Mesh.Zoom);
        Assert.Equal(24d, restoredAssetTab.Inspector.Preview.Mesh.RotationX);
        Assert.Equal(-72d, restoredAssetTab.Inspector.Preview.Mesh.RotationY);
    }

    [Fact]
    public async Task TryRestoreWorkspaceBrowserStateAsync_ShouldPublishPendingAssetEditorWindowsAndPreserveThemOnPersist()
    {
        var context = CreateContext();
        context.DocumentWorkspace.Clear();
        var pendingEditorWindows = new[]
        {
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 7,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(240, 160, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-ui", "asset-button", "Button")
                ],
                ActiveBundleId: "bundle-ui",
                ActiveAssetId: "asset-button")
        };
        context.WorkspaceContextService.CurrentBrowserState = new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: null,
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs:
            [
                new WorkspaceMetadataBundleTabDto(
                    BundleId: "bundle-ui",
                    LogicalPath: "ui.ab",
                    IsPreview: false,
                    SelectedAssetId: null,
                    SearchKeyword: string.Empty,
                    AssetTypeFilter: null,
                    ShowModifiedOnly: false,
                    ShowPreviewableOnly: false,
                    OpenAssetTabs: Array.Empty<WorkspaceMetadataAssetTabDto>(),
                    ActiveAssetTabId: null)
            ],
            ActiveBundleTabId: "bundle-ui",
            LastBundleId: "bundle-ui",
            LastAssetId: null,
            OpenAssetEditorWindows: pendingEditorWindows);

        var restored = await context.Coordinator.TryRestoreWorkspaceBrowserStateAsync();

        Assert.True(restored);
        var pendingWindow = Assert.Single(context.RestorePrompt.PendingWindows);
        Assert.Equal(7, pendingWindow.WindowStateId);
        Assert.Equal("bundle-ui", pendingWindow.ActiveBundleId);

        var persistedBrowserState = context.WorkspaceContextService.CurrentBrowserState!;
        var persistedWindow = Assert.Single(persistedBrowserState.OpenAssetEditorWindows!);
        Assert.Equal(7, persistedWindow.WindowStateId);
        Assert.Equal("asset-button", Assert.Single(persistedWindow.OpenAssetTabs).AssetId);
    }

    private static BrowserStateCoordinatorContext CreateContext(
        IReadOnlyList<WorkspaceAssetEditorWindowStateDto>? openAssetEditorWindows = null)
    {
        var localizationService = new TestLocalizationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var restorePrompt = new AssetEditorWindowRestorePromptViewModel(localizationService);
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        bundlePane.RebuildBundleGroups(PresentationTestDataFactory.CreateBundleTree("ui"));
        var documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        var bundleTab = new BundleDocumentTabViewModel(
            new BundleId("bundle-ui"),
            "ui.ab",
            "ui.ab",
            isPreview: false);
        documentWorkspace.BundleTabs.Add(bundleTab);
        documentWorkspace.SetActiveBundleTab(bundleTab);

        var coordinator = new BundleExplorerBrowserStateCoordinator(
            workspaceContextService,
            bundlePane,
            documentWorkspace,
            hasWorkspaceProvider: static () => true,
            captureActiveTabStateWithoutPersistence: static () => { },
            createAssetDocumentTab: (bundleId, assetId, assetName) =>
                CreateAssetTab(bundleId, assetId, assetName, localizationService),
            activateBundleTabAsync: tab =>
            {
                documentWorkspace.SetActiveBundleTab(tab);
                return Task.CompletedTask;
            },
            captureOpenAssetEditorWindows: () => openAssetEditorWindows ?? Array.Empty<WorkspaceAssetEditorWindowStateDto>(),
            restorePendingAssetEditorWindows: snapshots => restorePrompt.ReplacePendingSnapshots(snapshots),
            capturePendingAssetEditorWindows: () => restorePrompt.PendingWindows);

        return new BrowserStateCoordinatorContext(
            localizationService,
            workspaceContextService,
            documentWorkspace,
            bundleTab,
            restorePrompt,
            coordinator);
    }

    private static AssetDocumentTabViewModel CreateAssetTab(
        BundleId bundleId,
        AssetId assetId,
        string assetName,
        TestLocalizationService localizationService)
    {
        return new AssetDocumentTabViewModel(
            bundleId,
            assetId,
            assetName,
            InspectorInformationTextSetFactory.Create(localizationService));
    }

    private static AssetPreviewMeshGeometryDto CreateMeshGeometry()
    {
        return new AssetPreviewMeshGeometryDto(
            SchemaVersion: 3,
            SourceVertexCount: 3,
            SourceIndexCount: 3,
            SourceTriangleCount: 1,
            IsGeometryTruncated: false,
            Vertices:
            [
                new AssetPreviewMeshVertexDto(0, 0, 0),
                new AssetPreviewMeshVertexDto(1, 0, 0),
                new AssetPreviewMeshVertexDto(0, 1, 0)
            ],
            Edges:
            [
                new AssetPreviewMeshEdgeDto(0, 1),
                new AssetPreviewMeshEdgeDto(1, 2),
                new AssetPreviewMeshEdgeDto(0, 2)
            ],
            SubMeshes: Array.Empty<AssetPreviewMeshSubMeshDto>(),
            Bounds: null,
            Uv0:
            [
                new AssetPreviewMeshUvDto(0, 0),
                new AssetPreviewMeshUvDto(1, 0),
                new AssetPreviewMeshUvDto(0, 1)
            ],
            Channels: Array.Empty<AssetPreviewMeshChannelDto>(),
            Triangles:
            [
                new AssetPreviewMeshTriangleDto(0, 1, 2, 0)
            ]);
    }

    private sealed record BrowserStateCoordinatorContext(
        TestLocalizationService LocalizationService,
        TestWorkspaceContextService WorkspaceContextService,
        BundleExplorerDocumentWorkspaceViewModel DocumentWorkspace,
        BundleDocumentTabViewModel BundleTab,
        AssetEditorWindowRestorePromptViewModel RestorePrompt,
        BundleExplorerBrowserStateCoordinator Coordinator);
}
