namespace AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;

internal readonly record struct DpiAwareWindowLayoutProfile(
    double InitialWidth,
    double InitialHeight,
    double MinimumWidth,
    double MinimumHeight,
    double WorkAreaMargin = 16d)
{
    public static DpiAwareWindowLayoutProfile MainWindow { get; } = new(
        InitialWidth: 1280d,
        InitialHeight: 768d,
        MinimumWidth: 900d,
        MinimumHeight: 640d);

    public static DpiAwareWindowLayoutProfile WorkspaceOperationWindow { get; } = new(
        InitialWidth: 1216d,
        InitialHeight: 736d,
        MinimumWidth: 1040d,
        MinimumHeight: 680d);

    public static DpiAwareWindowLayoutProfile AssetEditorWindow { get; } = new(
        InitialWidth: 1056d,
        InitialHeight: 656d,
        MinimumWidth: 760d,
        MinimumHeight: 520d);
}
