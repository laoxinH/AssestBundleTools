using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal interface IElevatedFileDropRouter
{
    IDisposable RegisterTarget(ElevatedFileDropTargetRegistration registration);
}

internal sealed record ElevatedFileDropTargetRegistration(
    string TargetId,
    Window SourceWindow,
    FrameworkElement TargetElement,
    int Priority,
    Func<bool> CanHandle,
    Func<ElevatedFileDropContext, Task> HandleDropAsync,
    Func<Point, bool>? HitTest = null);

internal enum ElevatedDropCoordinateValidationMode
{
    NativeClientAreaFlag,
    ClientBounds,
    ClientBoundsFallback
}

internal static class ElevatedFileDropTargetPriorities
{
    public const int Surface = 100;
    public const int Item = 200;
    public const int Overlay = 300;
}

internal sealed record ElevatedFileDropContext(
    string TargetId,
    IReadOnlyList<string> Paths,
    ElevatedNativeDropPosition NativePosition,
    ElevatedDropCoordinateMapping Coordinates);

internal sealed record ElevatedDropCoordinateMapping(
    Point RootPosition,
    Point LocalPosition,
    double ScaleX,
    double ScaleY,
    ElevatedDropCoordinateValidationMode ValidationMode);
