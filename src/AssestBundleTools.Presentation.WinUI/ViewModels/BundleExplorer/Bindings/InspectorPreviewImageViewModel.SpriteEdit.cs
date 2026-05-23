using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    private const double SpriteEditHandleHitRadius = 12d;
    private const double SpriteEditMinimumRegionSize = 2d;
    private const double SpriteEditViewportPadding = 24d;

    private bool _isSpriteEditModeRequested;
    private bool _isSpriteEditModeActive;
    private SpriteEditDragState? _spriteEditDragState;
    private SpriteEditDeferredViewport? _deferredSpriteEditViewport;

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

    public bool IsSpriteEditDragActive => _spriteEditDragState is not null;

    internal void RequestSpriteEditMode()
    {
        _isSpriteEditModeRequested = true;
        TryActivateRequestedSpriteEditMode();
    }

    internal void CancelSpriteEditMode()
    {
        _isSpriteEditModeRequested = false;
        _spriteEditDragState = null;
        OnPropertyChanged(nameof(IsSpriteEditDragActive));
        IsSpriteEditModeActive = false;
        if (_deferredSpriteEditViewport is not null)
        {
            ApplyDeferredSpriteEditViewportUpdate();
            return;
        }

        RecalculateImageLayout();
        RecalculateOverlays();
    }

    internal bool TryBeginSpriteEditDrag(double viewportX, double viewportY)
    {
        if (!IsSpriteEditModeActive ||
            !TryCreateSpriteEditDragViewportMapping(out var viewportMapping) ||
            !TryMapViewportPointToSource(
                viewportX,
                viewportY,
                clampToImage: false,
                viewportMapping,
                out var sourceX,
                out var sourceY))
        {
            return false;
        }

        var dragKind = ResolveSpriteEditDragKind(viewportX, viewportY);
        if (dragKind == SpriteEditDragKind.None)
        {
            return false;
        }

        RefreshSpriteEditPivotRelativePositionFromOverlay();
        _spriteEditDragState = new SpriteEditDragState(
            dragKind,
            sourceX,
            sourceY,
            _overlayState.SourceX,
            _overlayState.SourceY,
            _overlayState.SourceWidth,
            _overlayState.SourceHeight,
            viewportMapping);
        OnPropertyChanged(nameof(IsSpriteEditDragActive));
        return true;
    }

    internal void UpdateSpriteEditDrag(double viewportX, double viewportY)
    {
        if (_spriteEditDragState is not { } dragState ||
            !TryMapViewportPointToSource(
                viewportX,
                viewportY,
                clampToImage: false,
                dragState.ViewportMapping,
                out var sourceX,
                out var sourceY))
        {
            return;
        }

        var deltaX = sourceX - dragState.StartPointerSourceX;
        var deltaY = sourceY - dragState.StartPointerSourceY;
        var nextX = dragState.StartRegionX;
        var nextY = dragState.StartRegionY;
        var nextWidth = dragState.StartRegionWidth;
        var nextHeight = dragState.StartRegionHeight;
        double nextPivotX;
        double nextPivotY;
        var shouldRefreshPivotRelativePosition = dragState.Kind == SpriteEditDragKind.Pivot;

        if (dragState.Kind == SpriteEditDragKind.Pivot)
        {
            nextPivotX = sourceX;
            nextPivotY = sourceY;
        }
        else if (dragState.Kind == SpriteEditDragKind.Move)
        {
            nextX = ClampRegionMinimum(dragState.StartRegionX + deltaX);
            nextY = ClampRegionMinimum(dragState.StartRegionY + deltaY);
            (nextPivotX, nextPivotY) = ResolveSpriteEditPivotFromRelative(
                nextX,
                nextY,
                nextWidth,
                nextHeight);
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
            (nextPivotX, nextPivotY) = ResolveSpriteEditPivotFromRelative(
                nextX,
                nextY,
                nextWidth,
                nextHeight);
        }

        nextPivotX = Math.Clamp(nextPivotX, nextX, nextX + nextWidth);
        nextPivotY = Math.Clamp(nextPivotY, nextY, nextY + nextHeight);
        ApplySpriteEditGeometry(
            nextX,
            nextY,
            nextWidth,
            nextHeight,
            nextPivotX,
            nextPivotY,
            shouldRefreshPivotRelativePosition);
    }

    internal void CompleteSpriteEditDrag()
    {
        _spriteEditDragState = null;
        OnPropertyChanged(nameof(IsSpriteEditDragActive));
        ApplyDeferredSpriteEditViewportUpdate();
    }

    private bool TryDeferSpriteEditViewportUpdate(double viewportWidth, double viewportHeight)
    {
        if (!IsSpriteEditModeActive || _spriteEditDragState is null)
        {
            return false;
        }

        _deferredSpriteEditViewport = new SpriteEditDeferredViewport(viewportWidth, viewportHeight);
        return true;
    }

    private void ApplyDeferredSpriteEditViewportUpdate()
    {
        if (_deferredSpriteEditViewport is not { } deferredViewport)
        {
            return;
        }

        _deferredSpriteEditViewport = null;
        UpdateViewport(deferredViewport.Width, deferredViewport.Height);
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
        InitializeSpriteEditPivotRelativePosition();
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
        var horizontalPadding = Math.Min(SpriteEditViewportPadding, _viewportState.ViewportWidth / 4d);
        var verticalPadding = Math.Min(SpriteEditViewportPadding, _viewportState.ViewportHeight / 4d);
        var availableWidth = Math.Max(1d, _viewportState.ViewportWidth - (horizontalPadding * 2d));
        var availableHeight = Math.Max(1d, _viewportState.ViewportHeight - (verticalPadding * 2d));
        var scale = Math.Min(
            availableWidth / normalizedSourceWidth,
            availableHeight / normalizedSourceHeight);
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
            right = Math.Max(
                left + SpriteEditMinimumRegionSize,
                dragState.StartRegionX + dragState.StartRegionWidth + deltaX);
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
            bottom = Math.Max(
                top + SpriteEditMinimumRegionSize,
                dragState.StartRegionY + dragState.StartRegionHeight + deltaY);
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
        OnPropertyChanged(nameof(OverlayFrameVisibility));
        OnPropertyChanged(nameof(SpriteEditFrameVisibility));
        OnPropertyChanged(nameof(SpriteEditHandleVisibility));
        OnPropertyChanged(nameof(SpriteEditGeometryPanelVisibility));
        OnPropertyChanged(nameof(SpriteEditMeasurementVisibility));
        OnPropertyChanged(nameof(OverlayBadgeVisibility));
        NotifySpriteEditMeasurementChanged();
        NotifyInfoBadgeVisibilityChanged();
        NotifyRegionOverlayVisibilityChanged();
    }

    private static double ClampRegionOrigin(double value, double size, double canvasSize)
    {
        return Math.Clamp(value, 0d, Math.Max(0d, canvasSize - size));
    }

    private static double ClampRegionMinimum(double value)
    {
        return Math.Max(0d, value);
    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        var deltaX = x1 - x2;
        var deltaY = y1 - y2;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
