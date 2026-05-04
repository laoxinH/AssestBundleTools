namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal readonly record struct PreviewImageOverlayLayout(
    double OverlayX,
    double OverlayY,
    double OverlayWidth,
    double OverlayHeight,
    double ImageScale,
    double ImageOffsetX,
    double ImageOffsetY);

internal static class PreviewImageOverlayLayoutCalculator
{
    public static bool TryCalculate(
        double sourceCanvasWidth,
        double sourceCanvasHeight,
        double overlaySourceX,
        double overlaySourceY,
        double overlaySourceWidth,
        double overlaySourceHeight,
        double viewportWidth,
        double viewportHeight,
        double zoom,
        double panX,
        double panY,
        out PreviewImageOverlayLayout layout)
    {
        layout = default;

        if (overlaySourceWidth <= 0d ||
            overlaySourceHeight <= 0d ||
            viewportWidth <= 0d ||
            viewportHeight <= 0d)
        {
            return false;
        }

        var sourceWidth = Math.Max(1d, sourceCanvasWidth);
        var sourceHeight = Math.Max(1d, sourceCanvasHeight);
        var baseScale = Math.Min(
            1d,
            Math.Min(
                viewportWidth / sourceWidth,
                viewportHeight / sourceHeight));
        var scale = baseScale * Math.Max(0.01d, zoom);
        if (scale <= 0d || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return false;
        }

        var renderedImageWidth = sourceWidth * scale;
        var renderedImageHeight = sourceHeight * scale;
        var imageOffsetX = ((viewportWidth - renderedImageWidth) / 2d) + panX;
        var imageOffsetY = ((viewportHeight - renderedImageHeight) / 2d) + panY;
        var overlayX = imageOffsetX + (Math.Max(0d, overlaySourceX) * scale);
        var overlayY = imageOffsetY + (Math.Max(0d, overlaySourceY) * scale);
        var overlayWidth = overlaySourceWidth * scale;
        var overlayHeight = overlaySourceHeight * scale;

        layout = new PreviewImageOverlayLayout(
            overlayX,
            overlayY,
            overlayWidth,
            overlayHeight,
            scale,
            imageOffsetX,
            imageOffsetY);
        return true;
    }

    public static bool TryCalculate(
        double sourceCanvasWidth,
        double sourceCanvasHeight,
        double overlaySourceX,
        double overlaySourceY,
        double overlaySourceWidth,
        double overlaySourceHeight,
        double viewportWidth,
        double viewportHeight,
        out PreviewImageOverlayLayout layout)
    {
        return TryCalculate(
            sourceCanvasWidth,
            sourceCanvasHeight,
            overlaySourceX,
            overlaySourceY,
            overlaySourceWidth,
            overlaySourceHeight,
            viewportWidth,
            viewportHeight,
            zoom: 1d,
            panX: 0d,
            panY: 0d,
            out layout);
    }
}
