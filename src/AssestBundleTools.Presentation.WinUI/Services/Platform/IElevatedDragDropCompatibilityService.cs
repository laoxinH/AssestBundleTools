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
        int clientX,
        int clientY,
        bool isClientArea)
    {
        SourceWindow = sourceWindow ?? throw new ArgumentNullException(nameof(sourceWindow));
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        ClientX = clientX;
        ClientY = clientY;
        IsClientArea = isClientArea;
    }

    public Window SourceWindow { get; }

    public IReadOnlyList<string> Paths { get; }

    public int ClientX { get; }

    public int ClientY { get; }

    public bool IsClientArea { get; }
}

internal readonly record struct ElevatedDragDropCompatibilityResult(
    bool Succeeded,
    string DiagnosticDetail);
