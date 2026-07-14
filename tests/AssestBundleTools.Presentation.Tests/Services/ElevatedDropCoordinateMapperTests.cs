using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class ElevatedDropCoordinateMapperTests
{
    [Fact]
    public void TryCalculateRootPosition_ShouldCalibrateBothAxesFromClientAndRootSizes()
    {
        var nativePosition = new ElevatedNativeDropPosition(
            ClientX: 450,
            ClientY: 300,
            IsClientArea: true,
            ClientWidth: 1800,
            ClientHeight: 1200,
            ScreenX: 0,
            ScreenY: 0,
            HasScreenPosition: true);

        var succeeded = ElevatedDropCoordinateMapper.TryCalculateRootPosition(
            nativePosition,
            rootWidth: 1200,
            rootHeight: 600,
            rasterizationScale: 1.25,
            out var rootPosition,
            out var scaleX,
            out var scaleY);

        Assert.True(succeeded);
        Assert.Equal(1.5d, scaleX, 6);
        Assert.Equal(2d, scaleY, 6);
        Assert.Equal(300d, rootPosition.X, 6);
        Assert.Equal(150d, rootPosition.Y, 6);
    }

    [Fact]
    public void TryCalculateRootPosition_ShouldUseRasterizationScale_WhenClientBoundsUnavailable()
    {
        var nativePosition = new ElevatedNativeDropPosition(
            ClientX: 240,
            ClientY: 120,
            IsClientArea: true,
            ClientWidth: 0,
            ClientHeight: 0,
            ScreenX: 0,
            ScreenY: 0,
            HasScreenPosition: false);

        var succeeded = ElevatedDropCoordinateMapper.TryCalculateRootPosition(
            nativePosition,
            rootWidth: 800,
            rootHeight: 600,
            rasterizationScale: 1.5,
            out var rootPosition,
            out var scaleX,
            out var scaleY);

        Assert.True(succeeded);
        Assert.Equal(1.5d, scaleX, 6);
        Assert.Equal(1.5d, scaleY, 6);
        Assert.Equal(160d, rootPosition.X, 6);
        Assert.Equal(80d, rootPosition.Y, 6);
    }

    [Fact]
    public void TryCalculateRootPosition_ShouldRejectMissingRootDimensions()
    {
        var nativePosition = new ElevatedNativeDropPosition(
            ClientX: 10,
            ClientY: 20,
            IsClientArea: true,
            ClientWidth: 100,
            ClientHeight: 100,
            ScreenX: 0,
            ScreenY: 0,
            HasScreenPosition: false);

        var succeeded = ElevatedDropCoordinateMapper.TryCalculateRootPosition(
            nativePosition,
            rootWidth: 0,
            rootHeight: 100,
            rasterizationScale: 1,
            out _,
            out _,
            out _);

        Assert.False(succeeded);
    }
}
