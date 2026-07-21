using Microsoft.UI.Windowing;

namespace AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;

internal sealed class DpiAwareWindowSizeTracker
{
    private readonly nint _windowHandle;
    private readonly AppWindow _appWindow;

    public DpiAwareWindowSizeTracker(nint windowHandle, AppWindow appWindow)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(windowHandle, nint.Zero);
        _windowHandle = windowHandle;
        _appWindow = appWindow ?? throw new ArgumentNullException(nameof(appWindow));
        CaptureCurrentSize();
    }

    public DpiAwareWindowSize? LastRestoredSize { get; private set; }

    public void HandleWindowChanged(AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPresenterChange)
        {
            CaptureCurrentSize();
        }
    }

    public void CaptureCurrentSize()
    {
        if (DpiAwareWindowLayoutService.TryCaptureEffectiveSize(
                _windowHandle,
                _appWindow,
                out var size))
        {
            LastRestoredSize = size;
        }
    }
}
