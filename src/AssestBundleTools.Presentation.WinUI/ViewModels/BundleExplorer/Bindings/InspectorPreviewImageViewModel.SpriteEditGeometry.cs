using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    public Visibility SpriteEditGeometryPanelVisibility =>
        IsSpriteEditModeActive && OverlayVisibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

    public double SpriteEditCropX => _overlayState.SourceX;

    public double SpriteEditCropY => _overlayState.SourceY;

    public double SpriteEditCropWidth => _overlayState.SourceWidth;

    public double SpriteEditCropHeight => _overlayState.SourceHeight;

    public double SpriteEditPivotX => ResolvePivotSourceX();

    public double SpriteEditPivotY => ResolvePivotSourceY();

    internal void UpdateSpriteEditGeometryFromInput(
        double cropX,
        double cropY,
        double cropWidth,
        double cropHeight,
        double pivotX,
        double pivotY)
    {
        if (!IsSpriteEditModeActive || !_overlayState.HasOverlay)
        {
            return;
        }

        var nextWidth = ClampRegionSize(
            NormalizeFinite(cropWidth, _overlayState.SourceWidth),
            CanvasWidth);
        var nextHeight = ClampRegionSize(
            NormalizeFinite(cropHeight, _overlayState.SourceHeight),
            CanvasHeight);
        var nextX = ClampRegionMinimum(NormalizeFinite(cropX, _overlayState.SourceX));
        var nextY = ClampRegionMinimum(NormalizeFinite(cropY, _overlayState.SourceY));
        var nextPivotX = NormalizeFinite(pivotX, ResolvePivotSourceX());
        var nextPivotY = NormalizeFinite(pivotY, ResolvePivotSourceY());

        ApplySpriteEditGeometry(
            nextX,
            nextY,
            nextWidth,
            nextHeight,
            nextPivotX,
            nextPivotY);
    }

    private void ApplySpriteEditGeometry(
        double cropX,
        double cropY,
        double cropWidth,
        double cropHeight,
        double pivotX,
        double pivotY)
    {
        _overlayState.UpdateEditableGeometry(
            CanvasWidth,
            CanvasHeight,
            cropX,
            cropY,
            cropWidth,
            cropHeight,
            pivotX,
            pivotY);
        RecalculateOverlayLayout();
    }

    private void NotifySpriteEditGeometryChanged()
    {
        OnPropertyChanged(nameof(SpriteEditCropX));
        OnPropertyChanged(nameof(SpriteEditCropY));
        OnPropertyChanged(nameof(SpriteEditCropWidth));
        OnPropertyChanged(nameof(SpriteEditCropHeight));
        OnPropertyChanged(nameof(SpriteEditPivotX));
        OnPropertyChanged(nameof(SpriteEditPivotY));
        OnPropertyChanged(nameof(SpriteEditGeometryPanelVisibility));
    }

    private static double ClampRegionSize(double value, double canvasSize)
    {
        var normalizedCanvasSize = Math.Max(1d, canvasSize);
        return Math.Max(
            Math.Min(SpriteEditMinimumRegionSize, normalizedCanvasSize),
            value);
    }

    private static double NormalizeFinite(double value, double fallback)
    {
        return double.IsFinite(value) ? value : fallback;
    }
}
