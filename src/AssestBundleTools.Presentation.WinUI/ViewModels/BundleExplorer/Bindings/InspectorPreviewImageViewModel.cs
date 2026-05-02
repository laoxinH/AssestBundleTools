using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 承载图片预览、Sprite 裁切覆盖层与裁切结果子状态。
/// </summary>
public sealed class InspectorPreviewImageViewModel : ObservableObject
{
    private string _overlayLabelText = string.Empty;
    private string _spriteFocusPreviewTitleText = string.Empty;
    private string _spriteFocusPreviewSizeText = string.Empty;
    private ImageSource? _imageSource;
    private ImageSource? _spriteFocusImageSource;
    private bool _isVisible;
    private double _canvasWidth = 1d;
    private double _canvasHeight = 1d;
    private double _overlayX;
    private double _overlayY;
    private double _overlayWidth;
    private double _overlayHeight;
    private double _overlayLabelX;
    private double _overlayLabelY;
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
    private double _viewportWidth;
    private double _viewportHeight;
    private double _overlaySourceX;
    private double _overlaySourceY;
    private double _overlaySourceWidth;
    private double _overlaySourceHeight;
    private double? _pivotSourceX;
    private double? _pivotSourceY;
    private double? _borderSourceLeft;
    private double? _borderSourceTop;
    private double? _borderSourceRight;
    private double? _borderSourceBottom;

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

    public double OverlayLabelX
    {
        get => _overlayLabelX;
        private set => SetProperty(ref _overlayLabelX, value);
    }

    public double OverlayLabelY
    {
        get => _overlayLabelY;
        private set => SetProperty(ref _overlayLabelY, value);
    }

    public string OverlayLabelText
    {
        get => _overlayLabelText;
        private set => SetProperty(ref _overlayLabelText, value);
    }

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

    public Visibility OverlayVisibility =>
        Visibility == Visibility.Visible &&
        OverlayWidth > 0 &&
        OverlayHeight > 0 &&
        !string.IsNullOrWhiteSpace(OverlayLabelText)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility SpriteFocusPreviewVisibility =>
        Visibility == Visibility.Visible && SpriteFocusImageSource is not null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility PivotVisibility =>
        OverlayVisibility == Visibility.Visible &&
        _pivotSourceX.HasValue &&
        _pivotSourceY.HasValue
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility BorderVisibility =>
        OverlayVisibility == Visibility.Visible &&
        (_borderSourceLeft is > 0d ||
         _borderSourceTop is > 0d ||
         _borderSourceRight is > 0d ||
         _borderSourceBottom is > 0d)
            ? Visibility.Visible
            : Visibility.Collapsed;

    internal void Apply(
        ImageSource imageSource,
        int imagePixelWidth,
        int imagePixelHeight,
        SpritePreviewOverlayDisplayState? overlayDisplayState,
        SpriteFocusPreviewDisplayState? focusPreviewDisplayState)
    {
        ArgumentNullException.ThrowIfNull(imageSource);

        Clear();
        _isVisible = true;
        ApplyDisplayState(imagePixelWidth, imagePixelHeight, overlayDisplayState);
        ApplyFocusPreviewDisplayState(focusPreviewDisplayState);
        ImageSource = imageSource;
        NotifyVisibilityChanged();
    }

    internal void UpdateViewport(double viewportWidth, double viewportHeight)
    {
        _viewportWidth = Math.Max(0d, viewportWidth);
        _viewportHeight = Math.Max(0d, viewportHeight);
        RecalculateOverlayLayout();
    }

    internal void Clear()
    {
        _isVisible = false;
        _viewportWidth = 0d;
        _viewportHeight = 0d;
        _overlaySourceX = 0d;
        _overlaySourceY = 0d;
        _overlaySourceWidth = 0d;
        _overlaySourceHeight = 0d;
        _pivotSourceX = null;
        _pivotSourceY = null;
        _borderSourceLeft = null;
        _borderSourceTop = null;
        _borderSourceRight = null;
        _borderSourceBottom = null;
        CanvasWidth = 1d;
        CanvasHeight = 1d;
        ImageSource = null;
        SpriteFocusImageSource = null;
        SpriteFocusPreviewTitleText = string.Empty;
        SpriteFocusPreviewSizeText = string.Empty;
        OverlayLabelText = string.Empty;
        ClearOverlayLayout();
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

        _overlaySourceX = overlayDisplayState is null ? 0d : Math.Max(0d, overlayDisplayState.RegionX);
        _overlaySourceY = overlayDisplayState is null ? 0d : Math.Max(0d, overlayDisplayState.RegionY);
        _overlaySourceWidth = overlayDisplayState is null ? 0d : Math.Max(0d, overlayDisplayState.RegionWidth);
        _overlaySourceHeight = overlayDisplayState is null ? 0d : Math.Max(0d, overlayDisplayState.RegionHeight);
        _pivotSourceX = overlayDisplayState?.PivotX;
        _pivotSourceY = overlayDisplayState?.PivotY;
        _borderSourceLeft = overlayDisplayState?.BorderLeft;
        _borderSourceTop = overlayDisplayState?.BorderTop;
        _borderSourceRight = overlayDisplayState?.BorderRight;
        _borderSourceBottom = overlayDisplayState?.BorderBottom;
        OverlayLabelText = string.IsNullOrWhiteSpace(overlayDisplayState?.BadgeText)
            ? string.Empty
            : overlayDisplayState.BadgeText.Trim();
        RecalculateOverlayLayout();
    }

