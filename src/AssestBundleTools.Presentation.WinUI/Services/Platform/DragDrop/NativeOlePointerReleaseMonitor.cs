using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class NativeOlePointerReleaseMonitor : IDisposable
{
    private const uint WindowMessageMouseMove = 0x0200;
    private const uint WindowMessageLeftButtonUp = 0x0202;
    private const int PollIntervalMilliseconds = 8;
    private const int TargetSettleMilliseconds = 75;
    private readonly IntPtr _sourceWindow;
    private readonly uint _ownerThreadId;
    private readonly string? _diagnosticPath;
    private readonly NativeOleDragReleaseGate _releaseGate;
    private readonly NativeOleDragFeedbackWindow? _feedbackWindow;
    private readonly Func<NativePointerReleaseSignal?> _pointerReleaseProvider;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Thread _thread;

    private NativeOlePointerReleaseMonitor(
        IntPtr sourceWindow,
        NativeOleDragReleaseGate releaseGate,
        NativeOleDragFeedbackWindow? feedbackWindow,
        Func<NativePointerReleaseSignal?> pointerReleaseProvider,
        string? diagnosticPath)
    {
        _sourceWindow = sourceWindow;
        _ownerThreadId = GetCurrentThreadId();
        _releaseGate = releaseGate;
        _feedbackWindow = feedbackWindow;
        _pointerReleaseProvider = pointerReleaseProvider ??
            throw new ArgumentNullException(nameof(pointerReleaseProvider));
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
        NativeOleDragReleaseGate releaseGate,
        NativeOleDragFeedbackWindow? feedbackWindow,
        Func<NativePointerReleaseSignal?> pointerReleaseProvider,
        string? diagnosticPath) =>
        new(sourceWindow, releaseGate, feedbackWindow, pointerReleaseProvider, diagnosticPath);

    public void Dispose()
    {
        _cancellation.Cancel();
        _ = _thread.Join(millisecondsTimeout: 100);
        _cancellation.Dispose();
    }

    private void MonitorPointerRelease()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            _feedbackWindow?.UpdatePosition();
            NativePointerReleaseSignal? signal;
            try
            {
                signal = _pointerReleaseProvider();
            }
            catch (Exception exception)
            {
                CancelDrag($"Pointer release relay failed: {exception.GetType().Name}.");
                return;
            }

            if (signal is { DropRequested: true } dropSignal)
            {
                PostPointerRelease(dropSignal);
                return;
            }

            if (signal is { DropRequested: false })
            {
                CancelDrag("Pointer release relay requested cancellation.");
                return;
            }

            Thread.Sleep(PollIntervalMilliseconds);
        }
    }

    private void PostPointerRelease(NativePointerReleaseSignal signal)
    {
        _feedbackWindow?.Hide();
        var messageWindow = ResolveMessageWindow();
        var screenPoint = new NativePoint { X = signal.ScreenX, Y = signal.ScreenY };
        var clientPoint = screenPoint;
        _ = ScreenToClient(messageWindow, ref clientPoint);

        var packedPosition = unchecked(
            (clientPoint.X & 0xFFFF) |
            ((clientPoint.Y & 0xFFFF) << 16));
        _ = PostMessageW(
            messageWindow,
            WindowMessageMouseMove,
            IntPtr.Zero,
            new IntPtr(packedPosition));
        if (_cancellation.Token.WaitHandle.WaitOne(TargetSettleMilliseconds))
        {
            return;
        }

        _releaseGate.AllowDrop();
        _ = PostMessageW(
            messageWindow,
            WindowMessageLeftButtonUp,
            IntPtr.Zero,
            new IntPtr(packedPosition));
        AppendDiagnostic(
            $"Pointer release monitor posted WM_LBUTTONUP after the elevated process relayed release. " +
            $"Window=0x{messageWindow.ToInt64():X}; Screen=({screenPoint.X},{screenPoint.Y}); " +
            $"Client=({clientPoint.X},{clientPoint.Y}).");
    }

    private void CancelDrag(string diagnostic)
    {
        _feedbackWindow?.Hide();
        _releaseGate.Cancel();
        var messageWindow = ResolveMessageWindow();
        _ = PostMessageW(messageWindow, WindowMessageMouseMove, IntPtr.Zero, IntPtr.Zero);
        AppendDiagnostic($"{diagnostic} Window=0x{messageWindow.ToInt64():X}.");
    }

    private IntPtr ResolveMessageWindow()
    {
        var threadInfo = new GuiThreadInfo
        {
            Size = checked((uint)Marshal.SizeOf<GuiThreadInfo>())
        };
        return GetGUIThreadInfo(_ownerThreadId, ref threadInfo) &&
               threadInfo.CaptureWindow != IntPtr.Zero
            ? threadInfo.CaptureWindow
            : _sourceWindow;
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

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo threadInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr window, ref NativePoint point);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public uint Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public NativeRect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

internal sealed class NativeOleDragReleaseGate
{
    private const int ContinueDecision = 0;
    private const int DropDecision = 1;
    private const int CancelDecision = 2;
    private int _decision;

    public bool IsDropAllowed => Volatile.Read(ref _decision) == DropDecision;

    public bool IsCancelRequested => Volatile.Read(ref _decision) == CancelDecision;

    public void AllowDrop() =>
        _ = Interlocked.CompareExchange(ref _decision, DropDecision, ContinueDecision);

    public void Cancel() =>
        _ = Interlocked.CompareExchange(ref _decision, CancelDecision, ContinueDecision);
}
