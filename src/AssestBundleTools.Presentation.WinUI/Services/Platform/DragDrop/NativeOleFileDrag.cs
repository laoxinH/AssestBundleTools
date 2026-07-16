using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal static class NativeOleFileDrag
{
    private const uint DropEffectCopy = 1;
    private const uint ExtendedStyleToolWindow = 0x00000080;
    private const uint WindowStylePopup = 0x80000000;
    private const uint WindowStyleVisible = 0x10000000;

    public static NativeOleDragResult Run(
        Func<IReadOnlyList<string>> pathsProvider,
        string? diagnosticPath = null)
    {
        var dataObject = new FileDropDataObject(pathsProvider);
        var dropSource = new OleDropSource(diagnosticPath);
        _ = GetCursorPos(out var cursorPosition);
        var sourceWindow = CreateWindowExW(
            ExtendedStyleToolWindow,
            "STATIC",
            string.Empty,
            WindowStylePopup | WindowStyleVisible,
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
            _ = SetForegroundWindow(sourceWindow);
            _ = SetCapture(sourceWindow);
            var hResult = DoDragDrop(dataObject, dropSource, DropEffectCopy, out var effect);
            return new NativeOleDragResult(hResult, effect);
        }
        finally
        {
            _ = ReleaseCapture();
            _ = DestroyWindow(sourceWindow);
        }
    }

    [DllImport("ole32.dll")]
    internal static extern int OleInitialize(IntPtr reserved);

    [DllImport("ole32.dll")]
    internal static extern void OleUninitialize();

    [DllImport("ole32.dll")]
    private static extern int DoDragDrop(
        [MarshalAs(UnmanagedType.Interface)] IDataObject dataObject,
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
    private static extern IntPtr SetCapture(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

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
        private const uint MouseKeyLeftButton = 0x0001;
        private const int DragDropCancel = 0x00040101;
        private const int DragDropDrop = 0x00040100;
        private const int UseDefaultCursors = 0x00040102;
        private readonly string? _diagnosticPath;
        private bool _queryLogged;
        private bool _feedbackLogged;

        public OleDropSource(string? diagnosticPath)
        {
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

            return (keyState & MouseKeyLeftButton) == 0 ? DragDropDrop : 0;
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

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class FileDropDataObject : IDataObject
    {
        private const short ClipboardFormatFileDrop = 15;
        private const int DataFormatError = unchecked((int)0x80040064);
        private const int NotImplemented = unchecked((int)0x80004001);
        private const int AdviseNotSupported = unchecked((int)0x80040003);
        private const int SameFormat = 0x00040130;
        private readonly Func<IReadOnlyList<string>> _pathsProvider;
        private IReadOnlyList<string>? _paths;

        public FileDropDataObject(Func<IReadOnlyList<string>> pathsProvider)
        {
            _pathsProvider = pathsProvider ?? throw new ArgumentNullException(nameof(pathsProvider));
        }

        public void GetData(ref FORMATETC format, out STGMEDIUM medium)
        {
            if (QueryGetData(ref format) != 0)
            {
                throw new COMException("The requested drag format is unavailable.", DataFormatError);
            }

            _paths ??= _pathsProvider();
            medium = new STGMEDIUM
            {
                tymed = TYMED.TYMED_HGLOBAL,
                unionmember = CreateFileDropHandle(_paths),
                pUnkForRelease = null
            };
        }

        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) =>
            throw new COMException("GetDataHere is not supported.", NotImplemented);

        public int QueryGetData(ref FORMATETC format) =>
            format.cfFormat == ClipboardFormatFileDrop &&
            format.dwAspect == DVASPECT.DVASPECT_CONTENT &&
            (format.tymed & TYMED.TYMED_HGLOBAL) != 0
                ? 0
                : DataFormatError;

        public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
        {
            formatOut = formatIn;
            formatOut.ptd = IntPtr.Zero;
            return SameFormat;
        }

        public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release) =>
            throw new COMException("SetData is not supported.", NotImplemented);

        public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
        {
            if (direction != DATADIR.DATADIR_GET)
            {
                throw new COMException("Only data retrieval formats are supported.", NotImplemented);
            }

            return new FileDropFormatEnumerator(CreateFileDropFormat());
        }

        public int DAdvise(ref FORMATETC format, ADVF advf, IAdviseSink adviseSink, out int connection)
        {
            connection = 0;
            return AdviseNotSupported;
        }

        public void DUnadvise(int connection) =>
            throw new COMException("Data advise is not supported.", AdviseNotSupported);

        public int EnumDAdvise(out IEnumSTATDATA? enumAdvise)
        {
            enumAdvise = null;
            return AdviseNotSupported;
        }

        private static FORMATETC CreateFileDropFormat() => new()
        {
            cfFormat = ClipboardFormatFileDrop,
            ptd = IntPtr.Zero,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL
        };

        private static IntPtr CreateFileDropHandle(IReadOnlyList<string> paths)
        {
            var pathBytes = Encoding.Unicode.GetBytes(string.Concat(string.Join('\0', paths), "\0\0"));
            var headerSize = Marshal.SizeOf<DropFiles>();
            var handle = GlobalAlloc(0x0042, checked((UIntPtr)(headerSize + pathBytes.Length)));
            if (handle == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Allocating the CF_HDROP payload failed.");
            }

            var memory = GlobalLock(handle);
            if (memory == IntPtr.Zero)
            {
                _ = GlobalFree(handle);
                throw new OutOfMemoryException("Locking the CF_HDROP payload failed.");
            }

            try
            {
                Marshal.StructureToPtr(
                    new DropFiles { FileListOffset = checked((uint)headerSize), IsWide = 1 },
                    memory,
                    fDeleteOld: false);
                Marshal.Copy(pathBytes, 0, IntPtr.Add(memory, headerSize), pathBytes.Length);
            }
            finally
            {
                _ = GlobalUnlock(handle);
            }

            return handle;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr memory);

        [StructLayout(LayoutKind.Sequential)]
        private struct DropFiles
        {
            public uint FileListOffset;
            public int X;
            public int Y;
            public int IsNonClient;
            public int IsWide;
        }
    }

    private sealed class FileDropFormatEnumerator : IEnumFORMATETC
    {
        private readonly FORMATETC _format;
        private bool _read;

        public FileDropFormatEnumerator(FORMATETC format)
        {
            _format = format;
        }

        public int Next(int count, FORMATETC[] formats, int[]? fetched)
        {
            if (count <= 0 || formats.Length == 0 || _read)
            {
                if (fetched is { Length: > 0 })
                {
                    fetched[0] = 0;
                }

                return 1;
            }

            formats[0] = _format;
            _read = true;
            if (fetched is { Length: > 0 })
            {
                fetched[0] = 1;
            }

            return count == 1 ? 0 : 1;
        }

        public int Skip(int count)
        {
            if (count <= 0 || _read)
            {
                return 1;
            }

            _read = true;
            return count == 1 ? 0 : 1;
        }

        public int Reset()
        {
            _read = false;
            return 0;
        }

        public void Clone(out IEnumFORMATETC newEnum)
        {
            var clone = new FileDropFormatEnumerator(_format) { _read = _read };
            newEnum = clone;
        }
    }
}
