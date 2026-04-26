using System.Diagnostics;
using System.Text;

namespace AssestBundleTools.Presentation.WinUI.Diagnostics;

/// <summary>
/// 启动阶段的最小诊断工具。
/// 当前优先服务于 VS 初步调试，尽量把窗口真正出现前的异常线索写入本地日志。
/// </summary>
public static class StartupDiagnostics
{
    private static readonly object SyncRoot = new();

    public static string LogFilePath { get; } = Path.Combine(
        Path.GetTempPath(),
        "AssestBundleTools",
        "startup.log");

    public static void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

        Debug.WriteLine(line);

        try
        {
            lock (SyncRoot)
            {
                var directoryPath = Path.GetDirectoryName(LogFilePath);

                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // 启动诊断不能再向外抛异常，避免影响主流程。
        }
    }
}
