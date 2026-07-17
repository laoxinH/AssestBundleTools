using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class NativeOlePointerReleaseMonitor : IDisposable
{
    private const int VirtualKeyLeftButton = 0x01;
    private const uint WindowMessageLeftButtonUp = 0x0202;
    private const int PollIntervalMilliseconds = 8;
    private readonly IntPtr _sourceWindow;
    private readonly string? _diagnosticPath;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Thread _thread;

    private NativeOlePointerReleaseMonitor(IntPtr sourceWindow, string? diagnosticPath)
    {
        _sourceWindow = sourceWindow;
        _diagnosticPath = diagnosticPath;
        _thread = new Thread(MonitorPointerRelease)
        {
            IsBackground = true,
            Name = "AssestBundleTools elevated drag release monitor"
        };
        _thread.Start();
    }

    public static NativeOlePointerReleaseMonitor Start(
        IntPtr sourceWindow,
        string? diagnosticPath) =>
        new(sourceWindow, diagnosticPath);

    public void Dispose()
    {
        _cancellation.Cancel();
        _ = _thread.Join(millisecondsTimeout: 100);
        _cancellation.Dispose();
    }

    private void MonitorPointerRelease()
    {
        var observedPressed = false;
        var releasedPollCount = 0;
        while (!_cancellation.IsCancellationRequested)
        {
            var isPressed = (GetAsyncKeyState(VirtualKeyLeftButton) & 0x8000) != 0;
            if (isPressed)
            {
                observedPressed = true;
                releasedPollCount = 0;
            }
            else if (observedPressed || ++releasedPollCount >= 4)
            {
                _ = PostMessageW(_sourceWindow, WindowMessageLeftButtonUp, IntPtr.Zero, IntPtr.Zero);
                AppendDiagnostic(
                    observedPressed
                        ? "Pointer release monitor posted WM_LBUTTONUP after the physical button was released."
                        : "Pointer release monitor posted WM_LBUTTONUP because the drag started after physical release.");
                return;
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }
    }

    private void AppendDiagnostic(string message)
    {
        if (_diagnosticPath is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(_diagnosticPath, string.Concat(message, Environment.NewLine));
        }
        catch
        {
        }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);
}
