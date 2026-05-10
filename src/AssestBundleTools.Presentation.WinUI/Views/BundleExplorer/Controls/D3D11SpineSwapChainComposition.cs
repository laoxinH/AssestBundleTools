using System.Numerics;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class D3D11SpineSwapChainComposition
{
    public static bool TryResolvePanelMetrics(
        double actualWidth,
        double actualHeight,
        double compositionScaleX,
        double compositionScaleY,
        double rasterizationScale,
        out D3D11SpinePanelMetrics metrics)
    {
        var scaleX = ResolveScale(compositionScaleX, rasterizationScale);
        var scaleY = ResolveScale(compositionScaleY, rasterizationScale);
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(actualWidth * scaleX));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(actualHeight * scaleY));
        metrics = new D3D11SpinePanelMetrics(pixelWidth, pixelHeight, scaleX, scaleY);
        return pixelWidth > 1 && pixelHeight > 1;
    }

    public static Matrix3x2 CreateInverseScaleTransform(double scaleX, double scaleY) =>
        Matrix3x2.CreateScale(
            (float)(1d / ResolveScale(scaleX, fallbackScale: 1d)),
            (float)(1d / ResolveScale(scaleY, fallbackScale: 1d)));

    public static double ResolveCheckerboardScale(double scaleX, double scaleY)
    {
        var normalizedScaleX = ResolveScale(scaleX, fallbackScale: 1d);
        var normalizedScaleY = ResolveScale(scaleY, fallbackScale: 1d);
        return (normalizedScaleX + normalizedScaleY) / 2d;
    }

    private static double ResolveScale(double compositionScale, double fallbackScale)
    {
        if (double.IsFinite(compositionScale) && compositionScale > 0d)
        {
            return compositionScale;
        }

        return double.IsFinite(fallbackScale) && fallbackScale > 0d
            ? fallbackScale
            : 1d;
    }
}

internal readonly record struct D3D11SpinePanelMetrics(
    int PixelWidth,
    int PixelHeight,
    double CompositionScaleX,
    double CompositionScaleY);
