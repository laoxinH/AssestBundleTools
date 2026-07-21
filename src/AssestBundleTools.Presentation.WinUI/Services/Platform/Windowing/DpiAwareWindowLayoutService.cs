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
        DpiAwareWindowLayoutProfile profile,
        DpiAwareWindowSize? preferredSize = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(windowHandle, nint.Zero);
        ArgumentNullException.ThrowIfNull(appWindow);

        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        var dpi = GetDpiForWindow(windowHandle);
        var effectiveProfile = preferredSize is { IsValid: true } size
            ? profile with { InitialWidth = size.Width, InitialHeight = size.Height }
            : profile;
        var layout = DpiAwareWindowLayoutCalculator.Calculate(
            effectiveProfile,
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

    public static bool TryCaptureEffectiveSize(
        nint windowHandle,
        AppWindow appWindow,
        out DpiAwareWindowSize size)
    {
        ArgumentNullException.ThrowIfNull(appWindow);
        if (windowHandle == nint.Zero ||
            appWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
        {
            size = default;
            return false;
        }

        var dpi = GetDpiForWindow(windowHandle);
        var scale = Math.Max(dpi, DefaultDpi) / (double)DefaultDpi;
        var physicalSize = appWindow.Size;
        size = new DpiAwareWindowSize(
            physicalSize.Width / scale,
            physicalSize.Height / scale);
        return size.IsValid;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint windowHandle);
}
