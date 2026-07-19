namespace AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;

internal readonly record struct DpiAwareWindowLayoutProfile(
    double InitialWidth,
    double InitialHeight,
    double MinimumWidth,
    double MinimumHeight,
    double WorkAreaMargin = 16d)
{
    public static DpiAwareWindowLayoutProfile MainWindow { get; } = new(
        InitialWidth: 1600d,
        InitialHeight: 960d,
        MinimumWidth: 900d,
        MinimumHeight: 640d);

    public static DpiAwareWindowLayoutProfile WorkspaceOperationWindow { get; } = new(
        InitialWidth: 1520d,
        InitialHeight: 920d,
        MinimumWidth: 1040d,
        MinimumHeight: 680d);
}
