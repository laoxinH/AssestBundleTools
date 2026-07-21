using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
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
        assetTab.Inspector.CaptureSectionSelectorScrollPosition(horizontalOffset: 18d, verticalOffset: 0d);
        assetTab.Inspector.CaptureSectionScrollPosition(sectionIndex: 0, horizontalOffset: 3d, verticalOffset: 120d);
        assetTab.Inspector.CaptureSectionScrollPosition(sectionIndex: 1, horizontalOffset: 0d, verticalOffset: 48d);
        assetTab.SetEditDraftReference(
            new AssetDocumentEditDraftReference(
                "draft-asset-button",
                "text",
                "source-v1",
                "Text draft",
                IsAppliedBaseline: true,
                DraftRevision: 3));
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
        Assert.Equal(18d, assetTabState.InspectorScrollState?.SectionSelector?.HorizontalOffset);
        Assert.Equal(0d, assetTabState.InspectorScrollState?.SectionSelector?.VerticalOffset);
        Assert.Equal(3d, assetTabState.InspectorScrollState?.DetailsSection?.HorizontalOffset);
        Assert.Equal(120d, assetTabState.InspectorScrollState?.DetailsSection?.VerticalOffset);
        Assert.Equal(0d, assetTabState.InspectorScrollState?.DependenciesSection?.HorizontalOffset);
        Assert.Equal(48d, assetTabState.InspectorScrollState?.DependenciesSection?.VerticalOffset);
        Assert.Equal("Uv", assetTabState.MeshPreviewDisplayMode);
        Assert.False(assetTabState.MeshPreviewTextureOverlaySelected);
        Assert.False(assetTabState.MeshPreviewGridSelected);
        Assert.Equal(1.5d, assetTabState.MeshPreviewZoom);
        Assert.Equal(24d, assetTabState.MeshPreviewRotationX);
        Assert.Equal(-72d, assetTabState.MeshPreviewRotationY);
        Assert.Equal("draft-asset-button", assetTabState.EditDraftReference?.DraftId);
        Assert.Equal("text", assetTabState.EditDraftReference?.DraftMode);
        Assert.Equal("source-v1", assetTabState.EditDraftReference?.SourceFingerprint);
        Assert.Equal("Text draft", assetTabState.EditDraftReference?.SourceDescription);
        Assert.True(assetTabState.EditDraftReference?.IsAppliedBaseline);
        Assert.Equal(3, assetTabState.EditDraftReference?.DraftRevision);
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
    public void PersistWorkspaceBrowserStateNow_ShouldIncludeAssetPaneViewMode()
    {
        var context = CreateContext();
        context.BundleTab.RestoreViewState(
            searchKeyword: "hero",
            selectedAssetTypeFilterKey: null,
            selectedAssetId: null,
            showModifiedOnly: false,
            showPreviewableOnly: false,
            assetPaneViewMode: "SpineGroups");

        context.Coordinator.PersistWorkspaceBrowserStateNow();

        var bundleTabState = Assert.Single(context.WorkspaceContextService.CurrentBrowserState!.OpenBundleTabs);
        Assert.Equal("SpineGroups", bundleTabState.AssetListViewMode);
    }

    [Fact]
    public void PersistWorkspaceBrowserStateNow_ShouldIncludeSpineGroupDocumentTabs()
    {
        var context = CreateContext();
        var spineGroupTab = CreateSpineGroupTab(
            new BundleId("bundle-ui"),
            "spine-group:bundle-ui:hero",
            "Hero Spine",
            context.LocalizationService);
        spineGroupTab.RestorePreviewState(new WorkspaceSpinePreviewStateDto(
            SelectedAnimationName: "Special",
            SelectedSkinName: "default",
            PlaybackSpeed: 1.5d,
            PlaybackTime: 2.25d,
            IsPlaying: false,
            BackgroundMode: "Checkerboard",
            ViewportZoom: 2d,
            ViewportPanX: 24d,
            ViewportPanY: -16d));
        spineGroupTab.Inspector.CaptureSectionScrollPosition(sectionIndex: 0, horizontalOffset: 0d, verticalOffset: 180d);
        context.BundleTab.AddSpineGroupTab(spineGroupTab, activate: true);
        context.BundleTab.RestoreViewState(
            searchKeyword: "hero",
            selectedAssetTypeFilterKey: null,
            selectedAssetId: null,
            showModifiedOnly: false,
            showPreviewableOnly: false,
            assetPaneViewMode: "SpineGroups");

        context.Coordinator.PersistWorkspaceBrowserStateNow();

        var bundleTabState = Assert.Single(context.WorkspaceContextService.CurrentBrowserState!.OpenBundleTabs);
        var spineGroupTabState = Assert.Single(bundleTabState.OpenSpineGroupTabs!);
        Assert.Equal("spine-group:bundle-ui:hero", spineGroupTabState.GroupId);
        Assert.Equal("Hero Spine", spineGroupTabState.DisplayName);
        Assert.Equal("Special", spineGroupTabState.PreviewState?.SelectedAnimationName);
        Assert.Equal("default", spineGroupTabState.PreviewState?.SelectedSkinName);
        Assert.Equal(1.5d, spineGroupTabState.PreviewState?.PlaybackSpeed);
        Assert.Equal(2.25d, spineGroupTabState.PreviewState?.PlaybackTime);
        Assert.False(spineGroupTabState.PreviewState?.IsPlaying ?? true);
        Assert.Equal("Checkerboard", spineGroupTabState.PreviewState?.BackgroundMode);
        Assert.Equal(2d, spineGroupTabState.PreviewState?.ViewportZoom);
        Assert.Equal(24d, spineGroupTabState.PreviewState?.ViewportPanX);
        Assert.Equal(-16d, spineGroupTabState.PreviewState?.ViewportPanY);
        Assert.Equal(0d, spineGroupTabState.InspectorScrollState?.DetailsSection?.HorizontalOffset);
        Assert.Equal(180d, spineGroupTabState.InspectorScrollState?.DetailsSection?.VerticalOffset);
        Assert.Equal("spine-group:bundle-ui:hero", bundleTabState.ActiveSpineGroupTabId);
        Assert.Null(bundleTabState.ActiveAssetTabId);
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
                            PreviewImagePanY: -12d,
                            EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                                "draft-asset-button",
                                "text",
                                "source-v1",
                                "Text draft"),
                            InspectorScrollState: new WorkspaceInspectorScrollStateDto(
                                DetailsSection: new WorkspaceInspectorSectionScrollPositionDto(2d, 140d),
                                DependenciesSection: new WorkspaceInspectorSectionScrollPositionDto(0d, 64d),
                                SectionSelector: new WorkspaceInspectorSectionScrollPositionDto(22d, 0d)))
                    ],
                    ActiveAssetTabId: "asset-button",
                    AssetListViewMode: "SpineGroups")
            ],
            ActiveBundleTabId: "bundle-ui",
            LastBundleId: "bundle-ui",
            LastAssetId: "asset-button");

        var restored = await context.Coordinator.TryRestoreWorkspaceBrowserStateAsync();

        Assert.True(restored);
        Assert.Equal("Unknown:Shader", context.DocumentWorkspace.ActiveBundleTab!.SelectedAssetTypeFilterKey);
        Assert.Equal(AssetType.Unknown, context.DocumentWorkspace.ActiveBundleTab.SelectedAssetType);
        Assert.Equal(AssetPaneViewMode.SpineGroups, context.DocumentWorkspace.ActiveBundleTab.AssetPaneViewMode);
        var restoredAssetTab = Assert.Single(context.DocumentWorkspace.ActiveBundleTab!.AssetTabs);
        Assert.True(restoredAssetTab.Inspector.Preview.Image.DisplayOptions.IsSolidBackgroundSelected);
        Assert.True(restoredAssetTab.Inspector.Preview.Image.DisplayOptions.IsSpriteCropResultSelected);
        Assert.Equal(1.75d, restoredAssetTab.Inspector.Preview.Image.Zoom);
        Assert.Equal(18d, restoredAssetTab.Inspector.Preview.Image.PanX);
        Assert.Equal(-12d, restoredAssetTab.Inspector.Preview.Image.PanY);
        Assert.Equal(new InspectorPanelScrollPosition(22d, 0d), restoredAssetTab.Inspector.GetSectionSelectorScrollPosition());
        Assert.Equal(new InspectorPanelScrollPosition(2d, 140d), restoredAssetTab.Inspector.GetSectionScrollPosition(0));
        Assert.Equal(new InspectorPanelScrollPosition(0d, 64d), restoredAssetTab.Inspector.GetSectionScrollPosition(1));
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
        Assert.True(restoredAssetTab.HasEditDraft);
        Assert.Equal("draft-asset-button", restoredAssetTab.EditDraftReference?.DraftId);
        Assert.Equal("text", restoredAssetTab.EditDraftReference?.DraftMode);
        Assert.Equal("source-v1", restoredAssetTab.EditDraftReference?.SourceFingerprint);
        Assert.Equal("Text draft", restoredAssetTab.EditDraftReference?.SourceDescription);
    }

    [Fact]
    public async Task TryRestoreWorkspaceBrowserStateAsync_ShouldRestoreSpineGroupDocumentTabs()
    {
        var context = CreateContext();
        context.WorkspaceContextService.CurrentBrowserState = new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: "hero",
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
                    SearchKeyword: "hero",
                    AssetTypeFilter: null,
                    ShowModifiedOnly: false,
                    ShowPreviewableOnly: false,
                    OpenAssetTabs: Array.Empty<WorkspaceMetadataAssetTabDto>(),
                    ActiveAssetTabId: null,
                    AssetListViewMode: "SpineGroups",
                    OpenSpineGroupTabs:
                    [
                        new WorkspaceMetadataSpineGroupTabDto(
                            "spine-group:bundle-ui:hero",
                            "Hero Spine",
                            "assets/hero/hero",
                            new WorkspaceSpinePreviewStateDto(
                                SelectedAnimationName: "Sleep",
                                SelectedSkinName: "summer",
                                PlaybackSpeed: 0.5d,
                                PlaybackTime: 1.25d,
                                IsPlaying: true,
                                BackgroundMode: "Checkerboard",
                                ViewportZoom: 1.75d,
                                ViewportPanX: 10d,
                                ViewportPanY: -20d),
                            InspectorScrollState: new WorkspaceInspectorScrollStateDto(
                                DetailsSection: new WorkspaceInspectorSectionScrollPositionDto(0d, 220d)))
                    ],
                    ActiveSpineGroupTabId: "spine-group:bundle-ui:hero")
            ],
            ActiveBundleTabId: "bundle-ui",
            LastBundleId: "bundle-ui",
            LastAssetId: null);

        var restored = await context.Coordinator.TryRestoreWorkspaceBrowserStateAsync();

        Assert.True(restored);
        var activeBundleTab = context.DocumentWorkspace.ActiveBundleTab!;
        Assert.Equal(AssetPaneViewMode.SpineGroups, activeBundleTab.AssetPaneViewMode);
        Assert.Null(activeBundleTab.ActiveAssetTab);
        var spineGroupTab = Assert.Single(activeBundleTab.SpineGroupTabs);
        Assert.Same(spineGroupTab, activeBundleTab.ActiveSpineGroupTab);
        Assert.Equal("spine-group:bundle-ui:hero", spineGroupTab.GroupId);
        Assert.Equal("Hero Spine", spineGroupTab.DisplayName);
        Assert.Equal("assets/hero/hero", spineGroupTab.Item.GroupKey);
        Assert.Equal("Sleep", spineGroupTab.PreviewState?.SelectedAnimationName);
        Assert.Equal("summer", spineGroupTab.PreviewState?.SelectedSkinName);
        Assert.Equal(0.5d, spineGroupTab.PreviewState?.PlaybackSpeed);
        Assert.Equal(1.25d, spineGroupTab.PreviewState?.PlaybackTime);
        Assert.Equal(1.75d, spineGroupTab.Inspector.Preview.SpineAnimation.ViewportZoom);
        Assert.Equal(10d, spineGroupTab.Inspector.Preview.SpineAnimation.ViewportPanX);
        Assert.Equal(-20d, spineGroupTab.Inspector.Preview.SpineAnimation.ViewportPanY);
        Assert.Equal(new InspectorPanelScrollPosition(0d, 220d), spineGroupTab.Inspector.GetSectionScrollPosition(0));
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

    [Fact]
    public async Task WindowSizes_ShouldRestoreAndPersistPerWorkspaceWindowKind()
    {
        var context = CreateContext();
        context.WorkspaceContextService.CurrentBrowserState = new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: null,
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs: Array.Empty<WorkspaceMetadataBundleTabDto>(),
            ActiveBundleTabId: null,
            LastBundleId: null,
            LastAssetId: null,
            WindowSizes: new WorkspaceWindowSizePreferencesDto(
                BatchExtraction: new WorkspaceWindowSizeDto(1080d, 700d)));

        await context.Coordinator.TryRestoreWorkspaceBrowserStateAsync();
        var restored = context.Coordinator.GetRememberedWindowSize(
            WorkspaceWindowSizeKind.BatchExtraction);
        var changed = context.Coordinator.RememberWindowSize(
            WorkspaceWindowSizeKind.Export,
            new WorkspaceWindowSizeDto(1200d, 740d));
        context.Coordinator.PersistWorkspaceBrowserStateNow();

        Assert.Equal(new WorkspaceWindowSizeDto(1080d, 700d), restored);
        Assert.True(changed);
        Assert.Equal(
            new WorkspaceWindowSizeDto(1080d, 700d),
            context.WorkspaceContextService.CurrentBrowserState?.WindowSizes?.BatchExtraction);
        Assert.Equal(
            new WorkspaceWindowSizeDto(1200d, 740d),
            context.WorkspaceContextService.CurrentBrowserState?.WindowSizes?.Export);
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
            createSpineGroupDocumentTab: group =>
                CreateSpineGroupTab(group.BundleId, group.GroupId, group.DisplayName, localizationService, group.GroupKey),
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

    private static SpineAssetGroupDocumentTabViewModel CreateSpineGroupTab(
        BundleId bundleId,
        string groupId,
        string displayName,
        TestLocalizationService localizationService,
        string? groupKey = null)
    {
        return new SpineAssetGroupDocumentTabViewModel(
            new SpineAssetGroupDto(
                groupId,
                bundleId,
                displayName,
                groupKey ?? groupId,
                SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
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
