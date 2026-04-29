using System.Diagnostics;
using System.IO;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 使用 Windows Explorer 打开本地路径所在位置。
/// </summary>
public sealed class WindowsExplorerPathNavigationService : IPathNavigationService
{
    public bool TryOpenContainingFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception)
        {
            return false;
        }

        try
        {
            if (File.Exists(normalizedPath))
            {
                return StartExplorer($"/select,\"{normalizedPath}\"");
            }

            if (Directory.Exists(normalizedPath))
            {
                return StartExplorer($"\"{normalizedPath}\"");
            }

            var directoryPath = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
            {
                return StartExplorer($"\"{directoryPath}\"");
            }
        }
        catch (Exception)
        {
            return false;
        }

        return false;
    }

    private static bool StartExplorer(string arguments)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true
        });

        return process is not null;
    }
}
