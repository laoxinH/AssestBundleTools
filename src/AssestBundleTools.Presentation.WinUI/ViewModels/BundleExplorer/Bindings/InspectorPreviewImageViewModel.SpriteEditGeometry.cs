using AssestBundleTools.Application.Workspace.Dtos;
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

    internal WorkspaceSpriteEditGeometryDto? CreateSpriteEditGeometry()
    {
        if (!IsSpriteEditModeActive || !_overlayState.HasOverlay)
        {
            return null;
        }

        var geometry = new WorkspaceSpriteEditGeometryDto(
            NormalizePositive(CanvasWidth, fallback: 1d),
            NormalizePositive(CanvasHeight, fallback: 1d),
            Math.Max(0d, NormalizeFinite(SpriteEditCropX, 0d)),
            Math.Max(0d, NormalizeFinite(SpriteEditCropY, 0d)),
            NormalizePositive(SpriteEditCropWidth, SpriteEditMinimumRegionSize),
            NormalizePositive(SpriteEditCropHeight, SpriteEditMinimumRegionSize),
            NormalizeFinite(SpriteEditPivotX, SpriteEditCropX + (SpriteEditCropWidth / 2d)),
            NormalizeFinite(SpriteEditPivotY, SpriteEditCropY + (SpriteEditCropHeight / 2d)));

        return geometry with
        {
            PivotX = Math.Clamp(geometry.PivotX, geometry.RegionX, geometry.RegionX + geometry.RegionWidth),
            PivotY = Math.Clamp(geometry.PivotY, geometry.RegionY, geometry.RegionY + geometry.RegionHeight)
        };
    }

    internal void UpdateSpriteEditGeometryFromInput(
        double cropX,
        double cropY,
        double cropWidth,
        double cropHeight,
        double pivotX,
        double pivotY,
        SpriteEditGeometryInputKind inputKind)
    {
        if (!IsSpriteEditModeActive || !_overlayState.HasOverlay || IsSpriteEditDragActive)
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
        var shouldRefreshPivotRelativePosition = inputKind == SpriteEditGeometryInputKind.Pivot;
        var (nextPivotX, nextPivotY) = shouldRefreshPivotRelativePosition
            ? (
                NormalizeFinite(pivotX, ResolvePivotSourceX()),
                NormalizeFinite(pivotY, ResolvePivotSourceY()))
            : ResolveSpriteEditPivotFromRelative(
                nextX,
                nextY,
                nextWidth,
                nextHeight);

        ApplySpriteEditGeometry(
            nextX,
            nextY,
            nextWidth,
            nextHeight,
            nextPivotX,
            nextPivotY,
            shouldRefreshPivotRelativePosition);
    }

    internal void ResetSpriteEditGeometryToOriginal()
    {
        if (!IsSpriteEditModeActive ||
            !_overlayState.HasOverlay ||
            IsSpriteEditDragActive ||
            !_overlayState.TryResetEditableGeometryToOriginal())
        {
            return;
        }

        RefreshSpriteEditPivotRelativePositionFromOverlay();
        RecalculateOverlayLayout();
        RecordSpriteEditGeometryHistory();
    }

    internal void UseFullImageForSpriteEditGeometry()
    {
        if (!IsSpriteEditModeActive || !_overlayState.HasOverlay || IsSpriteEditDragActive)
        {
            return;
        }

        var nextWidth = NormalizePositive(CanvasWidth, 1d);
        var nextHeight = NormalizePositive(CanvasHeight, 1d);
        var (nextPivotX, nextPivotY) = ResolveSpriteEditPivotFromRelative(
            cropX: 0d,
            cropY: 0d,
            cropWidth: nextWidth,
            cropHeight: nextHeight);
        ApplySpriteEditGeometry(
            cropX: 0d,
            cropY: 0d,
            cropWidth: nextWidth,
            cropHeight: nextHeight,
            pivotX: nextPivotX,
            pivotY: nextPivotY,
            refreshPivotRelativePosition: false);
    }

    private void ApplySpriteEditGeometry(
        double cropX,
        double cropY,
        double cropWidth,
        double cropHeight,
        double pivotX,
        double pivotY,
        bool refreshPivotRelativePosition,
        bool recordHistory = true)
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
        if (refreshPivotRelativePosition)
        {
            RefreshSpriteEditPivotRelativePositionFromOverlay();
        }

        RecalculateOverlayLayout();
        if (recordHistory)
        {
            RecordSpriteEditGeometryHistory();
        }
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
        NotifySpriteEditHistoryStateChanged();
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

    private static double NormalizePositive(double value, double fallback)
    {
        return double.IsFinite(value) && value > 0d
            ? value
            : fallback;
    }
}
