using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class WindowsElevatedDragDropCompatibilityService : IElevatedDragDropCompatibilityService
{
    private const uint MessageFilterAllow = 1;
    private static readonly MessageDescriptor[] AllowedMessages =
    [
        new("WM_DROPFILES", 0x0233),
        new("WM_COPYDATA", 0x004A),
        new("WM_COPYGLOBALDATA", 0x0049)
    ];

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

            DragAcceptFiles(windowHandle, true);
            var appliedMessages = string.Join(",", AllowedMessages.Select(static item => item.Name));
            return failures.Count == 0
                ? new ElevatedDragDropCompatibilityResult(
                    true,
                    $"AllowedMessages={appliedMessages}; DragAcceptFiles=True.")
                : new ElevatedDragDropCompatibilityResult(
                    false,
                    $"AllowedMessages={appliedMessages}; DragAcceptFiles=True; Failures={string.Join(",", failures)}.");
        }
        catch (Exception ex)
        {
            return new ElevatedDragDropCompatibilityResult(
                false,
                $"Enabling elevated drag-drop compatibility failed: {ex.Message}");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ChangeWindowMessageFilterEx(
        IntPtr windowHandle,
        uint message,
        uint action,
        ref ChangeFilterStruct changeFilter);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(
        IntPtr windowHandle,
        [MarshalAs(UnmanagedType.Bool)] bool accept);

    [StructLayout(LayoutKind.Sequential)]
    private struct ChangeFilterStruct
    {
        public uint Size;
        public uint ExtendedStatus;
    }

    private readonly record struct MessageDescriptor(string Name, uint MessageId);
}
