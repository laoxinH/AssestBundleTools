using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    public ImageSource? ImageSource
    {
        get => _imageSource;
        private set => SetProperty(ref _imageSource, value);
    }

    public ImageSource? SpriteFocusImageSource
    {
        get => _spriteFocusImageSource;
        private set => SetProperty(ref _spriteFocusImageSource, value);
    }

    public string SpriteFocusPreviewTitleText
    {
        get => _spriteFocusPreviewTitleText;
        private set => SetProperty(ref _spriteFocusPreviewTitleText, value);
    }

    public string SpriteFocusPreviewSizeText
    {
        get => _spriteFocusPreviewSizeText;
        private set => SetProperty(ref _spriteFocusPreviewSizeText, value);
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        private set => SetProperty(ref _canvasWidth, value);
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        private set => SetProperty(ref _canvasHeight, value);
    }

    public double FullImageX
    {
        get => _fullImageX;
        private set => SetProperty(ref _fullImageX, value);
    }

    public double FullImageY
    {
        get => _fullImageY;
        private set => SetProperty(ref _fullImageY, value);
    }

    public double FullImageWidth
    {
        get => _fullImageWidth;
        private set => SetProperty(ref _fullImageWidth, value);
    }

    public double FullImageHeight
    {
        get => _fullImageHeight;
        private set => SetProperty(ref _fullImageHeight, value);
    }

    public double SpriteFocusImageX
    {
        get => _spriteFocusImageX;
        private set => SetProperty(ref _spriteFocusImageX, value);
    }

    public double SpriteFocusImageY
    {
        get => _spriteFocusImageY;
        private set => SetProperty(ref _spriteFocusImageY, value);
    }

    public double SpriteFocusImageWidth
    {
        get => _spriteFocusImageWidth;
        private set => SetProperty(ref _spriteFocusImageWidth, value);
    }

    public double SpriteFocusImageHeight
    {
        get => _spriteFocusImageHeight;
        private set => SetProperty(ref _spriteFocusImageHeight, value);
    }

    public double OverlayX
    {
        get => _overlayX;
        private set => SetProperty(ref _overlayX, value);
    }

    public double OverlayY
    {
        get => _overlayY;
        private set => SetProperty(ref _overlayY, value);
    }

    public double OverlayWidth
    {
        get => _overlayWidth;
        private set => SetProperty(ref _overlayWidth, value);
    }

    public double OverlayHeight
    {
        get => _overlayHeight;
        private set => SetProperty(ref _overlayHeight, value);
    }

    public string OverlayLabelText
    {
        get => _overlayLabelText;
        private set => SetProperty(ref _overlayLabelText, value);
    }

    public string RegionOverlayBadgeText
    {
        get => _regionOverlayBadgeText;
        private set => SetProperty(ref _regionOverlayBadgeText, value);
    }

    public string InfoBadgePrimaryText
    {
        get => _infoBadgePrimaryText;
        private set
        {
            if (SetProperty(ref _infoBadgePrimaryText, value))
            {
                NotifyInfoBadgeVisibilityChanged();
            }
        }
    }

    public string InfoBadgeSecondaryText
    {
        get => _infoBadgeSecondaryText;
        private set
        {
            if (SetProperty(ref _infoBadgeSecondaryText, value))
            {
                NotifyInfoBadgeVisibilityChanged();
            }
        }
    }

    public string InfoBadgeDetailText
    {
        get => _infoBadgeDetailText;
        private set
        {
            if (SetProperty(ref _infoBadgeDetailText, value))
            {
                NotifyInfoBadgeVisibilityChanged();
            }
        }
    }

    public double Zoom => _viewportState.Zoom;

    public double PanX => _viewportState.PanX;

    public double PanY => _viewportState.PanY;

    public double PivotX
    {
        get => _pivotX;
        private set => SetProperty(ref _pivotX, value);
    }

    public double PivotY
    {
        get => _pivotY;
        private set => SetProperty(ref _pivotY, value);
    }

    public double BorderLeftX
    {
        get => _borderLeftX;
        private set => SetProperty(ref _borderLeftX, value);
    }

    public double BorderRightX
    {
        get => _borderRightX;
        private set => SetProperty(ref _borderRightX, value);
    }

    public double BorderTopY
    {
        get => _borderTopY;
        private set => SetProperty(ref _borderTopY, value);
    }

    public double BorderBottomY
    {
        get => _borderBottomY;
        private set => SetProperty(ref _borderBottomY, value);
    }

    public double BorderVerticalTop
    {
        get => _borderVerticalTop;
        private set => SetProperty(ref _borderVerticalTop, value);
    }

    public double BorderVerticalBottom
    {
        get => _borderVerticalBottom;
        private set => SetProperty(ref _borderVerticalBottom, value);
    }

    public double BorderVerticalHeight
    {
        get => _borderVerticalHeight;
        private set => SetProperty(ref _borderVerticalHeight, value);
    }

    public double BorderHorizontalLeft
    {
        get => _borderHorizontalLeft;
        private set => SetProperty(ref _borderHorizontalLeft, value);
    }

    public double BorderHorizontalRight
    {
        get => _borderHorizontalRight;
        private set => SetProperty(ref _borderHorizontalRight, value);
    }

    public double BorderHorizontalWidth
    {
        get => _borderHorizontalWidth;
        private set => SetProperty(ref _borderHorizontalWidth, value);
    }

    public Visibility Visibility =>
        _isVisible && ImageSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ControlsVisibility =>
        Visibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility OverlayVisibility =>
        Visibility == Visibility.Visible &&
        OverlayWidth > 0 &&
        OverlayHeight > 0 &&
        !string.IsNullOrWhiteSpace(OverlayLabelText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility OverlayFrameVisibility =>
        OverlayVisibility == Visibility.Visible &&
        !IsSpriteEditModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SpriteEditFrameVisibility =>
        OverlayVisibility == Visibility.Visible &&
        IsSpriteEditModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility RegionOverlayVisibility =>
        Visibility == Visibility.Visible &&
        DisplayOptions.FullImagePreviewVisibility == Visibility.Visible &&
        RegionOverlays.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility RegionOverlayBadgeVisibility =>
        RegionOverlayVisibility == Visibility.Visible &&
        !IsSpriteEditModeActive &&
        !string.IsNullOrWhiteSpace(RegionOverlayBadgeText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility InfoBadgeVisibility =>
        Visibility == Visibility.Visible &&
        DisplayOptions.FullImagePreviewVisibility == Visibility.Visible &&
        !IsSpriteEditModeActive &&
        HasInfoBadgeText
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility InfoBadgeSecondaryVisibility =>
        string.IsNullOrWhiteSpace(InfoBadgeSecondaryText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility InfoBadgeDetailVisibility =>
        string.IsNullOrWhiteSpace(InfoBadgeDetailText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SpriteFocusPreviewVisibility =>
        Visibility == Visibility.Visible && SpriteFocusImageSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PivotVisibility =>
        OverlayVisibility == Visibility.Visible &&
        _overlayState.HasPivot
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility BorderVisibility =>
        OverlayVisibility == Visibility.Visible &&
        _overlayState.HasBorder
            ? Visibility.Visible
            : Visibility.Collapsed;
}
