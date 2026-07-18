using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class NativeOleDragImage : IDisposable
{
    internal const int ImageWidth = 320;
    internal const int ImageHeight = 72;
    internal const int HotspotX = 20;
    internal const int HotspotY = 20;
    internal const uint TransparentColorKey = 0x00FF00FF;
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
        var theme = NativeOleDragVisualThemeResolver.Resolve();
        IntPtr feedbackBitmap;
        try
        {
            feedbackBitmap = NativeOleDragBitmapRenderer.Create(
                displayName,
                itemCount,
                theme,
                premultiplyAlpha: true);
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
            shellBitmap = NativeOleDragBitmapRenderer.Create(
                displayName,
                itemCount,
                theme,
                premultiplyAlpha: false);
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
                    $"Shell drag image initialized. DisplayName={displayName}; ItemCount={itemCount}; " +
                    $"Theme={theme.Name}; Size={ImageWidth}x{ImageHeight}.");
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

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);

}
