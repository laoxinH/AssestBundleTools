namespace AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;

internal readonly record struct PhysicalWorkArea(int X, int Y, int Width, int Height);

internal readonly record struct DpiAwareWindowLayout(
    int X,
    int Y,
    int Width,
    int Height,
    int MinimumWidth,
    int MinimumHeight);

internal static class DpiAwareWindowLayoutCalculator
{
    private const double DefaultDpi = 96d;

    public static DpiAwareWindowLayout Calculate(
        DpiAwareWindowLayoutProfile profile,
        uint dpi,
        PhysicalWorkArea workArea)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(profile.InitialWidth, 0d);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(profile.InitialHeight, 0d);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(profile.MinimumWidth, 0d);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(profile.MinimumHeight, 0d);
        ArgumentOutOfRangeException.ThrowIfNegative(profile.WorkAreaMargin);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(workArea.Width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(workArea.Height, 0);

        var scale = Math.Max(dpi, (uint)DefaultDpi) / DefaultDpi;
        var margin = ToPhysicalPixels(profile.WorkAreaMargin, scale);
        var availableWidth = Math.Max(1, workArea.Width - (margin * 2));
        var availableHeight = Math.Max(1, workArea.Height - (margin * 2));
        var minimumWidth = Math.Min(ToPhysicalPixels(profile.MinimumWidth, scale), availableWidth);
        var minimumHeight = Math.Min(ToPhysicalPixels(profile.MinimumHeight, scale), availableHeight);
        var width = Math.Clamp(ToPhysicalPixels(profile.InitialWidth, scale), minimumWidth, availableWidth);
        var height = Math.Clamp(ToPhysicalPixels(profile.InitialHeight, scale), minimumHeight, availableHeight);

        return new DpiAwareWindowLayout(
            X: workArea.X + ((workArea.Width - width) / 2),
            Y: workArea.Y + ((workArea.Height - height) / 2),
            Width: width,
            Height: height,
            MinimumWidth: minimumWidth,
            MinimumHeight: minimumHeight);
    }

    private static int ToPhysicalPixels(double effectivePixels, double scale) =>
        Math.Max(1, checked((int)Math.Ceiling(effectivePixels * scale)));
}
