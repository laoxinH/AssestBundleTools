using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class D3D11SpineSwapChainCompositionTests
{
    [Fact]
    public void TryResolvePanelMetrics_ShouldUseCompositionScaleForPhysicalBufferSize()
    {
        var resolved = D3D11SpineSwapChainComposition.TryResolvePanelMetrics(
            actualWidth: 314.4d,
            actualHeight: 162.4d,
            compositionScaleX: 1.25d,
            compositionScaleY: 1.25d,
            rasterizationScale: 1d,
            out var metrics);

        Assert.True(resolved);
        Assert.Equal(393, metrics.PixelWidth);
        Assert.Equal(203, metrics.PixelHeight);
        Assert.Equal(1.25d, metrics.CompositionScaleX, precision: 6);
        Assert.Equal(1.25d, metrics.CompositionScaleY, precision: 6);
    }

    [Fact]
    public void TryResolvePanelMetrics_ShouldFallbackToRasterizationScale()
    {
        var resolved = D3D11SpineSwapChainComposition.TryResolvePanelMetrics(
            actualWidth: 100d,
            actualHeight: 100d,
            compositionScaleX: 0d,
            compositionScaleY: double.NaN,
            rasterizationScale: 1.5d,
            out var metrics);

        Assert.True(resolved);
        Assert.Equal(150, metrics.PixelWidth);
        Assert.Equal(150, metrics.PixelHeight);
        Assert.Equal(1.5d, metrics.CompositionScaleX, precision: 6);
        Assert.Equal(1.5d, metrics.CompositionScaleY, precision: 6);
    }

    [Fact]
    public void CreateInverseScaleTransform_ShouldMapPhysicalBufferBackToXamlSpace()
    {
        var matrix = D3D11SpineSwapChainComposition.CreateInverseScaleTransform(1.25d, 2d);

        Assert.Equal(0.8f, matrix.M11, precision: 6);
        Assert.Equal(0.5f, matrix.M22, precision: 6);
        Assert.Equal(0f, matrix.M31, precision: 6);
        Assert.Equal(0f, matrix.M32, precision: 6);
    }
}
