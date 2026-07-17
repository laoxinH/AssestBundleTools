using System.Runtime.InteropServices;
using System.Diagnostics;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class WindowsElevatedDragExportBrokerService : IElevatedDragExportBrokerService
{
    private const int VirtualKeyLeftButton = 0x01;
    private const int ReadyWaitAttemptCount = 800;
    private const int CompletionWaitAttemptCount = 4800;
    private static readonly TimeSpan ReadyWaitInterval = TimeSpan.FromMilliseconds(25);
    private readonly IUnelevatedProcessRestartService _unelevatedProcessService;
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private string? _sessionPath;
    private int? _brokerProcessId;
    private string _brokerLaunchDiagnostic = string.Empty;

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
            _brokerLaunchDiagnostic = launchResult.DiagnosticDetail;
            GrantForegroundActivation();

            for (var attempt = 0; attempt < ReadyWaitAttemptCount; attempt++)
            {
                if (IsReady())
                {
                    return ElevatedDragExportBrokerResult.Success(
                        $"The standard-user OLE broker is ready. ProcessId={launchResult.ProcessId}. " +
                        launchResult.DiagnosticDetail);
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
        ElevatedDragExportDescriptor descriptor,
        Func<Task<IReadOnlyList<string>?>> preparePathsAsync)
    {
        ArgumentNullException.ThrowIfNull(preparePathsAsync);

        var totalStopwatch = Stopwatch.StartNew();
        var brokerReadyStopwatch = Stopwatch.StartNew();
        var startResult = await EnsureStartedAsync();
        var brokerReadyMilliseconds = brokerReadyStopwatch.ElapsedMilliseconds;
        if (!startResult.Succeeded || _sessionPath is null)
        {
            return AppendTimings(startResult, brokerReadyMilliseconds, 0, totalStopwatch.ElapsedMilliseconds);
        }

        GrantForegroundActivation();

        var requestPath = Path.Combine(_sessionPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(requestPath);
        using var pointerRelayCancellation = new CancellationTokenSource();
        var pointerRelayTask = RelayPointerReleaseAsync(
            requestPath,
            pointerRelayCancellation.Token);
        ElevatedDragExportBrokerProtocol.WriteCommand(requestPath, descriptor);

        try
        {
            var preparationStopwatch = Stopwatch.StartNew();
            var paths = await preparePathsAsync();
            var preparationMilliseconds = preparationStopwatch.ElapsedMilliseconds;
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
                    "The drag export preparation did not produce an existing file or directory. " +
                    FormatTimings(
                        brokerReadyMilliseconds,
                        preparationMilliseconds,
                        totalStopwatch.ElapsedMilliseconds));
            }

            await ElevatedDragExportBrokerProtocol.WritePayloadAsync(
                requestPath,
                existingPaths,
                CancellationToken.None);
            var completionResult = await WaitForCompletionAsync(requestPath, existingPaths.Length);
            return AppendTimings(
                completionResult,
                brokerReadyMilliseconds,
                preparationMilliseconds,
                totalStopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                Path.Combine(requestPath, ElevatedDragExportBrokerProtocol.FailureFileName),
                exception.Message);
            return ElevatedDragExportBrokerResult.Failure(
                exception.Message + " " +
                FormatTimings(
                    brokerReadyMilliseconds,
                    preparationMilliseconds: 0,
                    totalStopwatch.ElapsedMilliseconds));
        }
        finally
        {
            pointerRelayCancellation.Cancel();
            await ObservePointerRelayCompletionAsync(pointerRelayTask);
        }
    }

    private static async Task RelayPointerReleaseAsync(
        string requestPath,
        CancellationToken cancellationToken)
    {
        if (!IsLeftButtonPressed())
        {
            ElevatedDragExportBrokerProtocol.WritePointerRelease(
                requestPath,
                CapturePointerSignal(dropRequested: false));
            return;
        }

        try
        {
            while (IsLeftButtonPressed())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(8), cancellationToken)
                    .ConfigureAwait(false);
            }

            ElevatedDragExportBrokerProtocol.WritePointerRelease(
                requestPath,
                CapturePointerSignal(dropRequested: true));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            try
            {
                ElevatedDragExportBrokerProtocol.WritePointerRelease(
                    requestPath,
                    CapturePointerSignal(dropRequested: false));
            }
            catch
            {
            }
        }
    }

    private static async Task ObservePointerRelayCompletionAsync(Task pointerRelayTask)
    {
        try
        {
            await pointerRelayTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static bool IsLeftButtonPressed() =>
        (GetAsyncKeyState(VirtualKeyLeftButton) & 0x8000) != 0;

    private static NativePointerReleaseSignal CapturePointerSignal(bool dropRequested)
    {
        return GetCursorPos(out var point)
            ? new NativePointerReleaseSignal(dropRequested, point.X, point.Y)
            : new NativePointerReleaseSignal(false, 0, 0);
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

                return completion.HResult < 0 ||
                       (completion.HResult == 0x00040100 && completion.Effect == 0)
                    ? ElevatedDragExportBrokerResult.Failure(
                        $"OLE drag failed with 0x{completion.HResult:X8}. " +
                        $"Effect={completion.Effect}. " +
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

    private ElevatedDragExportBrokerResult AppendTimings(
        ElevatedDragExportBrokerResult result,
        long brokerReadyMilliseconds,
        long preparationMilliseconds,
        long totalMilliseconds)
    {
        var launchDiagnostic = string.IsNullOrWhiteSpace(_brokerLaunchDiagnostic)
            ? string.Empty
            : string.Concat(_brokerLaunchDiagnostic, " ");
        return result with
        {
            DiagnosticDetail = string.Concat(
                result.DiagnosticDetail,
                " ",
                launchDiagnostic,
                FormatTimings(
                    brokerReadyMilliseconds,
                    preparationMilliseconds,
                    totalMilliseconds))
        };
    }

    private static string FormatTimings(
        long brokerReadyMilliseconds,
        long preparationMilliseconds,
        long totalMilliseconds) =>
        $"Timings: BrokerReady={brokerReadyMilliseconds}ms; " +
        $"Preparation={preparationMilliseconds}ms; DragLifetime={totalMilliseconds}ms.";

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

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
