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

        store.Clear();
        var next = store.CreateNextWindowLayout();

        Assert.Equal(
            new AssetEditorWindowLayoutSnapshot(
                AssetEditorWindowStateStore.DefaultX,
                AssetEditorWindowStateStore.DefaultY,
                AssetEditorWindowStateStore.DefaultWidth,
                AssetEditorWindowStateStore.DefaultHeight),
            next);
    }
}
