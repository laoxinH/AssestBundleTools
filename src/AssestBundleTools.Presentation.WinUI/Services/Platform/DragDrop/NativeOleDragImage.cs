using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class NativeOleDragImage : IDisposable
{
    internal const int ImageWidth = 280;
    internal const int ImageHeight = 58;
    internal const int HotspotX = 18;
    internal const int HotspotY = 18;
    internal const uint TransparentColorKey = 0x00FF00FF;
    private const int DefaultGuiFont = 17;
    private const int TransparentBackgroundMode = 1;
    private const int PenStyleSolid = 0;
    private const uint ColorWindow = 5;
    private const uint ColorWindowText = 8;
    private const uint ColorHighlight = 13;
    private const uint ColorHighlightText = 14;
    private const uint DrawTextSingleLine = 0x00000020;
    private const uint DrawTextVerticalCenter = 0x00000004;
    private const uint DrawTextEndEllipsis = 0x00008000;
    private const uint DrawTextNoPrefix = 0x00000800;
    private IntPtr _bitmap;

    private NativeOleDragImage(IntPtr bitmap)
    {
        _bitmap = bitmap;
    }

    internal IntPtr BitmapHandle => _bitmap;

    public static NativeOleDragImage? TryInitialize(
        IDataObject dataObject,
        string displayName,
        int itemCount,
        string? diagnosticPath)
    {
        IntPtr feedbackBitmap;
        try
        {
            feedbackBitmap = CreateDragBitmap(displayName, itemCount);
        }
        catch (Exception exception)
        {
            AppendDiagnostic(diagnosticPath, $"Creating the drag feedback bitmap failed: {exception.Message}");
            return null;
        }

        IntPtr shellBitmap = IntPtr.Zero;
        object? helper = null;
        try
        {
            shellBitmap = CreateDragBitmap(displayName, itemCount);
            var dragImage = new ShellDragImage
            {
                Size = new NativeSize(ImageWidth, ImageHeight),
                Offset = new NativePoint(HotspotX, HotspotY),
                Bitmap = shellBitmap,
                ColorKey = TransparentColorKey
            };
            helper = new DragDropHelper();
            var hResult = ((IDragSourceHelper)helper).InitializeFromBitmap(ref dragImage, dataObject);
            if (hResult < 0)
            {
                AppendDiagnostic(diagnosticPath, $"IDragSourceHelper.InitializeFromBitmap failed: 0x{hResult:X8}.");
            }
            else
            {
                shellBitmap = IntPtr.Zero;
                AppendDiagnostic(
                    diagnosticPath,
                    $"Shell drag image initialized. DisplayName={displayName}; ItemCount={itemCount}.");
            }
        }
        catch (Exception exception)
        {
            AppendDiagnostic(diagnosticPath, $"Shell drag image initialization failed: {exception.Message}");
        }
        finally
        {
            if (helper is not null && Marshal.IsComObject(helper))
            {
                _ = Marshal.FinalReleaseComObject(helper);
            }

            if (shellBitmap != IntPtr.Zero)
            {
                _ = DeleteObject(shellBitmap);
            }
        }

        return new NativeOleDragImage(feedbackBitmap);
    }

    public void Dispose()
    {
        if (_bitmap == IntPtr.Zero)
        {
            return;
        }

        _ = DeleteObject(_bitmap);
        _bitmap = IntPtr.Zero;
    }

    private static IntPtr CreateDragBitmap(string displayName, int itemCount)
    {
        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("Acquiring the screen device context failed.");
        }

        IntPtr memoryDc = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr pixels = IntPtr.Zero;
        IntPtr previousBitmap = IntPtr.Zero;
        var succeeded = false;
        try
        {
            memoryDc = CreateCompatibleDC(screenDc);
            bitmap = CreateDragDib(screenDc, out pixels);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                throw new InvalidOperationException("Creating the shell drag bitmap failed.");
            }

            previousBitmap = SelectObject(memoryDc, bitmap);
            if (previousBitmap == IntPtr.Zero || previousBitmap == new IntPtr(-1))
            {
                throw new InvalidOperationException("Selecting the shell drag bitmap failed.");
            }

            DrawDragImage(memoryDc, displayName, itemCount);
            ApplyExplicitAlpha(pixels);
            succeeded = true;
            return bitmap;
        }
        finally
        {
            if (previousBitmap != IntPtr.Zero)
            {
                _ = SelectObject(memoryDc, previousBitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                _ = DeleteDC(memoryDc);
            }

            _ = ReleaseDC(IntPtr.Zero, screenDc);
            if (!succeeded && bitmap != IntPtr.Zero)
            {
                _ = DeleteObject(bitmap);
            }
        }
    }

    private static IntPtr CreateDragDib(IntPtr screenDc, out IntPtr pixels)
    {
        var bitmapInfo = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = checked((uint)Marshal.SizeOf<BitmapInfoHeader>()),
                Width = ImageWidth,
                Height = -ImageHeight,
                Planes = 1,
                BitCount = 32,
                Compression = 0,
                SizeImage = checked((uint)(ImageWidth * ImageHeight * sizeof(int)))
            }
        };
        return CreateDIBSection(
            screenDc,
            ref bitmapInfo,
            usage: 0,
            out pixels,
            IntPtr.Zero,
            offset: 0);
    }

    private static void ApplyExplicitAlpha(IntPtr pixels)
    {
        if (pixels == IntPtr.Zero)
        {
            throw new InvalidOperationException("The shell drag bitmap did not expose pixel memory.");
        }

        var pixelCount = ImageWidth * ImageHeight;
        for (var index = 0; index < pixelCount; index++)
        {
            var offset = index * sizeof(int);
            var color = unchecked((uint)Marshal.ReadInt32(pixels, offset));
            var alpha = (color & 0x00FFFFFF) == TransparentColorKey ? 0u : 0xFF000000u;
            Marshal.WriteInt32(pixels, offset, unchecked((int)((color & 0x00FFFFFF) | alpha)));
        }
    }

    private static void DrawDragImage(IntPtr dc, string displayName, int itemCount)
    {
        Fill(dc, new NativeRect(0, 0, ImageWidth, ImageHeight), TransparentColorKey);

        var backgroundBrush = CreateSolidBrush(GetSysColor(ColorWindow));
        var borderPen = CreatePen(PenStyleSolid, 1, GetSysColor(ColorHighlight));
        var previousBrush = SelectObject(dc, backgroundBrush);
        var previousPen = SelectObject(dc, borderPen);
        _ = RoundRect(dc, 1, 1, ImageWidth - 1, ImageHeight - 1, 12, 12);

        var iconBrush = CreateSolidBrush(GetSysColor(ColorHighlight));
        _ = SelectObject(dc, iconBrush);
        _ = RoundRect(dc, 12, 11, 48, 47, 7, 7);

        var fileBrush = CreateSolidBrush(GetSysColor(ColorHighlightText));
        _ = SelectObject(dc, fileBrush);
        _ = Rectangle(dc, 23, 18, 38, 41);
        _ = SelectObject(dc, previousBrush);
        _ = SelectObject(dc, previousPen);
        _ = DeleteObject(fileBrush);
        _ = DeleteObject(iconBrush);
        _ = DeleteObject(backgroundBrush);
        _ = DeleteObject(borderPen);

        var previousFont = SelectObject(dc, GetStockObject(DefaultGuiFont));
        _ = SetBkMode(dc, TransparentBackgroundMode);
        _ = SetTextColor(dc, GetSysColor(ColorWindowText));

        var countText = itemCount > 1
            ? itemCount > 999 ? "999+" : itemCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
        var countWidth = countText.Length > 0 ? 48 : 0;
        var nameRect = new NativeRect(60, 7, ImageWidth - 12 - countWidth, ImageHeight - 7);
        _ = DrawTextW(
            dc,
            displayName,
            displayName.Length,
            ref nameRect,
            DrawTextSingleLine | DrawTextVerticalCenter | DrawTextEndEllipsis | DrawTextNoPrefix);

        if (countText.Length > 0)
        {
            var badgeBrush = CreateSolidBrush(GetSysColor(ColorHighlight));
            var previousBadgeBrush = SelectObject(dc, badgeBrush);
            _ = RoundRect(dc, ImageWidth - 50, 14, ImageWidth - 10, 44, 14, 14);
            _ = SelectObject(dc, previousBadgeBrush);
            _ = DeleteObject(badgeBrush);
            _ = SetTextColor(dc, GetSysColor(ColorHighlightText));
            var countRect = new NativeRect(ImageWidth - 50, 14, ImageWidth - 10, 44);
            _ = DrawTextW(
                dc,
                countText,
                countText.Length,
                ref countRect,
                DrawTextSingleLine | DrawTextVerticalCenter | DrawTextNoPrefix | 0x00000001);
        }

        _ = SelectObject(dc, previousFont);
    }

    private static void Fill(IntPtr dc, NativeRect rect, uint color)
    {
        var brush = CreateSolidBrush(color);
        _ = FillRect(dc, ref rect, brush);
        _ = DeleteObject(brush);
    }

    private static void AppendDiagnostic(string? path, string message)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.AppendAllText(path, string.Concat(message, Environment.NewLine));
        }
        catch
        {
        }
    }

    [ComImport]
    [Guid("4657278A-411B-11D2-839A-00C04FD918D0")]
    private sealed class DragDropHelper
    {
    }

    [ComImport]
    [Guid("DE5BF786-477A-11D2-839D-00C04FD918D0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDragSourceHelper
    {
        [PreserveSig]
        int InitializeFromBitmap(
            ref ShellDragImage dragImage,
            [MarshalAs(UnmanagedType.Interface)] IDataObject dataObject);

        [PreserveSig]
        int InitializeFromWindow(
            IntPtr window,
            ref NativePoint point,
            [MarshalAs(UnmanagedType.Interface)] IDataObject dataObject);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ShellDragImage
    {
        public NativeSize Size;
        public NativePoint Offset;
        public IntPtr Bitmap;
        public uint ColorKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }

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
    private static extern IntPtr CreateSolidBrush(uint color);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreatePen(int style, int width, uint color);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RoundRect(
        IntPtr dc,
        int left,
        int top,
        int right,
        int bottom,
        int width,
        int height);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Rectangle(
        IntPtr dc,
        int left,
        int top,
        int right,
        int bottom);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr dc, ref NativeRect rect, IntPtr brush);

    [DllImport("user32.dll")]
    private static extern uint GetSysColor(uint index);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int index);

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
