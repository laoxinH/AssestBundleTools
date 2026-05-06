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

    private static AssetEditorWindowGroupSnapshot CreateGroupSnapshot(string bundleId, string assetId) =>
        new(
            new[]
            {
                new AssetEditorWindowAssetTabSnapshot(bundleId, assetId, assetId),
            },
            bundleId,
            assetId);
}
