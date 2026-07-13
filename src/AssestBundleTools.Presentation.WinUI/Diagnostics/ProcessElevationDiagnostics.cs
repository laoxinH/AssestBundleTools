using System.Security.Principal;

namespace AssestBundleTools.Presentation.WinUI.Diagnostics;

internal static class ProcessElevationDiagnostics
{
    public static ProcessElevationSnapshot Capture()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var isElevated = new WindowsPrincipal(identity)
                .IsInRole(WindowsBuiltInRole.Administrator);
            return new ProcessElevationSnapshot(isElevated, isElevated.ToString());
        }
        catch (Exception exception)
        {
            return new ProcessElevationSnapshot(
                IsElevated: null,
                DiagnosticValue: $"{exception.GetType().Name}:0x{exception.HResult:X8}");
        }
    }
}

internal readonly record struct ProcessElevationSnapshot(
    bool? IsElevated,
    string DiagnosticValue);
