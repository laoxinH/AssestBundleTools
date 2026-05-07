using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载图片预览、Sprite 裁切覆盖层与裁切结果子状态。
/// </summary>
public sealed partial class InspectorPreviewImageViewModel : ObservableObject
{
    private readonly PreviewImageViewportState _viewportState = new();
    private readonly PreviewImageOverlayState _overlayState = new();
    private readonly PreviewImageRegionOverlayState _regionOverlayState = new();
    private string _overlayLabelText = string.Empty;
    private string _regionOverlayBadgeText = string.Empty;
    private string _infoBadgePrimaryText = string.Empty;
    private string _infoBadgeSecondaryText = string.Empty;
    private string _infoBadgeDetailText = string.Empty;
    private string _spriteFocusPreviewTitleText = string.Empty;
    private string _spriteFocusPreviewSizeText = string.Empty;
    private ImageSource? _imageSource;
    private ImageSource? _spriteFocusImageSource;
    private bool _isVisible;
    private double _canvasWidth = 1d;
    private double _canvasHeight = 1d;
    private double _fullImageX;
    private double _fullImageY;
    private double _fullImageWidth = 1d;
    private double _fullImageHeight = 1d;
    private double _spriteFocusImageX;
    private double _spriteFocusImageY;
    private double _spriteFocusImageWidth = 1d;
    private double _spriteFocusImageHeight = 1d;
    private double _overlayX;
    private double _overlayY;
    private double _overlayWidth;
    private double _overlayHeight;
    private double _pivotX;
    private double _pivotY;
    private double _borderLeftX;
    private double _borderRightX;
    private double _borderTopY;
    private double _borderBottomY;
    private double _borderVerticalTop;
    private double _borderVerticalBottom;
    private double _borderVerticalHeight;
    private double _borderHorizontalLeft;
    private double _borderHorizontalRight;
    private double _borderHorizontalWidth;
    private double _spriteFocusCanvasWidth = 1d;
    private double _spriteFocusCanvasHeight = 1d;

    public InspectorPreviewImageDisplayOptionsViewModel DisplayOptions { get; } = new();

    public ObservableCollection<PreviewImageRegionOverlayItemViewModel> RegionOverlays { get; } = [];

    public event EventHandler? ViewStateChanged;

    public InspectorPreviewImageViewModel()
    {
        DisplayOptions.PropertyChanged += OnDisplayOptionsPropertyChanged;
    }

    internal void Apply(
        ImageSource imageSource,
        int imagePixelWidth,
        int imagePixelHeight,
        SpritePreviewOverlayDisplayState? overlayDisplayState,
        SpriteFocusPreviewDisplayState? focusPreviewDisplayState,
        ImagePreviewInfoDisplayState? infoDisplayState = null,
        ImageRegionOverlayDisplayState? regionOverlayDisplayState = null)
    {
        ArgumentNullException.ThrowIfNull(imageSource);

        Clear();
        _isVisible = true;
        ApplyDisplayState(imagePixelWidth, imagePixelHeight, overlayDisplayState);
        ApplyFocusPreviewDisplayState(focusPreviewDisplayState);
        ApplyInfoDisplayState(infoDisplayState);
        ApplyRegionOverlayDisplayState(regionOverlayDisplayState);
        ImageSource = imageSource;
        if (_viewportState.ClampPan(ResolveActiveImageViewportSource()))
        {
            NotifyViewStatePropertiesChanged();
        }

        RecalculateImageLayout();
        RecalculateOverlays();
        NotifyVisibilityChanged();
    }

    internal void UpdateViewport(double viewportWidth, double viewportHeight)
    {
        if (!double.IsFinite(viewportWidth) ||
            !double.IsFinite(viewportHeight) ||
            viewportWidth <= 0d ||
            viewportHeight <= 0d)
        {
            return;
        }

        _viewportState.UpdateViewport(viewportWidth, viewportHeight);
        if (_viewportState.ClampPan(ResolveActiveImageViewportSource()))
        {
            NotifyViewStatePropertiesChanged();
        }

        RecalculateImageLayout();
        RecalculateOverlays();
    }

