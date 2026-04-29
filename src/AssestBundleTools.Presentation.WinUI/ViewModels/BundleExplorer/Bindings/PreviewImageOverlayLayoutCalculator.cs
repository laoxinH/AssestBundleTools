namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal readonly record struct PreviewImageOverlayLayout(
    double OverlayX,
    double OverlayY,
    double OverlayWidth,
    double OverlayHeight,
    double LabelX,
    double LabelY);

internal static class PreviewImageOverlayLayoutCalculator
{
    private const double LabelInset = 10d;
    private const double PreferredLabelWidth = 240d;
    private const double PreferredLabelHeight = 40d;
    private const double LabelGap = 10d;

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
            ComputeLabelX(overlayX, viewportWidth),
            ComputeLabelY(overlayY, overlayHeight, viewportHeight));
        return true;
    }

    private static double ComputeLabelX(
        double overlayX,
        double canvasWidth)
    {
        var preferredX = overlayX + LabelInset;
        var maxAllowedX = Math.Max(0d, canvasWidth - PreferredLabelWidth);
        return Math.Min(Math.Max(0d, preferredX), maxAllowedX);
    }

    private static double ComputeLabelY(
        double overlayY,
        double overlayHeight,
        double canvasHeight)
    {
        var aboveLabelY = overlayY - (PreferredLabelHeight + LabelGap);
        if (aboveLabelY >= 0d)
        {
            return aboveLabelY;
        }

        var belowLabelY = overlayY + overlayHeight + LabelGap;
        var maxAllowedY = Math.Max(0d, canvasHeight - PreferredLabelHeight);
        return Math.Min(Math.Max(0d, belowLabelY), maxAllowedY);
    }
}
