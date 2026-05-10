namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal readonly record struct SpineAnimationPreviewViewport(
    double Zoom,
    double PanX,
    double PanY)
{
    public const double DefaultZoom = 1d;
    public const double MinZoom = 0.2d;
    public const double MaxZoom = 8d;

    public static SpineAnimationPreviewViewport Default { get; } = new(DefaultZoom, 0d, 0d);

    public SpineAnimationPreviewViewport Normalize() =>
        new(
            NormalizeZoom(Zoom),
            NormalizePan(PanX),
            NormalizePan(PanY));

    public SpineAnimationPreviewViewport ScalePan(double scale)
    {
        var normalizedScale = double.IsFinite(scale) && scale > 0d
            ? scale
            : 1d;
        return ScalePan(normalizedScale, normalizedScale);
    }

    public SpineAnimationPreviewViewport ScalePan(double scaleX, double scaleY)
    {
        var normalizedScaleX = double.IsFinite(scaleX) && scaleX > 0d
            ? scaleX
            : 1d;
        var normalizedScaleY = double.IsFinite(scaleY) && scaleY > 0d
            ? scaleY
            : 1d;
        return new SpineAnimationPreviewViewport(
                Zoom,
                PanX * normalizedScaleX,
                PanY * normalizedScaleY)
            .Normalize();
    }

    internal static double NormalizeZoom(double zoom) =>
        double.IsFinite(zoom)
            ? Math.Clamp(zoom, MinZoom, MaxZoom)
            : DefaultZoom;

    private static double NormalizePan(double value) =>
        double.IsFinite(value) ? value : 0d;
}

internal sealed class SpineAnimationPreviewViewportState
{
    public SpineAnimationPreviewViewport Current { get; private set; } =
        SpineAnimationPreviewViewport.Default;

    public bool PanBy(double deltaX, double deltaY)
    {
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY))
        {
            return false;
        }

        return SetCurrent(new SpineAnimationPreviewViewport(
            Current.Zoom,
            Current.PanX + deltaX,
            Current.PanY + deltaY));
    }

    public bool ZoomBy(
        double scale,
        double anchorX,
        double anchorY,
        double viewportWidth,
        double viewportHeight)
    {
        if (!double.IsFinite(scale) ||
            scale <= 0d ||
            !double.IsFinite(anchorX) ||
            !double.IsFinite(anchorY))
        {
            return false;
        }

        var previous = Current.Normalize();
        var nextZoom = SpineAnimationPreviewViewport.NormalizeZoom(previous.Zoom * scale);
        if (AreClose(previous.Zoom, nextZoom))
        {
            return false;
        }

        var centerX = ResolveCenter(viewportWidth);
        var centerY = ResolveCenter(viewportHeight);
        var zoomRatio = nextZoom / previous.Zoom;
        var nextPanX = previous.PanX + ((anchorX - centerX - previous.PanX) * (1d - zoomRatio));
        var nextPanY = previous.PanY + ((anchorY - centerY - previous.PanY) * (1d - zoomRatio));

        return SetCurrent(new SpineAnimationPreviewViewport(nextZoom, nextPanX, nextPanY));
    }

    public bool Reset() =>
        SetCurrent(SpineAnimationPreviewViewport.Default);

    public bool Restore(double? zoom, double? panX, double? panY) =>
        SetCurrent(new SpineAnimationPreviewViewport(
            zoom ?? SpineAnimationPreviewViewport.DefaultZoom,
            panX ?? 0d,
            panY ?? 0d));

    private bool SetCurrent(SpineAnimationPreviewViewport viewport)
    {
        var normalized = viewport.Normalize();
        if (AreClose(Current.Zoom, normalized.Zoom) &&
            AreClose(Current.PanX, normalized.PanX) &&
            AreClose(Current.PanY, normalized.PanY))
        {
            return false;
        }

        Current = normalized;
        return true;
    }

    private static double ResolveCenter(double length) =>
        double.IsFinite(length) && length > 0d ? length / 2d : 0d;

    private static bool AreClose(double left, double right) =>
        Math.Abs(left - right) < 0.0000001d;
}
