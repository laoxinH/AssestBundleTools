namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal readonly record struct AssetEditorWindowWorkAreaSnapshot(
    int X,
    int Y,
    int Width,
    int Height);

internal static class AssetEditorWindowLayoutSafety
{
    public static bool CanRestoreSize(AssetEditorWindowLayoutSnapshot layout) =>
        layout.Width >= AssetEditorWindowStateStore.MinimumRememberedWidth &&
        layout.Height >= AssetEditorWindowStateStore.MinimumRememberedHeight;

    public static AssetEditorWindowLayoutSnapshot ClampToWorkArea(
        AssetEditorWindowLayoutSnapshot layout,
        AssetEditorWindowWorkAreaSnapshot workArea)
    {
        if (workArea.Width <= 0 || workArea.Height <= 0)
        {
            return layout;
        }

        var width = Math.Min(layout.Width, workArea.Width);
        var height = Math.Min(layout.Height, workArea.Height);
        var maxX = workArea.X + Math.Max(0, workArea.Width - width);
        var maxY = workArea.Y + Math.Max(0, workArea.Height - height);

        return layout with
        {
            X = Clamp(layout.X, workArea.X, maxX),
            Y = Clamp(layout.Y, workArea.Y, maxY),
            Width = width,
            Height = height
        };
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}

