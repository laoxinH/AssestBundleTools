using System.Text;
using AssestBundleTools.Application.Common.Diagnostics;

namespace AssestBundleTools.Infrastructure.Workspace.Diagnostics;

public sealed class FileSystemWorkspaceDiagnosticLogStore : IWorkspaceDiagnosticLogStore
{
    public const string RelativeLogFilePath = @"logs\diagnostics.log";

    private readonly object _syncRoot = new();

    public void Append(string workspaceDataRootPath, WorkspaceDiagnosticLogEntryDto entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDataRootPath);
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            var logFilePath = Path.Combine(
                Path.GetFullPath(workspaceDataRootPath),
                RelativeLogFilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
            var normalizedMessage = entry.Message
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
            var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{entry.Level}] {normalizedMessage}{Environment.NewLine}";

            lock (_syncRoot)
            {
                File.AppendAllText(logFilePath, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // 诊断日志落盘失败不能反向中断用户操作。
        }
    }
}
