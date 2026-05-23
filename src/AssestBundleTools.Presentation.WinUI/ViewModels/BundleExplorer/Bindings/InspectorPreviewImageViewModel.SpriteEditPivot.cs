namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    private double _spriteEditPivotRelativeX = 0.5d;
    private double _spriteEditPivotRelativeY = 0.5d;

    private void ResetSpriteEditPivotRelativePosition()
    {
        _spriteEditPivotRelativeX = 0.5d;
        _spriteEditPivotRelativeY = 0.5d;
    }

    private void InitializeSpriteEditPivotRelativePosition()
    {
        RefreshSpriteEditPivotRelativePosition(
            _overlayState.SourceX,
            _overlayState.SourceY,
            _overlayState.SourceWidth,
            _overlayState.SourceHeight,
            ResolvePivotSourceX(),
            ResolvePivotSourceY());
    }

    private void RefreshSpriteEditPivotRelativePositionFromOverlay()
    {
        RefreshSpriteEditPivotRelativePosition(
            _overlayState.SourceX,
            _overlayState.SourceY,
            _overlayState.SourceWidth,
            _overlayState.SourceHeight,
            ResolvePivotSourceX(),
            ResolvePivotSourceY());
    }

    private void RefreshSpriteEditPivotRelativePosition(
        double cropX,
        double cropY,
        double cropWidth,
        double cropHeight,
        double pivotX,
        double pivotY)
    {
        var normalizedWidth = Math.Max(SpriteEditMinimumRegionSize, NormalizeFinite(cropWidth, SpriteEditMinimumRegionSize));
        var normalizedHeight = Math.Max(SpriteEditMinimumRegionSize, NormalizeFinite(cropHeight, SpriteEditMinimumRegionSize));
        _spriteEditPivotRelativeX = Math.Clamp(
            (NormalizeFinite(pivotX, cropX) - NormalizeFinite(cropX, 0d)) / normalizedWidth,
            0d,
            1d);
        _spriteEditPivotRelativeY = Math.Clamp(
            (NormalizeFinite(pivotY, cropY) - NormalizeFinite(cropY, 0d)) / normalizedHeight,
            0d,
            1d);
    }

    private (double X, double Y) ResolveSpriteEditPivotFromRelative(
        double cropX,
        double cropY,
        double cropWidth,
        double cropHeight)
    {
        var normalizedWidth = Math.Max(SpriteEditMinimumRegionSize, NormalizeFinite(cropWidth, SpriteEditMinimumRegionSize));
        var normalizedHeight = Math.Max(SpriteEditMinimumRegionSize, NormalizeFinite(cropHeight, SpriteEditMinimumRegionSize));
        var pivotX = NormalizeFinite(cropX, 0d) + (normalizedWidth * _spriteEditPivotRelativeX);
        var pivotY = NormalizeFinite(cropY, 0d) + (normalizedHeight * _spriteEditPivotRelativeY);
        return (
            Math.Clamp(pivotX, cropX, cropX + cropWidth),
            Math.Clamp(pivotY, cropY, cropY + cropHeight));
    }
}
