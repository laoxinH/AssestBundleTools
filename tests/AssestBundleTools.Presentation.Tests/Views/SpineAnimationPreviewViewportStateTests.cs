using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SpineAnimationPreviewViewportStateTests
{
    [Fact]
    public void ZoomBy_ShouldKeepAnchorStable()
    {
        var state = new SpineAnimationPreviewViewportState();

        var changed = state.ZoomBy(
            scale: 2d,
            anchorX: 75d,
            anchorY: 25d,
            viewportWidth: 100d,
            viewportHeight: 100d);

        Assert.True(changed);
        Assert.Equal(2d, state.Current.Zoom, precision: 6);
        Assert.Equal(-25d, state.Current.PanX, precision: 6);
        Assert.Equal(25d, state.Current.PanY, precision: 6);
    }

    [Fact]
    public void ZoomBy_ShouldClampZoom()
    {
        var state = new SpineAnimationPreviewViewportState();

        state.ZoomBy(100d, 50d, 50d, 100d, 100d);

        Assert.Equal(SpineAnimationPreviewViewport.MaxZoom, state.Current.Zoom, precision: 6);
    }

    [Fact]
    public void Reset_ShouldReturnDefaultViewport()
    {
        var state = new SpineAnimationPreviewViewportState();
        state.PanBy(15d, -9d);
        state.ZoomBy(2d, 50d, 50d, 100d, 100d);

        var changed = state.Reset();

        Assert.True(changed);
        Assert.Equal(SpineAnimationPreviewViewport.Default, state.Current);
    }
}
