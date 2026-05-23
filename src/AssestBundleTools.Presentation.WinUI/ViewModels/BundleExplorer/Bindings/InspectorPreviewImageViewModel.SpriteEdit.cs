using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    private const double SpriteEditHandleHitRadius = 12d;
    private const double SpriteEditMinimumRegionSize = 2d;

    private bool _isSpriteEditModeRequested;
    private bool _isSpriteEditModeActive;
    private SpriteEditDragState? _spriteEditDragState;

    public bool IsSpriteEditModeActive
    {
        get => _isSpriteEditModeActive;
        private set
        {
            if (SetProperty(ref _isSpriteEditModeActive, value))
            {
                NotifySpriteEditStateChanged();
            }
        }
    }

    public Visibility ImageViewportControlsVisibility =>
        Visibility == Visibility.Visible && !IsSpriteEditModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SpriteDisplayModeControlVisibility =>
        DisplayOptions.SpriteModeSwitchVisibility == Visibility.Visible && !IsSpriteEditModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SpriteEditHandleVisibility =>
        IsSpriteEditModeActive && OverlayVisibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility OverlayBadgeVisibility =>
        OverlayVisibility == Visibility.Visible && !IsSpriteEditModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;

    public double OverlayRightX => OverlayX + OverlayWidth;

    public double OverlayBottomY => OverlayY + OverlayHeight;

    internal void RequestSpriteEditMode()
    {
        _isSpriteEditModeRequested = true;
        TryActivateRequestedSpriteEditMode();
    }

    internal void CancelSpriteEditMode()
    {
        _isSpriteEditModeRequested = false;
        _spriteEditDragState = null;
        IsSpriteEditModeActive = false;
        RecalculateImageLayout();
        RecalculateOverlays();
    }

    internal bool TryBeginSpriteEditDrag(double viewportX, double viewportY)
    {
        if (!IsSpriteEditModeActive ||
            !TryMapViewportPointToSource(viewportX, viewportY, clampToImage: true, out var sourceX, out var sourceY))
        {
            return false;
        }

        var dragKind = ResolveSpriteEditDragKind(viewportX, viewportY);
        if (dragKind == SpriteEditDragKind.None)
        {
            return false;
        }

        _spriteEditDragState = new SpriteEditDragState(
            dragKind,
            sourceX,
            sourceY,
            _overlayState.SourceX,
            _overlayState.SourceY,
            _overlayState.SourceWidth,
            _overlayState.SourceHeight,
            ResolvePivotSourceX(),
            ResolvePivotSourceY());
        return true;
    }

    internal void UpdateSpriteEditDrag(double viewportX, double viewportY)
    {
        if (_spriteEditDragState is not { } dragState ||
            !TryMapViewportPointToSource(viewportX, viewportY, clampToImage: true, out var sourceX, out var sourceY))
        {
            return;
        }

        var deltaX = sourceX - dragState.StartPointerSourceX;
        var deltaY = sourceY - dragState.StartPointerSourceY;
        var nextX = dragState.StartRegionX;
        var nextY = dragState.StartRegionY;
        var nextWidth = dragState.StartRegionWidth;
        var nextHeight = dragState.StartRegionHeight;
        var nextPivotX = dragState.StartPivotX;
        var nextPivotY = dragState.StartPivotY;

        if (dragState.Kind == SpriteEditDragKind.Pivot)
        {
            nextPivotX = sourceX;
            nextPivotY = sourceY;
        }
        else if (dragState.Kind == SpriteEditDragKind.Move)
        {
            nextX = ClampRegionOrigin(dragState.StartRegionX + deltaX, dragState.StartRegionWidth, CanvasWidth);
            nextY = ClampRegionOrigin(dragState.StartRegionY + deltaY, dragState.StartRegionHeight, CanvasHeight);
            nextPivotX = dragState.StartPivotX + (nextX - dragState.StartRegionX);
            nextPivotY = dragState.StartPivotY + (nextY - dragState.StartRegionY);
        }
        else
        {
            ResizeRegion(
                dragState,
                deltaX,
                deltaY,
                out nextX,
                out nextY,
                out nextWidth,
                out nextHeight);
        }

        nextPivotX = Math.Clamp(nextPivotX, nextX, nextX + nextWidth);
        nextPivotY = Math.Clamp(nextPivotY, nextY, nextY + nextHeight);
        ApplySpriteEditGeometry(
            nextX,
            nextY,
            nextWidth,
            nextHeight,
            nextPivotX,
            nextPivotY);
    }

    internal void CompleteSpriteEditDrag()
    {
        _spriteEditDragState = null;
    }

    private void TryActivateRequestedSpriteEditMode()
    {
        if (!_isSpriteEditModeRequested ||
            Visibility != Visibility.Visible ||
            !_overlayState.HasOverlay)
        {
            return;
        }

        DisplayOptions.UseSpriteCropPreview();
        _viewportState.Reset(ResolveActiveImageViewportSource());
        IsSpriteEditModeActive = true;
        RecalculateImageLayout();
        RecalculateOverlays();
    }

    private bool TryCalculateSpriteEditImageLayout(double sourceWidth, double sourceHeight, out PreviewImageViewportLayout layout)
    {
        layout = default;
        if (_viewportState.ViewportWidth <= 0d || _viewportState.ViewportHeight <= 0d)
        {
            return false;
        }

        var normalizedSourceWidth = Math.Max(1d, sourceWidth);
        var normalizedSourceHeight = Math.Max(1d, sourceHeight);
        var scale = Math.Min(
            _viewportState.ViewportWidth / normalizedSourceWidth,
            _viewportState.ViewportHeight / normalizedSourceHeight);
        if (!double.IsFinite(scale) || scale <= 0d)
        {
            return false;
        }

        var width = normalizedSourceWidth * scale;
        var height = normalizedSourceHeight * scale;
        layout = new PreviewImageViewportLayout(
            (_viewportState.ViewportWidth - width) / 2d,
            (_viewportState.ViewportHeight - height) / 2d,
            width,
            height,
            scale);
        return true;
    }

    private bool TryMapViewportPointToSource(
        double viewportX,
        double viewportY,
        bool clampToImage,
        out double sourceX,
        out double sourceY)
    {
        sourceX = 0d;
        sourceY = 0d;
        if (FullImageWidth <= 0d ||
            FullImageHeight <= 0d ||
            CanvasWidth <= 0d ||
            CanvasHeight <= 0d)
        {
            return false;
        }

        var scaleX = FullImageWidth / CanvasWidth;
        var scaleY = FullImageHeight / CanvasHeight;
        if (!double.IsFinite(scaleX) ||
            !double.IsFinite(scaleY) ||
            scaleX <= 0d ||
            scaleY <= 0d)
        {
            return false;
        }

        sourceX = (viewportX - FullImageX) / scaleX;
        sourceY = (viewportY - FullImageY) / scaleY;
        if (!clampToImage)
        {
            return sourceX >= 0d && sourceX <= CanvasWidth && sourceY >= 0d && sourceY <= CanvasHeight;
        }

        sourceX = Math.Clamp(sourceX, 0d, CanvasWidth);
        sourceY = Math.Clamp(sourceY, 0d, CanvasHeight);
        return true;
    }

    private SpriteEditDragKind ResolveSpriteEditDragKind(double viewportX, double viewportY)
    {
        if (PivotVisibility == Visibility.Visible &&
            Distance(viewportX, viewportY, PivotX, PivotY) <= SpriteEditHandleHitRadius)
        {
            return SpriteEditDragKind.Pivot;
        }

        var nearLeft = Math.Abs(viewportX - OverlayX) <= SpriteEditHandleHitRadius;
        var nearRight = Math.Abs(viewportX - OverlayRightX) <= SpriteEditHandleHitRadius;
        var nearTop = Math.Abs(viewportY - OverlayY) <= SpriteEditHandleHitRadius;
        var nearBottom = Math.Abs(viewportY - OverlayBottomY) <= SpriteEditHandleHitRadius;
        var withinHorizontalBand = viewportX >= OverlayX - SpriteEditHandleHitRadius &&
                                   viewportX <= OverlayRightX + SpriteEditHandleHitRadius;
        var withinVerticalBand = viewportY >= OverlayY - SpriteEditHandleHitRadius &&
                                 viewportY <= OverlayBottomY + SpriteEditHandleHitRadius;

        if (nearLeft && nearTop)
        {
            return SpriteEditDragKind.ResizeTopLeft;
        }

        if (nearRight && nearTop)
        {
            return SpriteEditDragKind.ResizeTopRight;
        }

        if (nearLeft && nearBottom)
        {
            return SpriteEditDragKind.ResizeBottomLeft;
        }

        if (nearRight && nearBottom)
        {
            return SpriteEditDragKind.ResizeBottomRight;
        }

        if (nearLeft && withinVerticalBand)
        {
            return SpriteEditDragKind.ResizeLeft;
        }

        if (nearRight && withinVerticalBand)
        {
            return SpriteEditDragKind.ResizeRight;
        }

        if (nearTop && withinHorizontalBand)
        {
            return SpriteEditDragKind.ResizeTop;
        }

        if (nearBottom && withinHorizontalBand)
        {
            return SpriteEditDragKind.ResizeBottom;
        }

        return viewportX >= OverlayX &&
               viewportX <= OverlayRightX &&
               viewportY >= OverlayY &&
               viewportY <= OverlayBottomY
            ? SpriteEditDragKind.Move
            : SpriteEditDragKind.None;
    }

    private void ResizeRegion(
        SpriteEditDragState dragState,
        double deltaX,
        double deltaY,
        out double nextX,
        out double nextY,
        out double nextWidth,
        out double nextHeight)
    {
        var left = dragState.StartRegionX;
        var top = dragState.StartRegionY;
        var right = dragState.StartRegionX + dragState.StartRegionWidth;
        var bottom = dragState.StartRegionY + dragState.StartRegionHeight;

        if (dragState.Kind is
            SpriteEditDragKind.ResizeLeft or
            SpriteEditDragKind.ResizeTopLeft or
            SpriteEditDragKind.ResizeBottomLeft)
        {
            left = Math.Clamp(
                dragState.StartRegionX + deltaX,
                0d,
                right - SpriteEditMinimumRegionSize);
        }

        if (dragState.Kind is
            SpriteEditDragKind.ResizeRight or
            SpriteEditDragKind.ResizeTopRight or
            SpriteEditDragKind.ResizeBottomRight)
        {
            right = Math.Clamp(
                dragState.StartRegionX + dragState.StartRegionWidth + deltaX,
                left + SpriteEditMinimumRegionSize,
                CanvasWidth);
        }

        if (dragState.Kind is
            SpriteEditDragKind.ResizeTop or
            SpriteEditDragKind.ResizeTopLeft or
            SpriteEditDragKind.ResizeTopRight)
        {
            top = Math.Clamp(
                dragState.StartRegionY + deltaY,
                0d,
                bottom - SpriteEditMinimumRegionSize);
        }

        if (dragState.Kind is
            SpriteEditDragKind.ResizeBottom or
            SpriteEditDragKind.ResizeBottomLeft or
            SpriteEditDragKind.ResizeBottomRight)
        {
            bottom = Math.Clamp(
                dragState.StartRegionY + dragState.StartRegionHeight + deltaY,
                top + SpriteEditMinimumRegionSize,
                CanvasHeight);
        }

        nextX = left;
        nextY = top;
        nextWidth = right - left;
        nextHeight = bottom - top;
    }

    private double ResolvePivotSourceX()
    {
        return _overlayState.PivotSourceX ??
               _overlayState.SourceX + (_overlayState.SourceWidth / 2d);
    }

    private double ResolvePivotSourceY()
    {
        return _overlayState.PivotSourceY ??
               _overlayState.SourceY + (_overlayState.SourceHeight / 2d);
    }

    private void NotifySpriteEditStateChanged()
    {
        OnPropertyChanged(nameof(ImageViewportControlsVisibility));
        OnPropertyChanged(nameof(SpriteDisplayModeControlVisibility));
        OnPropertyChanged(nameof(SpriteEditHandleVisibility));
        OnPropertyChanged(nameof(SpriteEditGeometryPanelVisibility));
        OnPropertyChanged(nameof(OverlayBadgeVisibility));
        NotifyInfoBadgeVisibilityChanged();
        NotifyRegionOverlayVisibilityChanged();
    }

    private static double ClampRegionOrigin(double value, double size, double canvasSize)
    {
        return Math.Clamp(value, 0d, Math.Max(0d, canvasSize - size));
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var deltaX = x1 - x2;
        var deltaY = y1 - y2;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}

internal enum SpriteEditDragKind
{
    None,
    Move,
    Pivot,
    ResizeLeft,
    ResizeRight,
    ResizeTop,
    ResizeBottom,
    ResizeTopLeft,
    ResizeTopRight,
    ResizeBottomLeft,
    ResizeBottomRight
}

internal readonly record struct SpriteEditDragState(
    SpriteEditDragKind Kind,
    double StartPointerSourceX,
    double StartPointerSourceY,
    double StartRegionX,
    double StartRegionY,
    double StartRegionWidth,
    double StartRegionHeight,
    double StartPivotX,
    double StartPivotY);
