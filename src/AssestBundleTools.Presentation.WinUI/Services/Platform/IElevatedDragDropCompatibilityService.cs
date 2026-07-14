using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal interface IElevatedDragDropCompatibilityService
{
    ElevatedDragDropCompatibilityResult TryEnable(Window window);
}

internal readonly record struct ElevatedDragDropCompatibilityResult(
    bool Succeeded,
    string DiagnosticDetail);
