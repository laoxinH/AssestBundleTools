using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

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
    private readonly Dictionary<int, AssetEditorWindowStateSnapshot> _openWindowStates = [];
    private int _nextCascadeStep;
    private int _nextWindowStateId = 1;

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

    public int RegisterWindow(
        AssetEditorWindowLayoutSnapshot layout,
        AssetEditorWindowGroupSnapshot group)
    {
        var windowStateId = _nextWindowStateId++;
        _openWindowStates[windowStateId] = new AssetEditorWindowStateSnapshot(
            windowStateId,
            layout,
            group);
        RememberWindowLayout(layout);
        return windowStateId;
    }

    public void UpdateWindowLayout(int windowStateId, AssetEditorWindowLayoutSnapshot layout)
    {
        if (!_openWindowStates.TryGetValue(windowStateId, out var state))
        {
            return;
        }

        _openWindowStates[windowStateId] = state with { Layout = layout };
        RememberWindowLayout(layout);
    }

    public void UpdateWindowGroup(int windowStateId, AssetEditorWindowGroupSnapshot group)
    {
        if (!_openWindowStates.TryGetValue(windowStateId, out var state))
        {
            return;
        }

        _openWindowStates[windowStateId] = state with { Group = group };
    }

    public void RemoveWindow(int windowStateId)
    {
        _openWindowStates.Remove(windowStateId);
    }

    public IReadOnlyList<AssetEditorWindowStateSnapshot> CaptureOpenWindows() =>
        _openWindowStates.Values
            .OrderBy(state => state.WindowStateId)
            .ToArray();

    public void Clear()
    {
        _lastKnownLayout = null;
        _openWindowStates.Clear();
        _nextCascadeStep = 0;
        _nextWindowStateId = 1;
    }

    private static AssetEditorWindowLayoutSnapshot CreateDefaultLayout() =>
        new(DefaultX, DefaultY, DefaultWidth, DefaultHeight);

    private static bool CanRemember(AssetEditorWindowLayoutSnapshot layout) =>
        layout.Width >= MinimumRememberedWidth &&
        layout.Height >= MinimumRememberedHeight;
}

internal sealed record AssetEditorWindowStateSnapshot(
    int WindowStateId,
    AssetEditorWindowLayoutSnapshot Layout,
    AssetEditorWindowGroupSnapshot Group);

internal sealed record AssetEditorWindowGroupSnapshot(
    IReadOnlyList<AssetEditorWindowAssetTabSnapshot> AssetTabs,
    string? ActiveBundleId,
    string? ActiveAssetId)
{
    public static AssetEditorWindowGroupSnapshot FromDocumentGroup(AssetDocumentGroupViewModel group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return new AssetEditorWindowGroupSnapshot(
            group.AssetTabs
                .Select(AssetEditorWindowAssetTabSnapshot.FromAssetTab)
                .ToArray(),
            group.ActiveAssetTab?.BundleId.Value,
            group.ActiveAssetTab?.AssetId.Value);
    }
}

internal sealed record AssetEditorWindowAssetTabSnapshot(
    string BundleId,
    string AssetId,
    string DisplayName)
{
    public static AssetEditorWindowAssetTabSnapshot FromAssetTab(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        return new AssetEditorWindowAssetTabSnapshot(
            assetTab.BundleId.Value,
            assetTab.AssetId.Value,
            assetTab.DisplayName);
    }
}
