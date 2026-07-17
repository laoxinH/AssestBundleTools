using System.Globalization;
using Windows.Storage;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Coordinators;

internal sealed record AssetDragExportStagingSession(string Path);

/// <summary>
/// 管理 Windows 文件拖出所需的短期中转目录。
/// </summary>
internal sealed class AssetDragExportStagingService
{
    private const string RootFolderName = "asset-drag-export";
    private static readonly TimeSpan StaleSessionAge = TimeSpan.FromHours(24);
    private static int _stalePruneScheduled;

    public Task<AssetDragExportStagingSession> CreateSessionAsync()
    {
        var rootPath = GetRootDirectoryPath();
        Directory.CreateDirectory(rootPath);
        ScheduleStaleSessionPrune(rootPath);
        var sessionName = string.Concat(
            DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            "-",
            Guid.NewGuid().ToString("N"));
        var sessionPath = Path.Combine(rootPath, sessionName);
        Directory.CreateDirectory(sessionPath);
        return Task.FromResult(new AssetDragExportStagingSession(sessionPath));
    }

    public async Task<IStorageItem> GetFileAsync(string path) =>
        await StorageFile.GetFileFromPathAsync(path);

    public async Task<IStorageItem> GetFolderAsync(string path) =>
        await StorageFolder.GetFolderFromPathAsync(path);

    public Task CleanupSessionAsync(string sessionPath)
    {
        if (string.IsNullOrWhiteSpace(sessionPath))
        {
            return Task.CompletedTask;
        }

        var rootPath = GetRootDirectoryPath();
        if (!IsChildPath(rootPath, sessionPath))
        {
            return Task.CompletedTask;
        }

        try
        {
            Directory.Delete(sessionPath, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }

        return Task.CompletedTask;
    }

    private static string GetRootDirectoryPath() =>
        Path.Combine(Path.GetTempPath(), "AssestBundleTools", RootFolderName);

    private static void PruneStaleSessions(string rootPath)
    {
        try
        {
            var cutoffUtc = DateTime.UtcNow - StaleSessionAge;
            foreach (var sessionPath in Directory.EnumerateDirectories(rootPath))
            {
                if (Directory.GetCreationTimeUtc(sessionPath) >= cutoffUtc)
                {
                    continue;
                }

                try
                {
                    Directory.Delete(sessionPath, recursive: true);
                }
                catch
                {
                    // 遗留目录清理不应阻止新的拖出会话。
                }
            }
        }
        catch
        {
            // 根目录枚举失败同样不能阻塞当前拖出。
        }
    }

    private static void ScheduleStaleSessionPrune(string rootPath)
    {
        if (Interlocked.Exchange(ref _stalePruneScheduled, 1) != 0)
        {
            return;
        }

        _ = Task.Run(() => PruneStaleSessions(rootPath));
    }

    private static bool IsChildPath(string rootPath, string candidatePath)
    {
        var normalizedRoot = string.Concat(
            Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar),
            Path.DirectorySeparatorChar);
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
