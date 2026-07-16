using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class WindowsElevatedDragExportBrokerService : IElevatedDragExportBrokerService
{
    private const int ReadyWaitAttemptCount = 800;
    private const int CompletionWaitAttemptCount = 4800;
    private static readonly TimeSpan ReadyWaitInterval = TimeSpan.FromMilliseconds(25);
    private readonly IUnelevatedProcessRestartService _unelevatedProcessService;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private string? _sessionPath;
    private int? _brokerProcessId;

    public WindowsElevatedDragExportBrokerService(
        IUnelevatedProcessRestartService unelevatedProcessService)
    {
        _unelevatedProcessService = unelevatedProcessService ??
            throw new ArgumentNullException(nameof(unelevatedProcessService));
    }

    public async Task<ElevatedDragExportBrokerResult> EnsureStartedAsync()
    {
        await _startGate.WaitAsync();
        try
        {
            if (IsReady())
            {
                GrantForegroundActivation();
                return ElevatedDragExportBrokerResult.Success("The standard-user OLE broker is ready.");
            }

            _sessionPath = CreateSessionPath();
            var launchResult = await _unelevatedProcessService.TryRestartAsync(
                ElevatedDragExportBrokerProtocol.CreateArguments(
                    _sessionPath,
                    Environment.ProcessId));
            if (!launchResult.Succeeded)
            {
                return ElevatedDragExportBrokerResult.Failure(launchResult.DiagnosticDetail);
            }

            _brokerProcessId = launchResult.ProcessId;
            GrantForegroundActivation();

            for (var attempt = 0; attempt < ReadyWaitAttemptCount; attempt++)
            {
                if (IsReady())
                {
                    return ElevatedDragExportBrokerResult.Success(
                        $"The standard-user OLE broker is ready. ProcessId={launchResult.ProcessId}.");
                }

                await Task.Delay(ReadyWaitInterval);
            }

            return ElevatedDragExportBrokerResult.Failure(
                "The standard-user OLE broker process started but did not become ready in time.");
        }
        finally
        {
            _startGate.Release();
        }
    }

    public async Task<ElevatedDragExportBrokerResult> StartDragAsync(
        Func<Task<IReadOnlyList<string>?>> preparePathsAsync)
    {
        ArgumentNullException.ThrowIfNull(preparePathsAsync);

        var startResult = await EnsureStartedAsync();
        if (!startResult.Succeeded || _sessionPath is null)
        {
            return startResult;
        }

        GrantForegroundActivation();

        var requestPath = Path.Combine(_sessionPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(requestPath);
        File.WriteAllText(Path.Combine(requestPath, ElevatedDragExportBrokerProtocol.CommandFileName), string.Empty);

        try
        {
            var paths = await preparePathsAsync();
            var existingPaths = paths?
                .Where(static path => !string.IsNullOrWhiteSpace(path) &&
                                      (File.Exists(path) || Directory.Exists(path)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (existingPaths is not { Length: > 0 })
            {
                File.WriteAllText(
                    Path.Combine(requestPath, ElevatedDragExportBrokerProtocol.FailureFileName),
                    "No exported file or directory was produced.");
                return ElevatedDragExportBrokerResult.Failure(
                    "The drag export preparation did not produce an existing file or directory.");
            }

            await ElevatedDragExportBrokerProtocol.WritePayloadAsync(
                requestPath,
                existingPaths,
                CancellationToken.None);
            return await WaitForCompletionAsync(requestPath, existingPaths.Length);
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                Path.Combine(requestPath, ElevatedDragExportBrokerProtocol.FailureFileName),
                exception.Message);
            return ElevatedDragExportBrokerResult.Failure(exception.Message);
        }
    }

    private static async Task<ElevatedDragExportBrokerResult> WaitForCompletionAsync(
        string requestPath,
        int pathCount)
    {
        var completionPath = Path.Combine(
            requestPath,
            ElevatedDragExportBrokerProtocol.CompletionFileName);
        for (var attempt = 0; attempt < CompletionWaitAttemptCount; attempt++)
        {
            if (File.Exists(completionPath))
            {
                var completion = ElevatedDragExportBrokerProtocol.ReadCompletion(requestPath);
                if (completion is null)
                {
                    return ElevatedDragExportBrokerResult.Failure(
                        "The standard-user OLE broker returned an invalid completion record.");
                }

                return completion.HResult < 0
                    ? ElevatedDragExportBrokerResult.Failure(
                        $"OLE drag failed with 0x{completion.HResult:X8}. " +
                        completion.DiagnosticDetail)
                    : ElevatedDragExportBrokerResult.Success(
                        $"OLE drag completed. HResult=0x{completion.HResult:X8}; " +
                        $"Effect={completion.Effect}; Paths={pathCount}.");
            }

            await Task.Delay(ReadyWaitInterval);
        }

        return ElevatedDragExportBrokerResult.Failure(
            "The standard-user OLE broker did not complete the drag in time.");
    }

    private bool IsReady() =>
        _sessionPath is not null &&
        File.Exists(Path.Combine(_sessionPath, ElevatedDragExportBrokerProtocol.ReadyFileName));

    private void GrantForegroundActivation()
    {
        if (_brokerProcessId is > 0)
        {
            _ = AllowSetForegroundWindow(checked((uint)_brokerProcessId.Value));
        }
    }

    private static string CreateSessionPath()
    {
        Directory.CreateDirectory(ElevatedDragExportBrokerProtocol.RootPath);
        var sessionPath = Path.Combine(
            ElevatedDragExportBrokerProtocol.RootPath,
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sessionPath);
        return sessionPath;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
