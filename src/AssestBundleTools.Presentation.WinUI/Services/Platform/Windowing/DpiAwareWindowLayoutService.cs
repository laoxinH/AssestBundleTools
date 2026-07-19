using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;

internal static class DpiAwareWindowLayoutService
{
    private const uint DefaultDpi = 96;

    public static DpiAwareWindowLayout Apply(
        nint windowHandle,
        AppWindow appWindow,
        DpiAwareWindowLayoutProfile profile)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(windowHandle, nint.Zero);
        ArgumentNullException.ThrowIfNull(appWindow);

        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        var dpi = GetDpiForWindow(windowHandle);
        var layout = DpiAwareWindowLayoutCalculator.Calculate(
            profile,
            dpi > 0 ? dpi : DefaultDpi,
            new PhysicalWorkArea(workArea.X, workArea.Y, workArea.Width, workArea.Height));

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = layout.MinimumWidth;
            presenter.PreferredMinimumHeight = layout.MinimumHeight;
        }

        appWindow.MoveAndResize(new RectInt32(layout.X, layout.Y, layout.Width, layout.Height));
        return layout;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);
}
