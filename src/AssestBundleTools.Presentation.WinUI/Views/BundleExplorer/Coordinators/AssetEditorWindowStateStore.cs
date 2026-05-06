namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal readonly record struct AssetEditorWindowLayoutSnapshot(
    int X,
    int Y,
    int Width,
    int Height)
{
    public AssetEditorWindowLayoutSnapshot Offset(int deltaX, int deltaY) =>
        this with
        {
            X = X + deltaX,
            Y = Y + deltaY,
        };
}

internal sealed class AssetEditorWindowStateStore
{
    internal const int DefaultX = 120;
    internal const int DefaultY = 90;
    internal const int DefaultWidth = 1320;
    internal const int DefaultHeight = 820;
    internal const int MinimumRememberedWidth = 760;
    internal const int MinimumRememberedHeight = 520;
    internal const int CascadeOffset = 32;
    private const int MaxCascadeSteps = 8;

    private AssetEditorWindowLayoutSnapshot? _lastKnownLayout;
    private int _nextCascadeStep;

    public AssetEditorWindowLayoutSnapshot CreateNextWindowLayout()
    {
        var baseLayout = _lastKnownLayout ?? CreateDefaultLayout();
        var cascadeStep = _nextCascadeStep;
        _nextCascadeStep = (_nextCascadeStep + 1) % MaxCascadeSteps;

        return baseLayout.Offset(
            CascadeOffset * cascadeStep,
            CascadeOffset * cascadeStep);
    }

    public void RememberWindowLayout(AssetEditorWindowLayoutSnapshot layout)
    {
        if (!CanRemember(layout))
        {
            return;
        }

        _lastKnownLayout = layout;
        _nextCascadeStep = 1;
    }

    public void Clear()
    {
        _lastKnownLayout = null;
        _nextCascadeStep = 0;
    }

    private static AssetEditorWindowLayoutSnapshot CreateDefaultLayout() =>
        new(DefaultX, DefaultY, DefaultWidth, DefaultHeight);

    private static bool CanRemember(AssetEditorWindowLayoutSnapshot layout) =>
        layout.Width >= MinimumRememberedWidth &&
        layout.Height >= MinimumRememberedHeight;
}