    internal void RestoreViewState(double? zoom, double? panX, double? panY)
    {
        if (_viewportState.Restore(zoom, panX, panY, ResolveActiveImageViewportSource()))
        {
            NotifyViewStatePropertiesChanged();
        }

        RecalculateImageLayout();
        RecalculateOverlays();
    }

    internal void ZoomBy(double scale, double? anchorX = null, double? anchorY = null)
    {
        if (_viewportState.ZoomBy(scale, anchorX, anchorY, ResolveActiveImageViewportSource()))
        {
            NotifyViewStatePropertiesChanged();
            RecalculateImageLayout();
            RecalculateOverlays();
            ViewStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void PanBy(double deltaX, double deltaY)
    {
        if (_viewportState.PanBy(deltaX, deltaY, ResolveActiveImageViewportSource()))
        {
            NotifyViewStatePropertiesChanged();
            RecalculateImageLayout();
            RecalculateOverlays();
            ViewStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void ResetView()
    {
        if (_viewportState.Reset(ResolveActiveImageViewportSource()))
        {
            NotifyViewStatePropertiesChanged();
            RecalculateImageLayout();
            RecalculateOverlays();
            ViewStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void Clear()
    {
        _isVisible = false;
        _viewportState.UpdateViewport(0d, 0d);
        _overlayState.Clear();
        _regionOverlayState.Clear();
        RegionOverlays.Clear();
        _spriteFocusCanvasWidth = 1d;
        _spriteFocusCanvasHeight = 1d;
        CanvasWidth = 1d;
        CanvasHeight = 1d;
        FullImageX = 0d;
        FullImageY = 0d;
        FullImageWidth = 1d;
        FullImageHeight = 1d;
        SpriteFocusImageX = 0d;
        SpriteFocusImageY = 0d;
        SpriteFocusImageWidth = 1d;
        SpriteFocusImageHeight = 1d;
        ImageSource = null;
        SpriteFocusImageSource = null;
        InfoBadgePrimaryText = string.Empty;
        InfoBadgeSecondaryText = string.Empty;
        InfoBadgeDetailText = string.Empty;
        SpriteFocusPreviewTitleText = string.Empty;
        SpriteFocusPreviewSizeText = string.Empty;
        OverlayLabelText = string.Empty;
        RegionOverlayBadgeText = string.Empty;
        ClearOverlayLayout();
        NotifyRegionOverlayVisibilityChanged();
        NotifyVisibilityChanged();
    }

    private void ApplyDisplayState(
        int imagePixelWidth,
        int imagePixelHeight,
        SpritePreviewOverlayDisplayState? overlayDisplayState)
    {
        CanvasWidth = overlayDisplayState is { CanvasWidth: > 0 }
            ? overlayDisplayState.CanvasWidth
            : Math.Max(1, imagePixelWidth);
        CanvasHeight = overlayDisplayState is { CanvasHeight: > 0 }
            ? overlayDisplayState.CanvasHeight
            : Math.Max(1, imagePixelHeight);

        _overlayState.Apply(overlayDisplayState);
        OverlayLabelText = _overlayState.LabelText;
        RecalculateOverlays();
    }

    private void ApplyFocusPreviewDisplayState(SpriteFocusPreviewDisplayState? focusPreviewDisplayState)
    {
        SpriteFocusImageSource = focusPreviewDisplayState?.ImageSource;
        SpriteFocusPreviewTitleText = focusPreviewDisplayState?.TitleText ?? string.Empty;
        SpriteFocusPreviewSizeText = focusPreviewDisplayState?.SizeText ?? string.Empty;
        _spriteFocusCanvasWidth = Math.Max(1d, focusPreviewDisplayState?.PixelWidth ?? 1);
        _spriteFocusCanvasHeight = Math.Max(1d, focusPreviewDisplayState?.PixelHeight ?? 1);
        RecalculateImageLayout();
    }

    private void ApplyInfoDisplayState(ImagePreviewInfoDisplayState? infoDisplayState)
    {
        InfoBadgePrimaryText = infoDisplayState?.PrimaryText ?? string.Empty;
        InfoBadgeSecondaryText = infoDisplayState?.SecondaryText ?? string.Empty;
        InfoBadgeDetailText = infoDisplayState?.DetailText ?? string.Empty;
    }

    private void ApplyRegionOverlayDisplayState(ImageRegionOverlayDisplayState? regionOverlayDisplayState)
    {
        _regionOverlayState.Apply(regionOverlayDisplayState);
        RegionOverlayBadgeText = _regionOverlayState.BadgeText;
        RecalculateRegionOverlayLayout();
    }

    private void ClearOverlayLayout()
    {
        OverlayX = 0d;
        OverlayY = 0d;
        OverlayWidth = 0d;
        OverlayHeight = 0d;
        PivotX = 0d;
        PivotY = 0d;
        BorderLeftX = 0d;
        BorderRightX = 0d;
        BorderTopY = 0d;
        BorderBottomY = 0d;
        BorderVerticalTop = 0d;
        BorderVerticalBottom = 0d;
        BorderVerticalHeight = 0d;
        BorderHorizontalLeft = 0d;
        BorderHorizontalRight = 0d;
        BorderHorizontalWidth = 0d;
        NotifyOverlayVisibilityChanged();
    }

    private void RecalculateOverlayLayout()
    {
        if (!_overlayState.HasOverlay)
        {
            ClearOverlayLayout();
            return;
        }

        if (!_overlayState.TryCalculateLayout(
                CanvasWidth,
                CanvasHeight,
                _viewportState,
                out var layout))
        {
            ClearOverlayLayout();
            return;
        }

        OverlayX = layout.OverlayX;
        OverlayY = layout.OverlayY;
        OverlayWidth = layout.OverlayWidth;
        OverlayHeight = layout.OverlayHeight;
        PivotX = layout.PivotX;
        PivotY = layout.PivotY;
        BorderLeftX = layout.BorderLeftX;
        BorderRightX = layout.BorderRightX;
        BorderTopY = layout.BorderTopY;
        BorderBottomY = layout.BorderBottomY;
        BorderVerticalTop = layout.BorderVerticalTop;
        BorderVerticalBottom = layout.BorderVerticalBottom;
        BorderVerticalHeight = layout.BorderVerticalHeight;
        BorderHorizontalLeft = layout.BorderHorizontalLeft;
        BorderHorizontalRight = layout.BorderHorizontalRight;
        BorderHorizontalWidth = layout.BorderHorizontalWidth;
        NotifyOverlayVisibilityChanged();
    }

    private void RecalculateOverlays()
    {
        RecalculateOverlayLayout();
        RecalculateRegionOverlayLayout();
    }

    private void RecalculateRegionOverlayLayout()
    {
        RegionOverlays.Clear();
        foreach (var overlay in _regionOverlayState.CalculateLayouts(_viewportState))
        {
            RegionOverlays.Add(overlay);
        }

        OnPropertyChanged(nameof(RegionOverlays));
        NotifyRegionOverlayVisibilityChanged();
    }

    private void NotifyVisibilityChanged()
    {
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(ControlsVisibility));
        OnPropertyChanged(nameof(SpriteFocusPreviewVisibility));
        NotifyInfoBadgeVisibilityChanged();
        NotifyRegionOverlayVisibilityChanged();
        DisplayOptions.UpdateImageState(
            Visibility == Visibility.Visible,
            SpriteFocusPreviewVisibility == Visibility.Visible);
        NotifyOverlayVisibilityChanged();
    }

    private void RecalculateImageLayout()
    {
        ApplyImageLayout(
            CanvasWidth,
            CanvasHeight,
            fullImage: true);
        ApplyImageLayout(
            _spriteFocusCanvasWidth,
            _spriteFocusCanvasHeight,
            fullImage: false);
    }

    private void ApplyImageLayout(double sourceWidth, double sourceHeight, bool fullImage)
    {
        if (!_viewportState.TryCalculateLayout(sourceWidth, sourceHeight, out var layout))
        {
            if (fullImage)
            {
                FullImageX = 0d;
                FullImageY = 0d;
                FullImageWidth = Math.Max(1d, sourceWidth);
                FullImageHeight = Math.Max(1d, sourceHeight);
            }
            else
            {
                SpriteFocusImageX = 0d;
                SpriteFocusImageY = 0d;
                SpriteFocusImageWidth = Math.Max(1d, sourceWidth);
                SpriteFocusImageHeight = Math.Max(1d, sourceHeight);
            }

            return;
        }

        if (fullImage)
        {
            FullImageX = layout.ImageOffsetX;
            FullImageY = layout.ImageOffsetY;
            FullImageWidth = layout.ImageWidth;
            FullImageHeight = layout.ImageHeight;
        }
        else
        {
            SpriteFocusImageX = layout.ImageOffsetX;
            SpriteFocusImageY = layout.ImageOffsetY;
            SpriteFocusImageWidth = layout.ImageWidth;
            SpriteFocusImageHeight = layout.ImageHeight;
        }
    }

    private PreviewImageViewportSource? ResolveActiveImageViewportSource()
    {
        if (DisplayOptions.IsSpriteCropResultSelected && SpriteFocusImageSource is not null)
        {
            return new PreviewImageViewportSource(
                _spriteFocusCanvasWidth,
                _spriteFocusCanvasHeight);
        }

        if (ImageSource is not null)
        {
            return new PreviewImageViewportSource(CanvasWidth, CanvasHeight);
        }

        return null;
    }

    private void NotifyOverlayVisibilityChanged()
    {
        OnPropertyChanged(nameof(OverlayVisibility));
        OnPropertyChanged(nameof(PivotVisibility));
        OnPropertyChanged(nameof(BorderVisibility));
    }

    private void NotifyRegionOverlayVisibilityChanged()
    {
        OnPropertyChanged(nameof(RegionOverlayVisibility));
        OnPropertyChanged(nameof(RegionOverlayBadgeVisibility));
    }

    private void NotifyInfoBadgeVisibilityChanged()
    {
        OnPropertyChanged(nameof(InfoBadgeVisibility));
        OnPropertyChanged(nameof(InfoBadgeSecondaryVisibility));
        OnPropertyChanged(nameof(InfoBadgeDetailVisibility));
    }

    private void OnDisplayOptionsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(InspectorPreviewImageDisplayOptionsViewModel.FullImagePreviewVisibility) or
            nameof(InspectorPreviewImageDisplayOptionsViewModel.SpriteCropResultVisibility)))
        {
            return;
        }

        if (_viewportState.ClampPan(ResolveActiveImageViewportSource()))
        {
            NotifyViewStatePropertiesChanged();
            ViewStateChanged?.Invoke(this, EventArgs.Empty);
        }

        RecalculateImageLayout();
        RecalculateOverlays();
        NotifyInfoBadgeVisibilityChanged();
        NotifyRegionOverlayVisibilityChanged();
    }

    private void NotifyViewStatePropertiesChanged()
    {
        OnPropertyChanged(nameof(Zoom));
        OnPropertyChanged(nameof(PanX));
        OnPropertyChanged(nameof(PanY));
    }

    private bool HasInfoBadgeText =>
        !string.IsNullOrWhiteSpace(InfoBadgePrimaryText) ||
        !string.IsNullOrWhiteSpace(InfoBadgeSecondaryText) ||
        !string.IsNullOrWhiteSpace(InfoBadgeDetailText);
}
