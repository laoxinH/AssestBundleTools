using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 保存 Sprite 源图裁切覆盖层的源坐标，并投影到当前图片视口。
/// </summary>
internal sealed class PreviewImageOverlayState
{
    private double _sourceX;
    private double _sourceY;
    private double _sourceWidth;
    private double _sourceHeight;
    private double _originalSourceX;
    private double _originalSourceY;
    private double _originalSourceWidth;
    private double _originalSourceHeight;
    private double? _originalPivotSourceX;
    private double? _originalPivotSourceY;
    private double? _originalBorderSourceLeft;
    private double? _originalBorderSourceTop;
    private double? _originalBorderSourceRight;
    private double? _originalBorderSourceBottom;
    private bool _hasOriginalOverlay;

    public string LabelText { get; private set; } = string.Empty;

    public double SourceX => _sourceX;

    public double SourceY => _sourceY;

    public double SourceWidth => _sourceWidth;

    public double SourceHeight => _sourceHeight;

    public double? PivotSourceX { get; private set; }

    public double? PivotSourceY { get; private set; }

    public double? BorderSourceLeft { get; private set; }

    public double? BorderSourceTop { get; private set; }

    public double? BorderSourceRight { get; private set; }

    public double? BorderSourceBottom { get; private set; }

    public bool HasOverlay =>
        _sourceWidth > 0d &&
        _sourceHeight > 0d &&
        !string.IsNullOrWhiteSpace(LabelText);

    public bool HasPivot => PivotSourceX.HasValue && PivotSourceY.HasValue;

    public bool HasBorder =>
        BorderSourceLeft is > 0d ||
        BorderSourceTop is > 0d ||
        BorderSourceRight is > 0d ||
        BorderSourceBottom is > 0d;

    public void Clear()
    {
        _sourceX = 0d;
        _sourceY = 0d;
        _sourceWidth = 0d;
        _sourceHeight = 0d;
        PivotSourceX = null;
        PivotSourceY = null;
        BorderSourceLeft = null;
        BorderSourceTop = null;
        BorderSourceRight = null;
        BorderSourceBottom = null;
        _originalSourceX = 0d;
        _originalSourceY = 0d;
        _originalSourceWidth = 0d;
        _originalSourceHeight = 0d;
        _originalPivotSourceX = null;
        _originalPivotSourceY = null;
        _originalBorderSourceLeft = null;
        _originalBorderSourceTop = null;
        _originalBorderSourceRight = null;
        _originalBorderSourceBottom = null;
        _hasOriginalOverlay = false;
        LabelText = string.Empty;
    }

    public void Apply(SpritePreviewOverlayDisplayState? overlayDisplayState)
    {
        _sourceX = overlayDisplayState is null ? 0d : Math.Max(0d, overlayDisplayState.RegionX);
        _sourceY = overlayDisplayState is null ? 0d : Math.Max(0d, overlayDisplayState.RegionY);
        _sourceWidth = overlayDisplayState is null ? 0d : Math.Max(0d, overlayDisplayState.RegionWidth);
        _sourceHeight = overlayDisplayState is null ? 0d : Math.Max(0d, overlayDisplayState.RegionHeight);
        PivotSourceX = overlayDisplayState?.PivotX;
        PivotSourceY = overlayDisplayState?.PivotY;
        BorderSourceLeft = overlayDisplayState?.BorderLeft;
        BorderSourceTop = overlayDisplayState?.BorderTop;
        BorderSourceRight = overlayDisplayState?.BorderRight;
        BorderSourceBottom = overlayDisplayState?.BorderBottom;
        LabelText = string.IsNullOrWhiteSpace(overlayDisplayState?.BadgeText)
            ? string.Empty
            : overlayDisplayState.BadgeText.Trim();
        CaptureOriginalGeometry();
    }

    public void UpdateEditableGeometry(
        double canvasWidth,
        double canvasHeight,
        double regionX,
        double regionY,
        double regionWidth,
        double regionHeight,
        double? pivotX,
        double? pivotY)
    {
        _sourceWidth = Math.Max(1d, NormalizeFinite(regionWidth, 1d));
        _sourceHeight = Math.Max(1d, NormalizeFinite(regionHeight, 1d));
        _sourceX = Math.Max(0d, NormalizeFinite(regionX, 0d));
        _sourceY = Math.Max(0d, NormalizeFinite(regionY, 0d));

        if (pivotX.HasValue && pivotY.HasValue)
        {
            PivotSourceX = Math.Clamp(
                NormalizeFinite(pivotX.Value, _sourceX + (_sourceWidth / 2d)),
                _sourceX,
                _sourceX + _sourceWidth);
            PivotSourceY = Math.Clamp(
                NormalizeFinite(pivotY.Value, _sourceY + (_sourceHeight / 2d)),
                _sourceY,
                _sourceY + _sourceHeight);
        }
    }

