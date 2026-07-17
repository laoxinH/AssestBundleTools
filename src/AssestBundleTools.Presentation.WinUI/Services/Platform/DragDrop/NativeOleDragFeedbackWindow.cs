using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class NativeOleDragFeedbackWindow : IDisposable
{
    private const uint ExtendedStyleTopmost = 0x00000008;
    private const uint ExtendedStyleTransparent = 0x00000020;
    private const uint ExtendedStyleToolWindow = 0x00000080;
    private const uint ExtendedStyleLayered = 0x00080000;
    private const uint ExtendedStyleNoActivate = 0x08000000;
    private const uint WindowStylePopup = 0x80000000;
    private const uint SetWindowPositionNoActivate = 0x0010;
    private const uint SetWindowPositionShowWindow = 0x0040;
    private const uint UpdateLayeredWindowUseAlpha = 0x00000002;
    private const byte AlphaBlendSourceOver = 0;
    private const byte AlphaBlendSourceAlpha = 1;
    private const int ShowWindowWithoutActivation = 8;
    private const int CursorGap = 22;
    private IntPtr _window;
    private IntPtr _memoryDc;
    private IntPtr _previousBitmap;

    private NativeOleDragFeedbackWindow(
        IntPtr window,
        IntPtr memoryDc,
        IntPtr previousBitmap)
    {
        _window = window;
        _memoryDc = memoryDc;
        _previousBitmap = previousBitmap;
    }

    public static NativeOleDragFeedbackWindow? TryCreate(
        IntPtr bitmap,
        string? diagnosticPath)
    {
        if (bitmap == IntPtr.Zero)
        {
            return null;
        }

        var window = CreateWindowExW(
            ExtendedStyleTopmost |
            ExtendedStyleTransparent |
            ExtendedStyleToolWindow |
            ExtendedStyleLayered |
            ExtendedStyleNoActivate,
            "STATIC",
            string.Empty,
            WindowStylePopup,
            0,
            0,
            NativeOleDragImage.ImageWidth,
            NativeOleDragImage.ImageHeight,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            AppendDiagnostic(diagnosticPath, "Creating the native drag feedback window failed.");
            return null;
        }

        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = screenDc == IntPtr.Zero ? IntPtr.Zero : CreateCompatibleDC(screenDc);
        var previousBitmap = memoryDc == IntPtr.Zero ? IntPtr.Zero : SelectObject(memoryDc, bitmap);
        if (screenDc == IntPtr.Zero ||
            memoryDc == IntPtr.Zero ||
            previousBitmap == IntPtr.Zero ||
            previousBitmap == new IntPtr(-1))
        {
            if (memoryDc != IntPtr.Zero)
            {
                _ = DeleteDC(memoryDc);
            }

            if (screenDc != IntPtr.Zero)
            {
                _ = ReleaseDC(IntPtr.Zero, screenDc);
            }

            _ = DestroyWindow(window);
            AppendDiagnostic(diagnosticPath, "Preparing the native drag feedback bitmap failed.");
            return null;
        }

        var position = CalculatePosition();
        var size = new NativeSize(NativeOleDragImage.ImageWidth, NativeOleDragImage.ImageHeight);
        var source = new NativePoint();
        var blend = new BlendFunction
        {
            BlendOperation = AlphaBlendSourceOver,
            SourceConstantAlpha = byte.MaxValue,
            AlphaFormat = AlphaBlendSourceAlpha
        };
        var updated = UpdateLayeredWindow(
            window,
            screenDc,
            ref position,
            ref size,
            memoryDc,
            ref source,
            0,
            ref blend,
            UpdateLayeredWindowUseAlpha);
        _ = ReleaseDC(IntPtr.Zero, screenDc);
        if (!updated)
        {
            _ = SelectObject(memoryDc, previousBitmap);
            _ = DeleteDC(memoryDc);
            _ = DestroyWindow(window);
            AppendDiagnostic(diagnosticPath, "Rendering the native drag feedback window failed.");
            return null;
        }

        _ = ShowWindow(window, ShowWindowWithoutActivation);
        var feedback = new NativeOleDragFeedbackWindow(window, memoryDc, previousBitmap);
        AppendDiagnostic(diagnosticPath, $"Native drag feedback window created. Window=0x{window.ToInt64():X}.");
        return feedback;
    }

    public void UpdatePosition()
    {
        if (_window == IntPtr.Zero)
        {
            return;
        }

        var position = CalculatePosition();
        _ = SetWindowPos(
            _window,
            new IntPtr(-1),
            position.X,
            position.Y,
            NativeOleDragImage.ImageWidth,
            NativeOleDragImage.ImageHeight,
            SetWindowPositionNoActivate | SetWindowPositionShowWindow);
    }

    public void Hide()
    {
        if (_window != IntPtr.Zero)
        {
            _ = ShowWindow(_window, 0);
        }
    }

    public void Dispose()
    {
        if (_window != IntPtr.Zero)
        {
            _ = DestroyWindow(_window);
            _window = IntPtr.Zero;
        }

        if (_memoryDc != IntPtr.Zero)
        {
            if (_previousBitmap != IntPtr.Zero)
            {
                _ = SelectObject(_memoryDc, _previousBitmap);
                _previousBitmap = IntPtr.Zero;
            }

            _ = DeleteDC(_memoryDc);
            _memoryDc = IntPtr.Zero;
        }
    }

    private static NativePoint CalculatePosition()
    {
        _ = GetCursorPos(out var cursor);
        var virtualLeft = GetSystemMetrics(76);
        var virtualTop = GetSystemMetrics(77);
        var virtualRight = virtualLeft + GetSystemMetrics(78);
        var virtualBottom = virtualTop + GetSystemMetrics(79);
        var left = cursor.X + CursorGap;
        var top = cursor.Y + CursorGap;
        if (left + NativeOleDragImage.ImageWidth > virtualRight)
        {
            left = cursor.X - CursorGap - NativeOleDragImage.ImageWidth;
        }

        if (top + NativeOleDragImage.ImageHeight > virtualBottom)
        {
            top = cursor.Y - CursorGap - NativeOleDragImage.ImageHeight;
        }

        return new NativePoint(left, top);
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
    private static extern bool UpdateLayeredWindow(
        IntPtr window,
        IntPtr destinationDc,
        ref NativePoint destination,
        ref NativeSize size,
        IntPtr sourceDc,
        ref NativePoint source,
        uint colorKey,
        ref BlendFunction blend,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);

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

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOperation;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }
}
