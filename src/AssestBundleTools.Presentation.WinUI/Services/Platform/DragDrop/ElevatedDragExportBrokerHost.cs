using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal static class ElevatedDragExportBrokerHost
{
    private static readonly TimeSpan CommandPollInterval = TimeSpan.FromMilliseconds(20);

    public static bool TryRunCurrentProcess()
    {
        if (!ElevatedDragExportBrokerProtocol.TryParseCurrentProcess(out var launch))
        {
            return false;
        }

        Run(launch);
        return true;
    }

    private static void Run(ElevatedDragExportBrokerLaunch launch)
    {
        Directory.CreateDirectory(launch.SessionPath);
        var oleResult = NativeOleFileDrag.OleInitialize(IntPtr.Zero);
        if (oleResult < 0)
        {
            WriteHostFailure(launch.SessionPath, $"OleInitialize failed: 0x{oleResult:X8}.");
            return;
        }

        try
        {
            File.WriteAllText(
                Path.Combine(launch.SessionPath, ElevatedDragExportBrokerProtocol.ReadyFileName),
                Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            while (IsParentRunning(launch.ParentProcessId))
            {
                ProcessPendingRequests(launch.SessionPath, launch.ParentProcessId);
                Thread.Sleep(CommandPollInterval);
            }
        }
        catch (Exception exception)
        {
            WriteHostFailure(launch.SessionPath, exception.ToString());
        }
        finally
        {
            NativeOleFileDrag.OleUninitialize();
        }
    }

    private static void ProcessPendingRequests(string sessionPath, int parentProcessId)
    {
        foreach (var requestPath in Directory.EnumerateDirectories(sessionPath))
        {
            var commandPath = Path.Combine(
                requestPath,
                ElevatedDragExportBrokerProtocol.CommandFileName);
            if (!File.Exists(commandPath))
            {
                continue;
            }

            var processingPath = Path.Combine(
                requestPath,
                ElevatedDragExportBrokerProtocol.ProcessingFileName);
            try
            {
                File.Move(commandPath, processingPath);
            }
            catch (IOException)
            {
                continue;
            }

            NativeOleDragResult result;
            try
            {
                var command = ElevatedDragExportBrokerProtocol.ReadCommand(processingPath) ??
                    throw new InvalidDataException("The drag-export broker command is invalid.");
                result = NativeOleFileDrag.Run(
                    () => WaitForPayload(requestPath, parentProcessId),
                    command.DisplayName,
                    command.ItemCount,
                    Path.Combine(requestPath, "ole.trace"));
            }
            catch (Exception exception)
            {
                result = new NativeOleDragResult(
                    Marshal.GetHRForException(exception),
                    0,
                    exception.Message);
            }

            ElevatedDragExportBrokerProtocol.WriteCompletion(requestPath, result);
        }
    }

    private static IReadOnlyList<string> WaitForPayload(string requestPath, int parentProcessId)
    {
        var payloadPath = Path.Combine(
            requestPath,
            ElevatedDragExportBrokerProtocol.PayloadFileName);
        var failurePath = Path.Combine(
            requestPath,
            ElevatedDragExportBrokerProtocol.FailureFileName);
        var timeoutAt = DateTime.UtcNow.AddMinutes(2);

        while (DateTime.UtcNow < timeoutAt && IsParentRunning(parentProcessId))
        {
            if (File.Exists(payloadPath))
            {
                var paths = ElevatedDragExportBrokerProtocol.ReadPayload(requestPath)?.Paths
                    .Where(static path => File.Exists(path) || Directory.Exists(path))
                    .ToArray();
                if (paths is { Length: > 0 })
                {
                    return paths;
                }

                throw new InvalidDataException("The broker payload did not contain an existing path.");
            }

            if (File.Exists(failurePath))
            {
                throw new InvalidOperationException(File.ReadAllText(failurePath));
            }

            Thread.Sleep(25);
        }

        throw new TimeoutException("The elevated process did not provide drag-export paths in time.");
    }

    private static bool IsParentRunning(int parentProcessId)
    {
        try
        {
            using var process = Process.GetProcessById(parentProcessId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteHostFailure(string sessionPath, string detail)
    {
        try
        {
            File.WriteAllText(Path.Combine(sessionPath, "broker.failed"), detail);
        }
        catch
        {
        }
    }
}

internal readonly record struct NativeOleDragResult(
    int HResult,
    uint Effect,
    string? DiagnosticDetail = null);
