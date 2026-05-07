using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 保存 atlas / region 这类多区域覆盖层的源坐标，并投影到当前图片视口。
/// </summary>
internal sealed class PreviewImageRegionOverlayState
{
    private IReadOnlyList<ImageRegionOverlayItemDisplayState> _regions =
        Array.Empty<ImageRegionOverlayItemDisplayState>();

    public string BadgeText { get; private set; } = string.Empty;

    public bool HasRegions =>
        _regions.Count > 0 &&
        !string.IsNullOrWhiteSpace(BadgeText);

    public void Clear()
    {
        _regions = Array.Empty<ImageRegionOverlayItemDisplayState>();
        BadgeText = string.Empty;
    }

    public void Apply(ImageRegionOverlayDisplayState? displayState)
    {
        _regions = displayState?.Regions is { Count: > 0 } regions
            ? regions
            : Array.Empty<ImageRegionOverlayItemDisplayState>();
        BadgeText = string.IsNullOrWhiteSpace(displayState?.BadgeText)
            ? string.Empty
            : displayState.BadgeText.Trim();
        CanvasWidth = Math.Max(1d, displayState?.CanvasWidth ?? 1d);
        CanvasHeight = Math.Max(1d, displayState?.CanvasHeight ?? 1d);
    }

    public double CanvasWidth { get; private set; } = 1d;

    public double CanvasHeight { get; private set; } = 1d;

    public IReadOnlyList<PreviewImageRegionOverlayItemViewModel> CalculateLayouts(
        PreviewImageViewportState viewportState)
    {
        ArgumentNullException.ThrowIfNull(viewportState);

        if (!HasRegions)
        {
            return Array.Empty<PreviewImageRegionOverlayItemViewModel>();
        }

        return _regions
            .Select(region => TryCreateLayout(region, viewportState))
            .Where(static item => item is not null)
            .Select(static item => item!)
            .ToArray();
    }

    private PreviewImageRegionOverlayItemViewModel? TryCreateLayout(
        ImageRegionOverlayItemDisplayState region,
        PreviewImageViewportState viewportState)
    {
        if (!PreviewImageOverlayLayoutCalculator.TryCalculate(
                CanvasWidth,
                CanvasHeight,
                region.X,
                region.Y,
                region.Width,
                region.Height,
                viewportState.ViewportWidth,
                viewportState.ViewportHeight,
                viewportState.Zoom,
                viewportState.PanX,
                viewportState.PanY,
                out var layout))
        {
            return null;
        }

        return new PreviewImageRegionOverlayItemViewModel(
            layout.OverlayX,
            layout.OverlayY,
            layout.OverlayWidth,
            layout.OverlayHeight,
            region.LabelText);
    }
}

public sealed record PreviewImageRegionOverlayItemViewModel(
    double X,
    double Y,
    double Width,
    double Height,
    string LabelText);
