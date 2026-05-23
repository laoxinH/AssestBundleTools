using System.Globalization;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewImageViewModel
{
    private const double SpriteEditCornerLabelWidth = 82d;
    private const double SpriteEditLengthLabelWidth = 56d;
    private const double SpriteEditMeasurementLabelHeight = 18d;
    private const double SpriteEditMeasurementLabelGap = 6d;
    private const double SpriteEditAxisLength = 48d;

    public Visibility SpriteEditMeasurementVisibility =>
        SpriteEditHandleVisibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

    public string SpriteEditTopLeftCoordinateText =>
        FormatCoordinate(_overlayState.SourceX, _overlayState.SourceY);

    public string SpriteEditTopRightCoordinateText =>
        FormatCoordinate(_overlayState.SourceX + _overlayState.SourceWidth, _overlayState.SourceY);

    public string SpriteEditBottomLeftCoordinateText =>
        FormatCoordinate(_overlayState.SourceX, _overlayState.SourceY + _overlayState.SourceHeight);

    public string SpriteEditBottomRightCoordinateText =>
        FormatCoordinate(_overlayState.SourceX + _overlayState.SourceWidth, _overlayState.SourceY + _overlayState.SourceHeight);

    public string SpriteEditTopLengthText => FormatLength(_overlayState.SourceWidth);

    public string SpriteEditBottomLengthText => FormatLength(_overlayState.SourceWidth);

    public string SpriteEditLeftLengthText => FormatLength(_overlayState.SourceHeight);

    public string SpriteEditRightLengthText => FormatLength(_overlayState.SourceHeight);

    public double SpriteEditTopLeftCoordinateLabelX =>
        ClampSpriteEditMeasurementLabelX(OverlayX + SpriteEditMeasurementLabelGap, SpriteEditCornerLabelWidth);

    public double SpriteEditTopLeftCoordinateLabelY =>
        ClampSpriteEditMeasurementLabelY(OverlayY - SpriteEditMeasurementLabelHeight - SpriteEditMeasurementLabelGap);

    public double SpriteEditTopRightCoordinateLabelX =>
        ClampSpriteEditMeasurementLabelX(OverlayRightX - SpriteEditCornerLabelWidth - SpriteEditMeasurementLabelGap, SpriteEditCornerLabelWidth);

    public double SpriteEditTopRightCoordinateLabelY =>
        ClampSpriteEditMeasurementLabelY(OverlayY - SpriteEditMeasurementLabelHeight - SpriteEditMeasurementLabelGap);

    public double SpriteEditBottomLeftCoordinateLabelX =>
        ClampSpriteEditMeasurementLabelX(OverlayX + SpriteEditMeasurementLabelGap, SpriteEditCornerLabelWidth);

    public double SpriteEditBottomLeftCoordinateLabelY =>
        ClampSpriteEditMeasurementLabelY(OverlayBottomY + SpriteEditMeasurementLabelGap);

    public double SpriteEditBottomRightCoordinateLabelX =>
        ClampSpriteEditMeasurementLabelX(OverlayRightX - SpriteEditCornerLabelWidth - SpriteEditMeasurementLabelGap, SpriteEditCornerLabelWidth);

    public double SpriteEditBottomRightCoordinateLabelY =>
        ClampSpriteEditMeasurementLabelY(OverlayBottomY + SpriteEditMeasurementLabelGap);

    public double SpriteEditTopLengthLabelX =>
        ClampSpriteEditMeasurementLabelX(OverlayX + (OverlayWidth / 2d) - (SpriteEditLengthLabelWidth / 2d), SpriteEditLengthLabelWidth);

    public double SpriteEditTopLengthLabelY =>
        ClampSpriteEditMeasurementLabelY(OverlayY + SpriteEditMeasurementLabelGap);

    public double SpriteEditBottomLengthLabelX =>
        ClampSpriteEditMeasurementLabelX(OverlayX + (OverlayWidth / 2d) - (SpriteEditLengthLabelWidth / 2d), SpriteEditLengthLabelWidth);

    public double SpriteEditBottomLengthLabelY =>
        ClampSpriteEditMeasurementLabelY(OverlayBottomY - SpriteEditMeasurementLabelHeight - SpriteEditMeasurementLabelGap);

    public double SpriteEditLeftLengthLabelX =>
        ClampSpriteEditMeasurementLabelX(OverlayX + SpriteEditMeasurementLabelGap, SpriteEditLengthLabelWidth);

    public double SpriteEditLeftLengthLabelY =>
        ClampSpriteEditMeasurementLabelY(OverlayY + (OverlayHeight / 2d) - (SpriteEditMeasurementLabelHeight / 2d));

    public double SpriteEditRightLengthLabelX =>
        ClampSpriteEditMeasurementLabelX(OverlayRightX - SpriteEditLengthLabelWidth - SpriteEditMeasurementLabelGap, SpriteEditLengthLabelWidth);

    public double SpriteEditRightLengthLabelY =>
        ClampSpriteEditMeasurementLabelY(OverlayY + (OverlayHeight / 2d) - (SpriteEditMeasurementLabelHeight / 2d));

    public double SpriteEditAxisOriginX => FullImageX;

    public double SpriteEditAxisOriginY => FullImageY;

    public double SpriteEditAxisXEndX => Math.Min(FullImageX + SpriteEditAxisLength, FullImageX + FullImageWidth);

    public double SpriteEditAxisYEndY => Math.Min(FullImageY + SpriteEditAxisLength, FullImageY + FullImageHeight);

    private void NotifySpriteEditMeasurementChanged()
    {
        OnPropertyChanged(nameof(SpriteEditMeasurementVisibility));
        OnPropertyChanged(nameof(SpriteEditTopLeftCoordinateText));
        OnPropertyChanged(nameof(SpriteEditTopRightCoordinateText));
        OnPropertyChanged(nameof(SpriteEditBottomLeftCoordinateText));
        OnPropertyChanged(nameof(SpriteEditBottomRightCoordinateText));
        OnPropertyChanged(nameof(SpriteEditTopLengthText));
        OnPropertyChanged(nameof(SpriteEditBottomLengthText));
        OnPropertyChanged(nameof(SpriteEditLeftLengthText));
        OnPropertyChanged(nameof(SpriteEditRightLengthText));
        OnPropertyChanged(nameof(SpriteEditTopLeftCoordinateLabelX));
        OnPropertyChanged(nameof(SpriteEditTopLeftCoordinateLabelY));
        OnPropertyChanged(nameof(SpriteEditTopRightCoordinateLabelX));
        OnPropertyChanged(nameof(SpriteEditTopRightCoordinateLabelY));
        OnPropertyChanged(nameof(SpriteEditBottomLeftCoordinateLabelX));
        OnPropertyChanged(nameof(SpriteEditBottomLeftCoordinateLabelY));
        OnPropertyChanged(nameof(SpriteEditBottomRightCoordinateLabelX));
        OnPropertyChanged(nameof(SpriteEditBottomRightCoordinateLabelY));
        OnPropertyChanged(nameof(SpriteEditTopLengthLabelX));
        OnPropertyChanged(nameof(SpriteEditTopLengthLabelY));
        OnPropertyChanged(nameof(SpriteEditBottomLengthLabelX));
        OnPropertyChanged(nameof(SpriteEditBottomLengthLabelY));
        OnPropertyChanged(nameof(SpriteEditLeftLengthLabelX));
        OnPropertyChanged(nameof(SpriteEditLeftLengthLabelY));
        OnPropertyChanged(nameof(SpriteEditRightLengthLabelX));
        OnPropertyChanged(nameof(SpriteEditRightLengthLabelY));
        OnPropertyChanged(nameof(SpriteEditAxisOriginX));
        OnPropertyChanged(nameof(SpriteEditAxisOriginY));
        OnPropertyChanged(nameof(SpriteEditAxisXEndX));
        OnPropertyChanged(nameof(SpriteEditAxisYEndY));
    }

    private double ClampSpriteEditMeasurementLabelX(double value, double width)
    {
        return Math.Clamp(value, 0d, Math.Max(0d, _viewportState.ViewportWidth - width));
    }

    private double ClampSpriteEditMeasurementLabelY(double value)
    {
        return Math.Clamp(value, 0d, Math.Max(0d, _viewportState.ViewportHeight - SpriteEditMeasurementLabelHeight));
    }

    private static string FormatCoordinate(double x, double y)
    {
        return "(" + FormatMeasurementValue(x) + ", " + FormatMeasurementValue(y) + ")";
    }

    private static string FormatLength(double value)
    {
        return FormatMeasurementValue(value);
    }

    private static string FormatMeasurementValue(double value)
    {
        return Math.Round(value, 2, MidpointRounding.AwayFromZero)
            .ToString("0.##", CultureInfo.InvariantCulture);
    }
}