    private void ApplyFocusPreviewDisplayState(SpriteFocusPreviewDisplayState? focusPreviewDisplayState)
    {
        SpriteFocusImageSource = focusPreviewDisplayState?.ImageSource;
        SpriteFocusPreviewTitleText = focusPreviewDisplayState?.TitleText ?? string.Empty;
        SpriteFocusPreviewSizeText = focusPreviewDisplayState?.SizeText ?? string.Empty;
    }

    private void ClearOverlayLayout()
    {
        OverlayX = 0d;
        OverlayY = 0d;
        OverlayWidth = 0d;
        OverlayHeight = 0d;
        OverlayLabelX = 0d;
        OverlayLabelY = 0d;
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
        if (_overlaySourceWidth <= 0d ||
            _overlaySourceHeight <= 0d ||
            string.IsNullOrWhiteSpace(OverlayLabelText))
        {
            ClearOverlayLayout();
            return;
        }

        if (!PreviewImageOverlayLayoutCalculator.TryCalculate(
                CanvasWidth,
                CanvasHeight,
                _overlaySourceX,
                _overlaySourceY,
                _overlaySourceWidth,
                _overlaySourceHeight,
                _viewportWidth,
                _viewportHeight,
                out var layout))
        {
            ClearOverlayLayout();
            return;
        }

        OverlayX = layout.OverlayX;
        OverlayY = layout.OverlayY;
        OverlayWidth = layout.OverlayWidth;
        OverlayHeight = layout.OverlayHeight;
        OverlayLabelX = layout.LabelX;
        OverlayLabelY = layout.LabelY;
        ApplyPivotLayout(layout);
        ApplyBorderLayout(layout);
        NotifyOverlayVisibilityChanged();
    }

    private void ApplyPivotLayout(PreviewImageOverlayLayout layout)
    {
        if (!_pivotSourceX.HasValue || !_pivotSourceY.HasValue)
        {
            PivotX = 0d;
            PivotY = 0d;
            return;
        }

        PivotX = layout.ImageOffsetX + (_pivotSourceX.Value * layout.ImageScale);
        PivotY = layout.ImageOffsetY + (_pivotSourceY.Value * layout.ImageScale);
    }

    private void ApplyBorderLayout(PreviewImageOverlayLayout layout)
    {
        var scale = layout.ImageScale;
        var left = Math.Max(0d, _borderSourceLeft ?? 0d) * scale;
        var top = Math.Max(0d, _borderSourceTop ?? 0d) * scale;
        var right = Math.Max(0d, _borderSourceRight ?? 0d) * scale;
        var bottom = Math.Max(0d, _borderSourceBottom ?? 0d) * scale;

        BorderLeftX = layout.OverlayX + Math.Min(left, layout.OverlayWidth);
        BorderRightX = layout.OverlayX + Math.Max(0d, layout.OverlayWidth - right);
        BorderTopY = layout.OverlayY + Math.Min(top, layout.OverlayHeight);
        BorderBottomY = layout.OverlayY + Math.Max(0d, layout.OverlayHeight - bottom);
        BorderVerticalTop = layout.OverlayY;
        BorderVerticalBottom = layout.OverlayY + layout.OverlayHeight;
        BorderVerticalHeight = layout.OverlayHeight;
        BorderHorizontalLeft = layout.OverlayX;
        BorderHorizontalRight = layout.OverlayX + layout.OverlayWidth;
        BorderHorizontalWidth = layout.OverlayWidth;
    }

    private void NotifyVisibilityChanged()
    {
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(SpriteFocusPreviewVisibility));
        NotifyOverlayVisibilityChanged();
    }

    private void NotifyOverlayVisibilityChanged()
    {
        OnPropertyChanged(nameof(OverlayVisibility));
        OnPropertyChanged(nameof(PivotVisibility));
        OnPropertyChanged(nameof(BorderVisibility));
    }
}
