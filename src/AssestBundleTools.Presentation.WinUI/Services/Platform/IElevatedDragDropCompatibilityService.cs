using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal interface IElevatedDragDropCompatibilityService
{
    event EventHandler<ElevatedFileDropReceivedEventArgs>? FilesDropped;

    ElevatedDragDropCompatibilityResult TryEnable(Window window);
}

internal sealed class ElevatedFileDropReceivedEventArgs : EventArgs
{
    public ElevatedFileDropReceivedEventArgs(
        Window sourceWindow,
        IReadOnlyList<string> paths,
        ElevatedNativeDropPosition position)
    {
        SourceWindow = sourceWindow ?? throw new ArgumentNullException(nameof(sourceWindow));
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        Position = position;
    }

    public Window SourceWindow { get; }

    public IReadOnlyList<string> Paths { get; }

    public ElevatedNativeDropPosition Position { get; }
}

internal readonly record struct ElevatedNativeDropPosition(
    int ClientX,
    int ClientY,
    bool IsClientArea,
    int ClientWidth,
    int ClientHeight,
    int ScreenX,
    int ScreenY,
    bool HasScreenPosition);

internal readonly record struct ElevatedDragDropCompatibilityResult(
    bool Succeeded,
    string DiagnosticDetail);
