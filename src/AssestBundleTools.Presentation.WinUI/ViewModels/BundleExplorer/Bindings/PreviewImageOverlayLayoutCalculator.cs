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
        var scale = Math.Min(
            1d,
            Math.Min(
                viewportWidth / sourceWidth,
                viewportHeight / sourceHeight));
        if (scale <= 0d || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return false;
        }

        var renderedImageWidth = sourceWidth * scale;
        var renderedImageHeight = sourceHeight * scale;
        var imageOffsetX = Math.Max(0d, (viewportWidth - renderedImageWidth) / 2d);
        var imageOffsetY = Math.Max(0d, (viewportHeight - renderedImageHeight) / 2d);
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
}
