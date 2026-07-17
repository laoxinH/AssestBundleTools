using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal static class NativeOleFileDrag
{
    private const uint DropEffectCopy = 1;
    private const uint ExtendedStyleToolWindow = 0x00000080;
    private const uint WindowStylePopup = 0x80000000;

    public static NativeOleDragResult Run(
        Func<IReadOnlyList<string>> pathsProvider,
        Func<NativePointerReleaseSignal?> pointerReleaseProvider,
        string displayName,
        int itemCount,
        string? diagnosticPath = null)
    {
        using var dataObject = new NativeOleFileDropDataObject(pathsProvider, diagnosticPath);
        var releaseGate = new NativeOleDragReleaseGate();
        var dropSource = new OleDropSource(releaseGate, diagnosticPath);
        _ = GetCursorPos(out var cursorPosition);
        var sourceWindow = CreateWindowExW(
            ExtendedStyleToolWindow,
            "STATIC",
            string.Empty,
            WindowStylePopup,
            cursorPosition.X,
            cursorPosition.Y,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        if (sourceWindow == IntPtr.Zero)
        {
            return new NativeOleDragResult(Marshal.GetHRForLastWin32Error(), 0);
        }

        try
        {
            using var dragImage = NativeOleDragImage.TryInitialize(
                dataObject,
                displayName,
                itemCount,
                diagnosticPath);
            using var feedbackWindow = NativeOleDragFeedbackWindow.TryCreate(
                dragImage?.BitmapHandle ?? IntPtr.Zero,
                diagnosticPath);
            using var releaseMonitor = NativeOlePointerReleaseMonitor.Start(
                sourceWindow,
                releaseGate,
                feedbackWindow,
                pointerReleaseProvider,
                diagnosticPath);
            var hResult = DoDragDrop(dataObject, dropSource, DropEffectCopy, out var effect);
            return new NativeOleDragResult(hResult, effect);
        }
        finally
        {
            _ = DestroyWindow(sourceWindow);
        }
    }

    [DllImport("ole32.dll")]
    internal static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll")]
    internal static extern void OleUninitialize();

    [DllImport("ole32.dll")]
    private static extern int DoDragDrop(
        [MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IDataObject dataObject,
        [MarshalAs(UnmanagedType.Interface)] IOleDropSource dropSource,
        uint allowedEffects,
        out uint effect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parentWindow,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [ComVisible(true)]
    [Guid("00000121-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleDropSource
    {
        [PreserveSig]
        int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escapePressed, uint keyState);

        [PreserveSig]
        int GiveFeedback(uint effect);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class OleDropSource : IOleDropSource
    {
        private const int DragDropCancel = 0x00040101;
        private const int DragDropDrop = 0x00040100;
        private const int UseDefaultCursors = 0x00040102;
        private readonly string? _diagnosticPath;
        private readonly NativeOleDragReleaseGate _releaseGate;
        private bool _queryLogged;
        private bool _feedbackLogged;

        public OleDropSource(
            NativeOleDragReleaseGate releaseGate,
            string? diagnosticPath)
        {
            _releaseGate = releaseGate;
            _diagnosticPath = diagnosticPath;
        }

        public int QueryContinueDrag(bool escapePressed, uint keyState)
        {
            if (!_queryLogged)
            {
                AppendDiagnostic($"QueryContinueDrag: Escape={escapePressed}; KeyState=0x{keyState:X8}.");
                _queryLogged = true;
            }

            if (escapePressed)
            {
                return DragDropCancel;
            }

            if (_releaseGate.IsCancelRequested)
            {
                return DragDropCancel;
            }

            return _releaseGate.IsDropAllowed ? DragDropDrop : 0;
        }

        public int GiveFeedback(uint effect)
        {
            if (!_feedbackLogged)
            {
                AppendDiagnostic($"GiveFeedback: Effect={effect}.");
                _feedbackLogged = true;
            }

            return UseDefaultCursors;
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
    }
}
