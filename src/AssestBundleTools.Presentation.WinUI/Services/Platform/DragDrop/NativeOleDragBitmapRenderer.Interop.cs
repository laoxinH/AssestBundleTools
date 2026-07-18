using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal static partial class NativeOleDragBitmapRenderer
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public NativeRect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPixelsPerMeter;
        public int YPixelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(
        IntPtr dc,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePen(int style, int width, uint color);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveToEx(IntPtr dc, int x, int y, IntPtr previousPoint);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LineTo(IntPtr dc, int x, int y);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        uint italic,
        uint underline,
        uint strikeOut,
        uint characterSet,
        uint outputPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string faceName);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr dc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr dc, uint color);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawTextW(
        IntPtr dc,
        string text,
        int characterCount,
        ref NativeRect rect,
        uint format);
}
