using System.Runtime.InteropServices;
using System.Text;
using AssestBundleTools.Presentation.WinUI.Diagnostics;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class WindowsElevatedDragDropCompatibilityService : IElevatedDragDropCompatibilityService
{
    private const uint MessageFilterAllow = 1;
    private const uint WmDropFiles = 0x0233;
    private const uint WmNcDestroy = 0x0082;
    private const uint QueryAllDropFiles = 0xFFFFFFFF;
    private static readonly UIntPtr WindowSubclassId = new(0x41425444);
    private static readonly MessageDescriptor[] AllowedMessages =
    [
        new("WM_DROPFILES", WmDropFiles),
        new("WM_COPYDATA", 0x004A),
        new("WM_COPYGLOBALDATA", 0x0049)
    ];
    private readonly Dictionary<IntPtr, Window> _subclassedWindows = [];
    private readonly WindowSubclassProc _windowSubclassProc;

    public WindowsElevatedDragDropCompatibilityService()
    {
        _windowSubclassProc = HandleWindowMessage;
    }

    public event EventHandler<ElevatedFileDropReceivedEventArgs>? FilesDropped;

    public ElevatedDragDropCompatibilityResult TryEnable(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        try
        {
            var windowHandle = WindowNative.GetWindowHandle(window);
            if (windowHandle == IntPtr.Zero)
            {
                return new ElevatedDragDropCompatibilityResult(
                    false,
                    "The main window handle is unavailable.");
            }

            var failures = new List<string>();
            foreach (var descriptor in AllowedMessages)
            {
                var changeFilter = new ChangeFilterStruct
                {
                    Size = checked((uint)Marshal.SizeOf<ChangeFilterStruct>())
                };
                if (!ChangeWindowMessageFilterEx(
                        windowHandle,
                        descriptor.MessageId,
                        MessageFilterAllow,
                        ref changeFilter))
                {
                    failures.Add(
                        $"{descriptor.Name}=Win32Error:{Marshal.GetLastWin32Error()}");
                }
            }

            if (!_subclassedWindows.ContainsKey(windowHandle))
            {
                if (!SetWindowSubclass(
                        windowHandle,
                        _windowSubclassProc,
                        WindowSubclassId,
                        UIntPtr.Zero))
                {
                    DragAcceptFiles(windowHandle, false);
                    failures.Add($"SetWindowSubclass=Win32Error:{Marshal.GetLastWin32Error()}");
                }
                else
                {
                    _subclassedWindows.Add(windowHandle, window);
                }
            }

            var subclassInstalled = _subclassedWindows.ContainsKey(windowHandle);
            if (subclassInstalled)
            {
                DragAcceptFiles(windowHandle, true);
            }

            var appliedMessages = string.Join(",", AllowedMessages.Select(static item => item.Name));
            return failures.Count == 0
                ? new ElevatedDragDropCompatibilityResult(
                    true,
                    $"AllowedMessages={appliedMessages}; DragAcceptFiles=True; WindowSubclass=True.")
                : new ElevatedDragDropCompatibilityResult(
                    false,
                    $"AllowedMessages={appliedMessages}; DragAcceptFiles={subclassInstalled}; " +
                    $"WindowSubclass={subclassInstalled}; Failures={string.Join(",", failures)}.");
        }
        catch (Exception ex)
        {
            return new ElevatedDragDropCompatibilityResult(
                false,
                $"Enabling elevated drag-drop compatibility failed: {ex.Message}");
        }
    }

    private IntPtr HandleWindowMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData)
    {
        if (message == WmDropFiles)
        {
            HandleDropFiles(windowHandle, wParam);
            return IntPtr.Zero;
        }

        if (message == WmNcDestroy)
        {
            RemoveWindowSubclass(windowHandle, _windowSubclassProc, WindowSubclassId);
            _subclassedWindows.Remove(windowHandle);
        }

        return DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private void HandleDropFiles(IntPtr windowHandle, IntPtr dropHandle)
    {
        try
        {
            var paths = ReadDropPaths(dropHandle);
            var isClientArea = DragQueryPoint(dropHandle, out var point);
            if (!_subclassedWindows.TryGetValue(windowHandle, out var sourceWindow))
            {
                StartupDiagnostics.Log(
                    $"WM_DROPFILES ignored because the source window was not registered. Hwnd=0x{windowHandle:X}.");
                return;
            }

            var screenPoint = point;
            var hasScreenPosition = ClientToScreen(windowHandle, ref screenPoint);
            var hasClientBounds = GetClientRect(windowHandle, out var clientBounds);
            var clientWidth = hasClientBounds
                ? Math.Max(0, clientBounds.Right - clientBounds.Left)
                : 0;
            var clientHeight = hasClientBounds
                ? Math.Max(0, clientBounds.Bottom - clientBounds.Top)
                : 0;

            FilesDropped?.Invoke(
                this,
                new ElevatedFileDropReceivedEventArgs(
                    sourceWindow,
                    paths,
                    new ElevatedNativeDropPosition(
                        point.X,
                        point.Y,
                        isClientArea,
                        clientWidth,
                        clientHeight,
                        screenPoint.X,
                        screenPoint.Y,
                        hasScreenPosition)));
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"WM_DROPFILES processing failed: {ex}");
        }
        finally
        {
            DragFinish(dropHandle);
        }
    }

    private static IReadOnlyList<string> ReadDropPaths(IntPtr dropHandle)
    {
        var fileCount = DragQueryFile(dropHandle, QueryAllDropFiles, null, 0);
        var paths = new List<string>(checked((int)fileCount));
        for (uint index = 0; index < fileCount; index++)
        {
            var characterCount = DragQueryFile(dropHandle, index, null, 0);
            if (characterCount == 0)
            {
                continue;
            }

            var buffer = new StringBuilder(checked((int)characterCount + 1));
            if (DragQueryFile(dropHandle, index, buffer, checked((uint)buffer.Capacity)) == 0)
            {
                continue;
            }

            var path = buffer.ToString();
            if (!string.IsNullOrWhiteSpace(path) &&
                !paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeWindowMessageFilterEx(
        IntPtr windowHandle,
        uint message,
        uint action,
        ref ChangeFilterStruct changeFilter);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr windowHandle,
        WindowSubclassProc subclassProc,
        UIntPtr subclassId,
        UIntPtr referenceData);

    [DllImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr windowHandle,
        WindowSubclassProc subclassProc,
        UIntPtr subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(
        IntPtr windowHandle,
        [MarshalAs(UnmanagedType.Bool)] bool accept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(
        IntPtr dropHandle,
        uint fileIndex,
        StringBuilder? fileName,
        uint characterCount);

    [DllImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DragQueryPoint(IntPtr dropHandle, out NativePoint point);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr dropHandle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr windowHandle, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr windowHandle, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct ChangeFilterStruct
    {
        public uint Size;
        public uint ExtendedStatus;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowSubclassProc(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData);

    private readonly record struct MessageDescriptor(string Name, uint MessageId);
}
