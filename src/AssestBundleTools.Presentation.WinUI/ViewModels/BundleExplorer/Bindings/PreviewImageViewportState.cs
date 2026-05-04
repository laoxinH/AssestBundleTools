namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal readonly record struct PreviewImageViewportSource(
    double Width,
    double Height);

internal readonly record struct PreviewImageViewportLayout(
    double ImageOffsetX,
    double ImageOffsetY,
    double ImageWidth,
    double ImageHeight,
    double ImageScale);

/// <summary>
/// 管理图片预览视口中的缩放、平移与渲染区域计算。
/// </summary>
internal sealed class PreviewImageViewportState
{
    private const double DefaultZoom = 1d;
    private const double MinZoom = 0.2d;
    private const double MaxZoom = 8d;
    private const double MinimumVisibleImageEdge = 24d;

    private double _viewportWidth;
    private double _viewportHeight;

    public double Zoom { get; private set; } = DefaultZoom;

    public double PanX { get; private set; }

    public double PanY { get; private set; }

    public double ViewportWidth => _viewportWidth;

    public double ViewportHeight => _viewportHeight;

    public void UpdateViewport(double viewportWidth, double viewportHeight)
    {
        _viewportWidth = Math.Max(0d, viewportWidth);
        _viewportHeight = Math.Max(0d, viewportHeight);
    }

    public bool Restore(
        double? zoom,
        double? panX,
        double? panY,
        PreviewImageViewportSource? activeSource)
    {
        var changed = SetZoomCore(zoom ?? DefaultZoom);
        changed = SetPanCore(panX ?? 0d, panY ?? 0d, activeSource) || changed;
        return changed;
    }

    public bool ZoomBy(
        double scale,
        double? anchorX,
        double? anchorY,
        PreviewImageViewportSource? activeSource)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0d)
        {
            return false;
        }

        var previousZoom = Zoom;
        var nextZoom = NormalizeZoom(Zoom * scale);
        if (AreClose(nextZoom, previousZoom))
        {
            return false;
        }

        var nextPanX = PanX;
        var nextPanY = PanY;
        if (activeSource.HasValue &&
            anchorX.HasValue &&
            anchorY.HasValue &&
            TryCalculateLayout(activeSource.Value, previousZoom, PanX, PanY, out var previousLayout) &&
            TryCalculateLayout(activeSource.Value, nextZoom, 0d, 0d, out var nextBaseLayout))
        {
            var sourceX = (anchorX.Value - previousLayout.ImageOffsetX) / previousLayout.ImageScale;
            var sourceY = (anchorY.Value - previousLayout.ImageOffsetY) / previousLayout.ImageScale;
            nextPanX = anchorX.Value - (sourceX * nextBaseLayout.ImageScale) - nextBaseLayout.ImageOffsetX;
            nextPanY = anchorY.Value - (sourceY * nextBaseLayout.ImageScale) - nextBaseLayout.ImageOffsetY;
        }

        var changed = SetZoomCore(nextZoom);
        changed = SetPanCore(nextPanX, nextPanY, activeSource) || changed;
        return changed;
    }

    public bool PanBy(double deltaX, double deltaY, PreviewImageViewportSource? activeSource)
    {
        if (double.IsNaN(deltaX) ||
            double.IsInfinity(deltaX) ||
            double.IsNaN(deltaY) ||
            double.IsInfinity(deltaY))
        {
            return false;
        }

        return SetPanCore(PanX + deltaX, PanY + deltaY, activeSource);
    }

    public bool Reset(PreviewImageViewportSource? activeSource)
    {
        var changed = SetZoomCore(DefaultZoom);
        changed = SetPanCore(0d, 0d, activeSource) || changed;
        return changed;
    }

    public bool ClampPan(PreviewImageViewportSource? activeSource)
    {
        return SetPanCore(PanX, PanY, activeSource);
    }

    public bool TryCalculateLayout(
        double sourceWidth,
        double sourceHeight,
        out PreviewImageViewportLayout layout)
    {
        return TryCalculateLayout(
            new PreviewImageViewportSource(sourceWidth, sourceHeight),
            Zoom,
            PanX,
            PanY,
            out layout);
    }

    private bool SetZoomCore(double zoom)
    {
        var normalizedZoom = NormalizeZoom(zoom);
        if (AreClose(Zoom, normalizedZoom))
        {
            return false;
        }

        Zoom = normalizedZoom;
        return true;
    }

    private bool SetPanCore(
        double panX,
        double panY,
        PreviewImageViewportSource? activeSource)
    {
        var normalizedPan = NormalizePan(panX, panY, activeSource);
        var changed = !AreClose(PanX, normalizedPan.X) || !AreClose(PanY, normalizedPan.Y);
        if (!changed)
        {
            return false;
        }

        PanX = normalizedPan.X;
        PanY = normalizedPan.Y;
        return true;
    }

    private (double X, double Y) NormalizePan(
        double panX,
        double panY,
        PreviewImageViewportSource? activeSource)
    {
        var normalizedPanX = double.IsNaN(panX) || double.IsInfinity(panX) ? 0d : panX;
        var normalizedPanY = double.IsNaN(panY) || double.IsInfinity(panY) ? 0d : panY;
        if (!activeSource.HasValue ||
            !TryCalculateLayout(activeSource.Value, Zoom, normalizedPanX, normalizedPanY, out var layout))
        {
            return (normalizedPanX, normalizedPanY);
        }

        var centeredX = (_viewportWidth - layout.ImageWidth) / 2d;
        var centeredY = (_viewportHeight - layout.ImageHeight) / 2d;
        var clampedOffsetX = ClampOffset(
            layout.ImageOffsetX,
            MinimumVisibleImageEdge - layout.ImageWidth,
            _viewportWidth - MinimumVisibleImageEdge);
        var clampedOffsetY = ClampOffset(
            layout.ImageOffsetY,
            MinimumVisibleImageEdge - layout.ImageHeight,
            _viewportHeight - MinimumVisibleImageEdge);
        return (clampedOffsetX - centeredX, clampedOffsetY - centeredY);
    }

    private bool TryCalculateLayout(
        PreviewImageViewportSource source,
        double zoom,
        double panX,
        double panY,
        out PreviewImageViewportLayout layout)
    {
        layout = default;
        if (_viewportWidth <= 0d || _viewportHeight <= 0d)
        {
            return false;
        }

        var normalizedSourceWidth = Math.Max(1d, source.Width);
        var normalizedSourceHeight = Math.Max(1d, source.Height);
        var baseScale = Math.Min(
            1d,
            Math.Min(
                _viewportWidth / normalizedSourceWidth,
                _viewportHeight / normalizedSourceHeight));
        var scale = baseScale * NormalizeZoom(zoom);
        if (scale <= 0d || double.IsNaN(scale) || double.IsInfinity(scale))
        {
            return false;
        }

        var width = normalizedSourceWidth * scale;
        var height = normalizedSourceHeight * scale;
        layout = new PreviewImageViewportLayout(
            ((_viewportWidth - width) / 2d) + panX,
            ((_viewportHeight - height) / 2d) + panY,
            width,
            height,
            scale);
        return true;
    }

    private static double NormalizeZoom(double zoom)
    {
        return double.IsNaN(zoom) || double.IsInfinity(zoom)
            ? DefaultZoom
            : Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    private static double ClampOffset(double value, double minimum, double maximum)
    {
        return minimum <= maximum
            ? Math.Clamp(value, minimum, maximum)
            : (minimum + maximum) / 2d;
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.0000001d;
    }
}
