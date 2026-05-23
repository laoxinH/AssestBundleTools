namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    private bool TryCreateSpriteEditDragViewportMapping(out SpriteEditDragViewportMapping viewportMapping)
    {
        viewportMapping = default;
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

        viewportMapping = new SpriteEditDragViewportMapping(
            FullImageX,
            FullImageY,
            scaleX,
            scaleY,
            CanvasWidth,
            CanvasHeight);
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
        return TryCreateSpriteEditDragViewportMapping(out var viewportMapping) &&
               TryMapViewportPointToSource(
                   viewportX,
                   viewportY,
                   clampToImage,
                   viewportMapping,
                   out sourceX,
                   out sourceY);
    }

    private static bool TryMapViewportPointToSource(
        double viewportX,
        double viewportY,
        bool clampToImage,
        SpriteEditDragViewportMapping viewportMapping,
        out double sourceX,
        out double sourceY)
    {
        sourceX = (viewportX - viewportMapping.FullImageX) / viewportMapping.ScaleX;
        sourceY = (viewportY - viewportMapping.FullImageY) / viewportMapping.ScaleY;
        if (!double.IsFinite(sourceX) || !double.IsFinite(sourceY))
        {
            return false;
        }

        if (!clampToImage)
        {
            return true;
        }

        sourceX = Math.Clamp(sourceX, 0d, viewportMapping.CanvasWidth);
        sourceY = Math.Clamp(sourceY, 0d, viewportMapping.CanvasHeight);
        return true;
    }

    private bool TryCalculateSpriteEditOverlayLayout(
        SpriteEditDragViewportMapping viewportMapping,
        out PreviewImageOverlayPresentationLayout presentationLayout)
    {
        presentationLayout = default;
        if (!_overlayState.HasOverlay ||
            viewportMapping.ScaleX <= 0d ||
            viewportMapping.ScaleY <= 0d)
        {
            return false;
        }

        var overlayX = viewportMapping.FullImageX + (_overlayState.SourceX * viewportMapping.ScaleX);
        var overlayY = viewportMapping.FullImageY + (_overlayState.SourceY * viewportMapping.ScaleY);
        var overlayWidth = _overlayState.SourceWidth * viewportMapping.ScaleX;
        var overlayHeight = _overlayState.SourceHeight * viewportMapping.ScaleY;
        if (!double.IsFinite(overlayX) ||
            !double.IsFinite(overlayY) ||
            !double.IsFinite(overlayWidth) ||
            !double.IsFinite(overlayHeight))
        {
            return false;
        }

        var left = Math.Max(0d, _overlayState.BorderSourceLeft ?? 0d) * viewportMapping.ScaleX;
        var top = Math.Max(0d, _overlayState.BorderSourceTop ?? 0d) * viewportMapping.ScaleY;
        var right = Math.Max(0d, _overlayState.BorderSourceRight ?? 0d) * viewportMapping.ScaleX;
        var bottom = Math.Max(0d, _overlayState.BorderSourceBottom ?? 0d) * viewportMapping.ScaleY;
        presentationLayout = new PreviewImageOverlayPresentationLayout(
            overlayX,
            overlayY,
            overlayWidth,
            overlayHeight,
            _overlayState.HasPivot
                ? viewportMapping.FullImageX + (_overlayState.PivotSourceX!.Value * viewportMapping.ScaleX)
                : 0d,
            _overlayState.HasPivot
                ? viewportMapping.FullImageY + (_overlayState.PivotSourceY!.Value * viewportMapping.ScaleY)
                : 0d,
            overlayX + Math.Min(left, overlayWidth),
            overlayX + Math.Max(0d, overlayWidth - right),
            overlayY + Math.Min(top, overlayHeight),
            overlayY + Math.Max(0d, overlayHeight - bottom),
            overlayY,
            overlayY + overlayHeight,
            overlayHeight,
            overlayX,
            overlayX + overlayWidth,
            overlayWidth);
        return true;
    }
}

internal readonly record struct SpriteEditDragViewportMapping(
    double FullImageX,
    double FullImageY,
    double ScaleX,
    double ScaleY,
    double CanvasWidth,
    double CanvasHeight);
