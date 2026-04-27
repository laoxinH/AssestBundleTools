using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Diagnostics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 负责工作区级进程占用与跨进程激活。
/// </summary>
/// <remarks>
/// 当前长期策略为“一个工作区由一个应用进程承载”：
/// 1. 新窗口打开工作区时优先激活已存在实例；
/// 2. 只有在目标工作区尚未被其他进程承载时，才真正启动新进程。
/// </remarks>
public sealed class WorkspaceProcessCoordinatorService : IWorkspaceProcessCoordinatorService
{
    private const int ActivationConnectTimeoutMilliseconds = 400;
    private const int ActivationRetryCount = 6;
    private const int ActivationRetryDelayMilliseconds = 150;
    private readonly object _syncRoot = new();
    private Mutex? _workspaceMutex;
    private CancellationTokenSource? _activationListenerCts;
    private Task? _activationListenerTask;
    private string? _registeredWorkspaceKey;
    private readonly ILocalizationService _localizationService;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public WorkspaceProcessCoordinatorService(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public event EventHandler<WorkspaceProcessActivationRequestedEventArgs>? WorkspaceActivationRequested;

    public bool TryRedirectLaunchToExistingProcess(ShellLaunchRequest? launchRequest)
    {
        if (!TryExtractWorkspaceNavigationRequest(launchRequest, out var navigationRequest))
        {
            return false;
        }

        return TryActivateExistingWorkspace(navigationRequest);
    }

    public bool TryActivateExistingWorkspace(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);

        var normalizedRequest = NormalizeNavigationRequest(navigationRequest);
        var workspaceKey = CreateWorkspaceKey(normalizedRequest);
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            IsCurrentProcessWorkspace(workspaceKey))
        {
            // 当前进程内窗口复用交给 App 层统一处理；
            // 这里仅负责“命中其他进程已打开工作区”的跨进程激活。
            return false;
        }

        if (!Mutex.TryOpenExisting(CreateMutexName(workspaceKey), out var existingMutex))
        {
            return false;
        }

        existingMutex.Dispose();
        var activationSucceeded = TrySendActivationRequest(
            CreatePipeName(workspaceKey),
            normalizedRequest);

