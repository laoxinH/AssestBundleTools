using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetEditorWindowStateStoreTests
{
    [Fact]
    public void CreateNextWindowLayout_ShouldCascadeFromDefaultLayout()
    {
        var store = new AssetEditorWindowStateStore();

        var first = store.CreateNextWindowLayout();
        var second = store.CreateNextWindowLayout();

        Assert.Equal(
            new AssetEditorWindowLayoutSnapshot(
                AssetEditorWindowStateStore.DefaultX,
                AssetEditorWindowStateStore.DefaultY,
                AssetEditorWindowStateStore.DefaultWidth,
                AssetEditorWindowStateStore.DefaultHeight),
            first);
        Assert.Equal(
            first.Offset(
                AssetEditorWindowStateStore.CascadeOffset,
                AssetEditorWindowStateStore.CascadeOffset),
            second);
    }

    [Fact]
    public void RememberWindowLayout_ShouldCascadeFromRememberedLayout()
    {
        var store = new AssetEditorWindowStateStore();
        var remembered = new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900);

        store.RememberWindowLayout(remembered);
        var next = store.CreateNextWindowLayout();

        Assert.Equal(
            remembered.Offset(
                AssetEditorWindowStateStore.CascadeOffset,
                AssetEditorWindowStateStore.CascadeOffset),
            next);
    }

    [Fact]
    public void RememberWindowLayout_ShouldIgnoreTooSmallLayout()
    {
        var store = new AssetEditorWindowStateStore();
        var remembered = new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900);
        var tooSmall = new AssetEditorWindowLayoutSnapshot(12, 24, 320, 240);

        store.RememberWindowLayout(remembered);
        store.RememberWindowLayout(tooSmall);
        var next = store.CreateNextWindowLayout();

        Assert.Equal(
            remembered.Offset(
                AssetEditorWindowStateStore.CascadeOffset,
                AssetEditorWindowStateStore.CascadeOffset),
            next);
    }

    [Fact]
    public void Clear_ShouldResetToDefaultLayout()
    {
        var store = new AssetEditorWindowStateStore();
        store.RememberWindowLayout(new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900));
        store.RegisterWindow(
            new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900),
            CreateGroupSnapshot("bundle-a", "asset-1"));

        store.Clear();
        var next = store.CreateNextWindowLayout();

        Assert.Empty(store.CaptureOpenWindows());
        Assert.Equal(
            new AssetEditorWindowLayoutSnapshot(
                AssetEditorWindowStateStore.DefaultX,
                AssetEditorWindowStateStore.DefaultY,
                AssetEditorWindowStateStore.DefaultWidth,
                AssetEditorWindowStateStore.DefaultHeight),
            next);
    }

    [Fact]
    public void RegisterWindow_ShouldCaptureOpenWindowState()
    {
        var store = new AssetEditorWindowStateStore();
        var layout = new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900);
        var group = CreateGroupSnapshot("bundle-a", "asset-1");

        var windowStateId = store.RegisterWindow(layout, group);
        var windows = store.CaptureOpenWindows();

        var state = Assert.Single(windows);
        Assert.Equal(windowStateId, state.WindowStateId);
        Assert.Equal(layout, state.Layout);
        Assert.Same(group, state.Group);
    }

    [Fact]
    public void UpdateWindowState_ShouldReplaceTrackedLayoutAndGroup()
    {
        var store = new AssetEditorWindowStateStore();
        var windowStateId = store.RegisterWindow(
            new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900),
            CreateGroupSnapshot("bundle-a", "asset-1"));
        var nextLayout = new AssetEditorWindowLayoutSnapshot(480, 220, 1500, 940);
        var nextGroup = CreateGroupSnapshot("bundle-b", "asset-2");

        store.UpdateWindowLayout(windowStateId, nextLayout);
        store.UpdateWindowGroup(windowStateId, nextGroup);

        var state = Assert.Single(store.CaptureOpenWindows());
        Assert.Equal(nextLayout, state.Layout);
        Assert.Same(nextGroup, state.Group);
    }

    [Fact]
    public void CaptureOpenWindowMetadata_ShouldMapWindowAndAssetTabSnapshots()
    {
        var store = new AssetEditorWindowStateStore();
        var layout = new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900);
        store.RegisterWindow(
            layout,
            new AssetEditorWindowGroupSnapshot(
                new[]
                {
                    new AssetEditorWindowDocumentTabSnapshot(
                        WorkspaceAssetEditorWindowDocumentTabDto.AssetKind,
                        AssetTab: new AssetEditorWindowAssetTabSnapshot(
                            BundleId: "bundle-a",
                            AssetId: "asset-1",
                            DisplayName: "asset-1",
                            SelectedInspectorSection: "dependencies",
                            PreviewImageBackgroundMode: "Solid",
                            SpritePreviewDisplayMode: "CropResult",
                            MeshPreviewDisplayMode: "Uv",
                            MeshPreviewTextureOverlaySelected: false,
                            MeshPreviewGridSelected: true,
                            MeshPreviewZoom: 1.25d,
                            MeshPreviewRotationX: 12d,
                            MeshPreviewRotationY: -24d,
                            PreviewImageZoom: 1.5d,
                            PreviewImagePanX: 8d,
                            PreviewImagePanY: -4d,
                            EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                                "draft-1",
                                "text",
                                "source-v1",
                                "Text draft"))),
                    new AssetEditorWindowDocumentTabSnapshot(
                        WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind,
                        SpineGroupTab: new AssetEditorWindowSpineGroupTabSnapshot(
                            "bundle-a",
                            "spine-group:bundle-a:hero",
                            "hero",
                            "hero"))
                },
                new[]
                {
                    new AssetEditorWindowAssetTabSnapshot(
                        BundleId: "bundle-a",
                        AssetId: "asset-1",
                        DisplayName: "asset-1",
                        SelectedInspectorSection: "dependencies",
                        PreviewImageBackgroundMode: "Solid",
                        SpritePreviewDisplayMode: "CropResult",
                        MeshPreviewDisplayMode: "Uv",
                        MeshPreviewTextureOverlaySelected: false,
                        MeshPreviewGridSelected: true,
                        MeshPreviewZoom: 1.25d,
                        MeshPreviewRotationX: 12d,
                        MeshPreviewRotationY: -24d,
                        PreviewImageZoom: 1.5d,
                        PreviewImagePanX: 8d,
                        PreviewImagePanY: -4d,
                        EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                            "draft-1",
                            "text",
                            "source-v1",
                            "Text draft"))
                },
                WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind,
                "bundle-a",
                null,
                "spine-group:bundle-a:hero"));

        var window = Assert.Single(store.CaptureOpenWindowMetadata());
        var assetTab = Assert.Single(window.OpenAssetTabs);
        var documentTabs = window.OpenDocumentTabs ?? Array.Empty<WorkspaceAssetEditorWindowDocumentTabDto>();

        Assert.Equal(layout.X, window.Layout.X);
        Assert.Equal(layout.Y, window.Layout.Y);
        Assert.Equal(layout.Width, window.Layout.Width);
        Assert.Equal(layout.Height, window.Layout.Height);
        Assert.Equal("bundle-a", window.ActiveBundleId);
        Assert.Null(window.ActiveAssetId);
        Assert.Equal(WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind, window.ActiveDocumentKind);
        Assert.Equal("spine-group:bundle-a:hero", window.ActiveSpineGroupId);
        Assert.Collection(
            documentTabs,
            tab => Assert.Equal(WorkspaceAssetEditorWindowDocumentTabDto.AssetKind, tab.Kind),
            tab => Assert.Equal(WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind, tab.Kind));
        Assert.Equal("bundle-a", assetTab.BundleId);
        Assert.Equal("asset-1", assetTab.AssetId);
        Assert.Equal("asset-1", assetTab.AssetName);
        Assert.Equal("dependencies", assetTab.SelectedInspectorSection);
        Assert.Equal("Solid", assetTab.PreviewImageBackgroundMode);
        Assert.Equal("CropResult", assetTab.SpritePreviewDisplayMode);
        Assert.Equal("Uv", assetTab.MeshPreviewDisplayMode);
        Assert.False(assetTab.MeshPreviewTextureOverlaySelected);
        Assert.True(assetTab.MeshPreviewGridSelected);
        Assert.Equal(1.25d, assetTab.MeshPreviewZoom);
        Assert.Equal(12d, assetTab.MeshPreviewRotationX);
        Assert.Equal(-24d, assetTab.MeshPreviewRotationY);
        Assert.Equal(1.5d, assetTab.PreviewImageZoom);
        Assert.Equal(8d, assetTab.PreviewImagePanX);
        Assert.Equal(-4d, assetTab.PreviewImagePanY);
        Assert.Equal("draft-1", assetTab.EditDraftReference?.DraftId);
        Assert.Equal("text", assetTab.EditDraftReference?.DraftMode);
        Assert.Equal("source-v1", assetTab.EditDraftReference?.SourceFingerprint);
        Assert.Equal("Text draft", assetTab.EditDraftReference?.SourceDescription);
    }

    [Fact]
    public void CaptureOpenWindowMetadata_ShouldKeepSpineOnlyWindow()
    {
        var store = new AssetEditorWindowStateStore();
        var layout = new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900);
        store.RegisterWindow(
            layout,
            new AssetEditorWindowGroupSnapshot(
                new[]
                {
                    new AssetEditorWindowDocumentTabSnapshot(
                        WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind,
                        SpineGroupTab: new AssetEditorWindowSpineGroupTabSnapshot(
                            "bundle-a",
                            "spine-group:bundle-a:hero",
                            "hero",
                            "hero"))
                },
                Array.Empty<AssetEditorWindowAssetTabSnapshot>(),
                WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind,
                "bundle-a",
                null,
                "spine-group:bundle-a:hero"));

        var window = Assert.Single(store.CaptureOpenWindowMetadata());
        var documentTab = Assert.Single(window.OpenDocumentTabs ?? []);

        Assert.Empty(window.OpenAssetTabs);
        Assert.Equal(WorkspaceAssetEditorWindowDocumentTabDto.SpineGroupKind, window.ActiveDocumentKind);
        Assert.Equal("bundle-a", window.ActiveBundleId);
        Assert.Null(window.ActiveAssetId);
        Assert.Equal("spine-group:bundle-a:hero", window.ActiveSpineGroupId);
        Assert.Equal("bundle-a", documentTab.SpineGroupTab?.BundleId);
        Assert.Equal("spine-group:bundle-a:hero", documentTab.SpineGroupTab?.GroupId);
    }

    [Fact]
    public void RemoveWindow_ShouldDropOpenWindowSnapshot_ButKeepLayoutCascade()
    {
        var store = new AssetEditorWindowStateStore();
        var layout = new AssetEditorWindowLayoutSnapshot(420, 180, 1440, 900);
        var windowStateId = store.RegisterWindow(layout, CreateGroupSnapshot("bundle-a", "asset-1"));

        store.RemoveWindow(windowStateId);
        var next = store.CreateNextWindowLayout();

        Assert.Empty(store.CaptureOpenWindows());
        Assert.Equal(
            layout.Offset(
                AssetEditorWindowStateStore.CascadeOffset,
                AssetEditorWindowStateStore.CascadeOffset),
            next);
    }

    [Fact]
    public void ClampToWorkArea_ShouldMoveRestoredLayoutInsideVisibleBounds()
    {
        var layout = new AssetEditorWindowLayoutSnapshot(4800, -900, 1320, 820);
        var workArea = new AssetEditorWindowWorkAreaSnapshot(0, 0, 1920, 1080);

        var safeLayout = AssetEditorWindowLayoutSafety.ClampToWorkArea(layout, workArea);

        Assert.Equal(600, safeLayout.X);
        Assert.Equal(0, safeLayout.Y);
        Assert.Equal(1320, safeLayout.Width);
        Assert.Equal(820, safeLayout.Height);
    }

    [Fact]
    public void ClampToWorkArea_ShouldShrinkLayoutThatExceedsWorkArea()
    {
        var layout = new AssetEditorWindowLayoutSnapshot(120, 90, 2200, 1400);
        var workArea = new AssetEditorWindowWorkAreaSnapshot(0, 0, 1920, 1080);

        var safeLayout = AssetEditorWindowLayoutSafety.ClampToWorkArea(layout, workArea);

        Assert.Equal(0, safeLayout.X);
        Assert.Equal(0, safeLayout.Y);
        Assert.Equal(1920, safeLayout.Width);
        Assert.Equal(1080, safeLayout.Height);
    }

    private static AssetEditorWindowGroupSnapshot CreateGroupSnapshot(string bundleId, string assetId) =>
        new(
            new[]
            {
                new AssetEditorWindowDocumentTabSnapshot(
                    WorkspaceAssetEditorWindowDocumentTabDto.AssetKind,
                    AssetTab: new AssetEditorWindowAssetTabSnapshot(bundleId, assetId, assetId)),
            },
            new[]
            {
                new AssetEditorWindowAssetTabSnapshot(bundleId, assetId, assetId),
            },
            WorkspaceAssetEditorWindowDocumentTabDto.AssetKind,
            bundleId,
            assetId,
            null);
}
