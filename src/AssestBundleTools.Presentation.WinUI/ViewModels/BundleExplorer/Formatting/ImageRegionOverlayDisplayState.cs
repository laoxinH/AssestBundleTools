namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

/// <summary>
/// 表示图片预览上可同时显示的多个区域覆盖层。
/// </summary>
public sealed record ImageRegionOverlayDisplayState(
    double CanvasWidth,
    double CanvasHeight,
    IReadOnlyList<ImageRegionOverlayItemDisplayState> Regions,
    string BadgeText);

/// <summary>
/// 表示多区域图片覆盖层中的单个区域。
/// </summary>
public sealed record ImageRegionOverlayItemDisplayState(
    double X,
    double Y,
    double Width,
    double Height,
    string LabelText);