        StartupDiagnostics.Log(
            $"Cross-process workspace activation attempted. WorkspaceKey={workspaceKey}; Success={activationSucceeded}.");
        return activationSucceeded;
    }

    public void RegisterWorkspace(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);

        var normalizedRequest = NormalizeNavigationRequest(navigationRequest);
        var workspaceKey = CreateWorkspaceKey(normalizedRequest);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            ClearWorkspaceRegistration();
            return;
        }

        lock (_syncRoot)
        {
            if (string.Equals(_registeredWorkspaceKey, workspaceKey, StringComparison.Ordinal))
            {
                return;
            }
        }

        ClearWorkspaceRegistration();

        var mutexName = CreateMutexName(workspaceKey);
        var workspaceMutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            StartupDiagnostics.Log(
                $"Workspace registration skipped because another process already owns it. WorkspaceKey={workspaceKey}.");
            workspaceMutex.Dispose();
            return;
        }

        var activationListenerCts = new CancellationTokenSource();
        var activationListenerTask = Task.Run(() => ListenForActivationRequestsAsync(
            CreatePipeName(workspaceKey),
            activationListenerCts.Token));

        lock (_syncRoot)
        {
            _workspaceMutex = workspaceMutex;
            _activationListenerCts = activationListenerCts;
            _activationListenerTask = activationListenerTask;
            _registeredWorkspaceKey = workspaceKey;
        }

        StartupDiagnostics.Log($"Workspace process registration established. WorkspaceKey={workspaceKey}.");
    }

    public void ClearWorkspaceRegistration()
    {
        Mutex? workspaceMutex;
        CancellationTokenSource? activationListenerCts;
        Task? activationListenerTask;
        string? workspaceKey;

        lock (_syncRoot)
        {
            workspaceMutex = _workspaceMutex;
            activationListenerCts = _activationListenerCts;
            activationListenerTask = _activationListenerTask;
            workspaceKey = _registeredWorkspaceKey;

            _workspaceMutex = null;
            _activationListenerCts = null;
            _activationListenerTask = null;
            _registeredWorkspaceKey = null;
        }

        activationListenerCts?.Cancel();
        activationListenerCts?.Dispose();

        if (workspaceMutex is not null)
        {
            try
            {
                workspaceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 如果当前线程并未持有该互斥体，直接忽略即可；
                // 这里的目标是尽量释放注册，而不是让清理流程抛出二次异常。
            }

            workspaceMutex.Dispose();
        }

        if (activationListenerTask is not null)
        {
            _ = activationListenerTask.ContinueWith(
                static task =>
                {
                    if (task.IsFaulted && task.Exception is not null)
                    {
                        StartupDiagnostics.Log($"Workspace activation listener faulted: {task.Exception}");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }

        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            StartupDiagnostics.Log($"Workspace process registration cleared. WorkspaceKey={workspaceKey}.");
        }
    }

    public void StartNewProcess(ShellLaunchRequest launchRequest)
    {
        ArgumentNullException.ThrowIfNull(launchRequest);

        var processPath = ResolveCurrentProcessPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(ShellLaunchArgumentCodec.CreateArgumentToken(launchRequest, _localizationService));

        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw PresentationExceptionFactory.CreateStartWorkspaceProcessFailedException(_localizationService);
        }
    }

    public void Dispose()
    {
        ClearWorkspaceRegistration();
    }

    /// <summary>
    /// 当前监听器只负责接收“请把这个工作区窗口带到前台”的请求，
    /// 具体导航和 UI 激活仍交给 App / Window 层处理。
    /// </summary>
    private async Task ListenForActivationRequestsAsync(string pipeName, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var serverStream = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await serverStream.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(
                    serverStream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 1024,
                    leaveOpen: false);

                var payload = await reader.ReadToEndAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    continue;
                }

                var navigationRequest = JsonSerializer.Deserialize<WorkspaceNavigationRequest>(payload, SerializerOptions);
                if (navigationRequest is null)
                {
                    continue;
                }

                WorkspaceActivationRequested?.Invoke(
                    this,
                    new WorkspaceProcessActivationRequestedEventArgs(navigationRequest));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Log($"Workspace activation listener failed: {ex}");

                try
                {
                    await Task.Delay(ActivationRetryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private static bool TrySendActivationRequest(
        string pipeName,
        WorkspaceNavigationRequest navigationRequest)
    {
        var payload = JsonSerializer.Serialize(navigationRequest, SerializerOptions);

        for (var attempt = 1; attempt <= ActivationRetryCount; attempt++)
        {
            try
            {
                using var clientStream = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.Out,
                    PipeOptions.None);

                clientStream.Connect(ActivationConnectTimeoutMilliseconds);

                using var writer = new StreamWriter(
                    clientStream,
                    Encoding.UTF8,
                    bufferSize: 1024,
                    leaveOpen: false);

                writer.Write(payload);
                writer.Flush();
                return true;
            }
            catch (TimeoutException) when (attempt < ActivationRetryCount)
            {
                Thread.Sleep(ActivationRetryDelayMilliseconds);
            }
            catch (IOException) when (attempt < ActivationRetryCount)
            {
                Thread.Sleep(ActivationRetryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException) when (attempt < ActivationRetryCount)
            {
                Thread.Sleep(ActivationRetryDelayMilliseconds);
            }
        }

        return false;
    }

    private static bool TryExtractWorkspaceNavigationRequest(
        ShellLaunchRequest? launchRequest,
        [NotNullWhen(true)]
        out WorkspaceNavigationRequest? navigationRequest)
    {
        navigationRequest = launchRequest?.NavigationParameter as WorkspaceNavigationRequest;
        return navigationRequest is not null;
    }

    private static WorkspaceNavigationRequest NormalizeNavigationRequest(WorkspaceNavigationRequest navigationRequest)
    {
        return navigationRequest with
        {
            OpenTarget = WorkspaceOpenTarget.CurrentWindow
        };
    }

    private bool IsCurrentProcessWorkspace(string workspaceKey)
    {
        lock (_syncRoot)
        {
            return string.Equals(_registeredWorkspaceKey, workspaceKey, StringComparison.Ordinal);
        }
    }

    private string ResolveCurrentProcessPath()
    {
        return Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw PresentationExceptionFactory.CreateCurrentProcessPathUnavailableException(_localizationService);
    }

    private static string CreateWorkspaceKey(WorkspaceNavigationRequest navigationRequest)
    {
        return WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            navigationRequest.SourcePath,
            navigationRequest.WorkspaceDataRootPath);
    }

    private static string CreateMutexName(string workspaceKey)
    {
        return $@"Local\AssestBundleTools.Workspace.{workspaceKey}";
    }

    private static string CreatePipeName(string workspaceKey)
    {
        return $"AssestBundleTools.Workspace.{workspaceKey}";
    }
}
