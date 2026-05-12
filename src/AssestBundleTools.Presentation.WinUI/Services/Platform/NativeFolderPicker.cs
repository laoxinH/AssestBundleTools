using System.IO;
using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 使用桌面 Common Item Dialog 选择文件夹，并默认把当前文件夹路径填入“文件夹”输入框。
/// WinRT FolderPicker 不会选中当前目录，导致用户必须手动点选子目录或输入名称。
/// </summary>
internal static class NativeFolderPicker
{
    private const int S_OK = 0;
    private const int ErrorCancelled = unchecked((int)0x800704C7);
    private static readonly Guid FileOpenDialogClassId = new("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");

    public static string? PickSingleFolder(IntPtr ownerWindowHandle)
    {
        var dialog = (IFileDialog)Activator.CreateInstance(
            Type.GetTypeFromCLSID(FileOpenDialogClassId, throwOnError: true)!)!;
        var events = new CurrentFolderSelectionEvents();
        uint cookie = 0;

        try
        {
            if (dialog.GetOptions(out var options) == S_OK)
            {
                dialog.SetOptions(
                    options |
                    FileOpenOptions.PickFolders |
                    FileOpenOptions.ForceFileSystem |
                    FileOpenOptions.PathMustExist |
                    FileOpenOptions.NoChangeDirectory);
            }

            dialog.Advise(events, out cookie);
            var showResult = dialog.Show(ownerWindowHandle);
            if (showResult == ErrorCancelled)
            {
                return null;
            }

            if (showResult != S_OK)
            {
                Marshal.ThrowExceptionForHR(showResult);
            }

            if (!string.IsNullOrWhiteSpace(events.SelectedCurrentFolderPath))
            {
                return events.SelectedCurrentFolderPath;
            }

            if (dialog.GetResult(out var resultItem) == S_OK &&
                TryGetShellItemPath(resultItem, out var resultPath))
            {
                return resultPath;
            }

            return events.CurrentFolderPath;
        }
        finally
        {
            if (cookie != 0)
            {
                dialog.Unadvise(cookie);
            }

            if (dialog is not null)
            {
                Marshal.ReleaseComObject(dialog);
            }
        }
    }

    internal static string ResolveDefaultSelectionPathForFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        return Path.TrimEndingDirectorySeparator(folderPath.Trim());
    }

    private static bool TryGetShellItemPath(IShellItem? item, out string path)
    {
        path = string.Empty;
        if (item is null ||
            item.GetDisplayName(ShellItemDisplayName.FileSystemPath, out var pathPointer) != S_OK ||
            pathPointer == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            path = Marshal.PtrToStringUni(pathPointer) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(path);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pathPointer);
        }
    }

    private static bool TryGetCurrentFolderPath(IFileDialog dialog, out string folderPath)
    {
        folderPath = string.Empty;
        return dialog.GetFolder(out var folderItem) == S_OK &&
               TryGetShellItemPath(folderItem, out folderPath);
    }

    private static bool TryGetFileName(IFileDialog dialog, out string fileName)
    {
        fileName = string.Empty;
        if (dialog.GetFileName(out var fileNamePointer) != S_OK ||
            fileNamePointer == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            fileName = Marshal.PtrToStringUni(fileNamePointer) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(fileName);
        }
        finally
        {
            Marshal.FreeCoTaskMem(fileNamePointer);
        }
    }

    [ComVisible(true)]
    private sealed class CurrentFolderSelectionEvents : IFileDialogEvents
    {
        public string? CurrentFolderPath { get; private set; }

        public string? SelectedCurrentFolderPath { get; private set; }

        public int OnFileOk(IFileDialog dialog)
        {
            if (TryGetCurrentFolderPath(dialog, out var folderPath) &&
                TryGetFileName(dialog, out var fileName) &&
                string.Equals(
                    fileName,
                    ResolveDefaultSelectionPathForFolderPath(folderPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                SelectedCurrentFolderPath = folderPath;
            }

            return S_OK;
        }

        public int OnFolderChanging(IFileDialog dialog, IShellItem folder) => S_OK;

        public int OnFolderChange(IFileDialog dialog)
        {
            ApplyCurrentFolderFileName(dialog);
            return S_OK;
        }

        public int OnSelectionChange(IFileDialog dialog) => S_OK;

        public int OnShareViolation(IFileDialog dialog, IShellItem item, out uint response)
        {
            response = 0;
            return S_OK;
        }

        public int OnTypeChange(IFileDialog dialog) => S_OK;

        public int OnOverwrite(IFileDialog dialog, IShellItem item, out uint response)
        {
            response = 0;
            return S_OK;
        }

        private void ApplyCurrentFolderFileName(IFileDialog dialog)
        {
            if (!TryGetCurrentFolderPath(dialog, out var folderPath))
            {
                return;
            }

            CurrentFolderPath = folderPath;
            var selectionPath = ResolveDefaultSelectionPathForFolderPath(folderPath);
            if (!string.IsNullOrWhiteSpace(selectionPath))
            {
                dialog.SetFileName(selectionPath);
            }
        }
    }

    [ComImport]
    [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig]
        int Show(IntPtr parent);

        [PreserveSig]
        int SetFileTypes(uint fileTypesCount, IntPtr filterSpec);

        [PreserveSig]
        int SetFileTypeIndex(uint fileType);

        [PreserveSig]
        int GetFileTypeIndex(out uint fileType);

        [PreserveSig]
        int Advise(IFileDialogEvents events, out uint cookie);

        [PreserveSig]
        int Unadvise(uint cookie);

        [PreserveSig]
        int SetOptions(FileOpenOptions options);

        [PreserveSig]
        int GetOptions(out FileOpenOptions options);

        [PreserveSig]
        int SetDefaultFolder(IShellItem item);

        [PreserveSig]
        int SetFolder(IShellItem item);

        [PreserveSig]
        int GetFolder(out IShellItem item);

        [PreserveSig]
        int GetCurrentSelection(out IShellItem item);

        [PreserveSig]
        int SetFileName(
            [MarshalAs(UnmanagedType.LPWStr)] string fileName);

        [PreserveSig]
        int GetFileName(out IntPtr fileName);

        [PreserveSig]
        int SetTitle(
            [MarshalAs(UnmanagedType.LPWStr)] string title);

        [PreserveSig]
        int SetOkButtonLabel(
            [MarshalAs(UnmanagedType.LPWStr)] string text);

        [PreserveSig]
        int SetFileNameLabel(
            [MarshalAs(UnmanagedType.LPWStr)] string label);

        [PreserveSig]
        int GetResult(out IShellItem item);

        [PreserveSig]
        int AddPlace(IShellItem item, uint fileDialogAddPlace);

        [PreserveSig]
        int SetDefaultExtension(
            [MarshalAs(UnmanagedType.LPWStr)] string defaultExtension);

        [PreserveSig]
        int Close(int result);

        [PreserveSig]
        int SetClientGuid(ref Guid guid);

        [PreserveSig]
        int ClearClientData();

        [PreserveSig]
        int SetFilter(IntPtr filter);
    }

    [ComImport]
    [Guid("973510DB-7D7F-452B-8975-74A85828D354")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialogEvents
    {
        [PreserveSig]
        int OnFileOk(IFileDialog dialog);

        [PreserveSig]
        int OnFolderChanging(IFileDialog dialog, IShellItem folder);

        [PreserveSig]
        int OnFolderChange(IFileDialog dialog);

        [PreserveSig]
        int OnSelectionChange(IFileDialog dialog);

        [PreserveSig]
        int OnShareViolation(IFileDialog dialog, IShellItem item, out uint response);

        [PreserveSig]
        int OnTypeChange(IFileDialog dialog);

        [PreserveSig]
        int OnOverwrite(IFileDialog dialog, IShellItem item, out uint response);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(IntPtr bindContext, ref Guid handlerId, ref Guid interfaceId, out IntPtr result);

        [PreserveSig]
        int GetParent(out IShellItem parent);

        [PreserveSig]
        int GetDisplayName(ShellItemDisplayName displayName, out IntPtr name);

        [PreserveSig]
        int GetAttributes(uint attributesMask, out uint attributes);

        [PreserveSig]
        int Compare(IShellItem item, uint hint, out int order);
    }

    [Flags]
    private enum FileOpenOptions : uint
    {
        NoChangeDirectory = 0x00000008,
        PickFolders = 0x00000020,
        ForceFileSystem = 0x00000040,
        PathMustExist = 0x00000800
    }

    private enum ShellItemDisplayName : uint
    {
        FileSystemPath = 0x80058000
    }
}