    public bool TryResetEditableGeometryToOriginal()
    {
        if (!_hasOriginalOverlay)
        {
            return false;
        }

        _sourceX = _originalSourceX;
        _sourceY = _originalSourceY;
        _sourceWidth = _originalSourceWidth;
        _sourceHeight = _originalSourceHeight;
        PivotSourceX = _originalPivotSourceX;
        PivotSourceY = _originalPivotSourceY;
        BorderSourceLeft = _originalBorderSourceLeft;
        BorderSourceTop = _originalBorderSourceTop;
        BorderSourceRight = _originalBorderSourceRight;
        BorderSourceBottom = _originalBorderSourceBottom;
        return true;
    }

    public bool TryCalculateLayout(
        double sourceCanvasWidth,
        double sourceCanvasHeight,
        PreviewImageViewportState viewportState,
        out PreviewImageOverlayPresentationLayout presentationLayout)
    {
        ArgumentNullException.ThrowIfNull(viewportState);

        if (!HasOverlay)
        {
            presentationLayout = default;
            return false;
        }

        if (!PreviewImageOverlayLayoutCalculator.TryCalculate(
            sourceCanvasWidth,
            sourceCanvasHeight,
            _sourceX,
            _sourceY,
            _sourceWidth,
            _sourceHeight,
            viewportState.ViewportWidth,
            viewportState.ViewportHeight,
            viewportState.Zoom,
            viewportState.PanX,
            viewportState.PanY,
            out var layout))
        {
            presentationLayout = default;
            return false;
        }

        presentationLayout = CreatePresentationLayout(layout);
        return true;
    }

    public bool TryCalculateLayoutFromImageLayout(
        PreviewImageViewportLayout imageLayout,
        out PreviewImageOverlayPresentationLayout presentationLayout)
    {
        presentationLayout = default;
        if (!HasOverlay || imageLayout.ImageScale <= 0d)
        {
            return false;
        }

        var layout = new PreviewImageOverlayLayout(
            imageLayout.ImageOffsetX + (_sourceX * imageLayout.ImageScale),
            imageLayout.ImageOffsetY + (_sourceY * imageLayout.ImageScale),
            _sourceWidth * imageLayout.ImageScale,
            _sourceHeight * imageLayout.ImageScale,
            imageLayout.ImageScale,
            imageLayout.ImageOffsetX,
            imageLayout.ImageOffsetY);
        presentationLayout = CreatePresentationLayout(layout);
        return true;
    }

    private PreviewImageOverlayPresentationLayout CreatePresentationLayout(PreviewImageOverlayLayout layout)
    {
        var scale = layout.ImageScale;
        var left = Math.Max(0d, BorderSourceLeft ?? 0d) * scale;
        var top = Math.Max(0d, BorderSourceTop ?? 0d) * scale;
        var right = Math.Max(0d, BorderSourceRight ?? 0d) * scale;
        var bottom = Math.Max(0d, BorderSourceBottom ?? 0d) * scale;
        return new PreviewImageOverlayPresentationLayout(
            layout.OverlayX,
            layout.OverlayY,
            layout.OverlayWidth,
            layout.OverlayHeight,
            HasPivot ? layout.ImageOffsetX + (PivotSourceX!.Value * scale) : 0d,
            HasPivot ? layout.ImageOffsetY + (PivotSourceY!.Value * scale) : 0d,
            layout.OverlayX + Math.Min(left, layout.OverlayWidth),
            layout.OverlayX + Math.Max(0d, layout.OverlayWidth - right),
            layout.OverlayY + Math.Min(top, layout.OverlayHeight),
            layout.OverlayY + Math.Max(0d, layout.OverlayHeight - bottom),
            layout.OverlayY,
            layout.OverlayY + layout.OverlayHeight,
            layout.OverlayHeight,
            layout.OverlayX,
            layout.OverlayX + layout.OverlayWidth,
            layout.OverlayWidth);
    }

    private void CaptureOriginalGeometry()
    {
        _originalSourceX = _sourceX;
        _originalSourceY = _sourceY;
        _originalSourceWidth = _sourceWidth;
        _originalSourceHeight = _sourceHeight;
        _originalPivotSourceX = PivotSourceX;
        _originalPivotSourceY = PivotSourceY;
        _originalBorderSourceLeft = BorderSourceLeft;
        _originalBorderSourceTop = BorderSourceTop;
        _originalBorderSourceRight = BorderSourceRight;
        _originalBorderSourceBottom = BorderSourceBottom;
        _hasOriginalOverlay = HasOverlay;
    }

    private static double NormalizeFinite(double value, double fallback)
    {
        return double.IsFinite(value) ? value : fallback;
    }
}

internal readonly record struct PreviewImageOverlayPresentationLayout(
    double OverlayX,
    double OverlayY,
    double OverlayWidth,
    double OverlayHeight,
    double PivotX,
    double PivotY,
    double BorderLeftX,
    double BorderRightX,
    double BorderTopY,
    double BorderBottomY,
    double BorderVerticalTop,
    double BorderVerticalBottom,
    double BorderVerticalHeight,
    double BorderHorizontalLeft,
    double BorderHorizontalRight,
    double BorderHorizontalWidth);
