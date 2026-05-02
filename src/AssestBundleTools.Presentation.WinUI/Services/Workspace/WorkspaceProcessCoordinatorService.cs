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
    private readonly ILocalizationService _localizationService;
    private WorkspaceProcessRegistration? _registeredWorkspace;
    private WorkspaceProcessRegistration? _pendingWorkspace;

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

        if (navigationRequest.ForceNewWorkspaceSession)
        {
            return false;
        }

        return TryActivateExistingWorkspace(navigationRequest);
    }

    public bool TryActivateExistingWorkspace(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);
        if (navigationRequest.ForceNewWorkspaceSession)
        {
            return false;
        }

        var normalizedRequest = NormalizeNavigationRequest(navigationRequest);
        var workspaceKey = CreateWorkspaceKey(normalizedRequest);
        if (string.IsNullOrWhiteSpace(workspaceKey) ||
            IsCurrentProcessWorkspace(workspaceKey))
        {
            // 当前进程内窗口复用交给 App 层统一处理；
            // 这里仅负责“命中其他进程已打开工作区”的跨进程激活。
            return false;
        }

        return TryActivateWorkspaceInOtherProcess(
            workspaceKey,
            normalizedRequest,
            "Cross-process workspace activation attempted");
    }

    public bool TryBeginPendingWorkspaceRegistration(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);

        var normalizedRequest = NormalizeNavigationRequest(navigationRequest);
        var workspaceKey = CreateWorkspaceKey(normalizedRequest);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            ClearPendingWorkspaceRegistration();
            return false;
        }

        WorkspaceProcessRegistration? pendingToRelease = null;
        var alreadyOwnedByCurrentProcess = false;

        lock (_syncRoot)
        {
            if (MatchesWorkspace(_registeredWorkspace, workspaceKey))
            {
                alreadyOwnedByCurrentProcess = true;
                if (_pendingWorkspace is not null &&
                    !MatchesWorkspace(_pendingWorkspace, workspaceKey))
                {
                    pendingToRelease = _pendingWorkspace;
                    _pendingWorkspace = null;
                }
            }
            else if (MatchesWorkspace(_pendingWorkspace, workspaceKey))
            {
                alreadyOwnedByCurrentProcess = true;
            }
        }

        if (alreadyOwnedByCurrentProcess)
        {
            ReleaseWorkspaceRegistration(pendingToRelease, "Workspace pending registration cleared");
            return true;
        }

        if (TryActivateWorkspaceInOtherProcess(
                workspaceKey,
                normalizedRequest,
                "Pending workspace registration matched an existing process"))
        {
            return false;
        }

        var pendingRegistration = TryCreateWorkspaceRegistration(workspaceKey);
        if (pendingRegistration is null)
        {
            var activationSucceeded = TryActivateWorkspaceInOtherProcess(
                workspaceKey,
                normalizedRequest,
                "Pending workspace registration race matched an existing process");
            if (!activationSucceeded)
            {
                StartupDiagnostics.Log(
                    $"Pending workspace registration skipped because another process already owns it. WorkspaceKey={workspaceKey}.");
            }

            return false;
        }

        lock (_syncRoot)
        {
            pendingToRelease = _pendingWorkspace;
            _pendingWorkspace = pendingRegistration;
        }

        ReleaseWorkspaceRegistration(pendingToRelease, "Workspace pending registration cleared");
        StartupDiagnostics.Log($"Workspace pending registration established. WorkspaceKey={workspaceKey}.");
        return true;
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

        WorkspaceProcessRegistration? registeredToRelease = null;
        WorkspaceProcessRegistration? pendingToRelease = null;
        var alreadyRegistered = false;
        var promotedPendingRegistration = false;

        lock (_syncRoot)
        {
            if (MatchesWorkspace(_registeredWorkspace, workspaceKey))
            {
                alreadyRegistered = true;
                if (_pendingWorkspace is not null &&
                    !MatchesWorkspace(_pendingWorkspace, workspaceKey))
                {
                    pendingToRelease = _pendingWorkspace;
                    _pendingWorkspace = null;
                }
            }
            else if (MatchesWorkspace(_pendingWorkspace, workspaceKey))
            {
                promotedPendingRegistration = true;
                registeredToRelease = _registeredWorkspace;
                _registeredWorkspace = _pendingWorkspace;
                _pendingWorkspace = null;
            }
        }

        if (alreadyRegistered)
        {
            ReleaseWorkspaceRegistration(pendingToRelease, "Workspace pending registration cleared");
            return;
        }

        if (promotedPendingRegistration)
        {
            ReleaseWorkspaceRegistration(registeredToRelease, "Workspace process registration cleared");
            StartupDiagnostics.Log($"Workspace pending registration promoted to active registration. WorkspaceKey={workspaceKey}.");
            return;
        }

        var registeredWorkspace = TryCreateWorkspaceRegistration(workspaceKey);
        if (registeredWorkspace is null)
        {
            StartupDiagnostics.Log(
                $"Workspace registration skipped because another process already owns it. WorkspaceKey={workspaceKey}.");
            return;
        }

        lock (_syncRoot)
        {
            registeredToRelease = _registeredWorkspace;
            pendingToRelease = _pendingWorkspace;
            _registeredWorkspace = registeredWorkspace;
            _pendingWorkspace = null;
        }

        ReleaseWorkspaceRegistration(pendingToRelease, "Workspace pending registration cleared");
        ReleaseWorkspaceRegistration(registeredToRelease, "Workspace process registration cleared");
        StartupDiagnostics.Log($"Workspace process registration established. WorkspaceKey={workspaceKey}.");
    }

    public void ClearPendingWorkspaceRegistration()
    {
        WorkspaceProcessRegistration? pendingToRelease;

        lock (_syncRoot)
        {
            pendingToRelease = _pendingWorkspace;
            _pendingWorkspace = null;
        }

        ReleaseWorkspaceRegistration(pendingToRelease, "Workspace pending registration cleared");
    }

    public void ClearWorkspaceRegistration()
    {
        WorkspaceProcessRegistration? registeredToRelease;
        WorkspaceProcessRegistration? pendingToRelease;

        lock (_syncRoot)
        {
            registeredToRelease = _registeredWorkspace;
            pendingToRelease = _pendingWorkspace;
            _registeredWorkspace = null;
            _pendingWorkspace = null;
        }

        ReleaseWorkspaceRegistration(pendingToRelease, "Workspace pending registration cleared");
        ReleaseWorkspaceRegistration(registeredToRelease, "Workspace process registration cleared");
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
            return MatchesWorkspace(_registeredWorkspace, workspaceKey) ||
                   MatchesWorkspace(_pendingWorkspace, workspaceKey);
        }
    }

    private bool TryActivateWorkspaceInOtherProcess(
        string workspaceKey,
        WorkspaceNavigationRequest navigationRequest,
        string logPrefix)
    {
        if (!Mutex.TryOpenExisting(CreateMutexName(workspaceKey), out var existingMutex))
        {
            return false;
        }

        existingMutex.Dispose();
        var activationSucceeded = TrySendActivationRequest(
            CreatePipeName(workspaceKey),
            navigationRequest);

        StartupDiagnostics.Log($"{logPrefix}. WorkspaceKey={workspaceKey}; Success={activationSucceeded}.");
        return activationSucceeded;
    }

    private static bool MatchesWorkspace(WorkspaceProcessRegistration? registration, string workspaceKey)
    {
        return registration is not null &&
               string.Equals(registration.WorkspaceKey, workspaceKey, StringComparison.Ordinal);
    }

    private WorkspaceProcessRegistration? TryCreateWorkspaceRegistration(string workspaceKey)
    {
        var workspaceMutex = new Mutex(initiallyOwned: true, CreateMutexName(workspaceKey), out var createdNew);
        if (!createdNew)
        {
            workspaceMutex.Dispose();
            return null;
        }

        var activationListenerCts = new CancellationTokenSource();
        var activationListenerTask = Task.Run(() => ListenForActivationRequestsAsync(
            CreatePipeName(workspaceKey),
            activationListenerCts.Token));
        return new WorkspaceProcessRegistration(
            workspaceKey,
            workspaceMutex,
            activationListenerCts,
            activationListenerTask);
    }

    private static void ReleaseWorkspaceRegistration(
        WorkspaceProcessRegistration? registration,
        string releaseLogPrefix)
    {
        if (registration is null)
        {
            return;
        }

        registration.ActivationListenerCts.Cancel();
        registration.ActivationListenerCts.Dispose();

        try
        {
            registration.WorkspaceMutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // 如果当前线程并未持有该互斥体，直接忽略即可；
            // 这里的目标是尽量释放注册，而不是让清理流程抛出二次异常。
        }

        registration.WorkspaceMutex.Dispose();
        _ = registration.ActivationListenerTask.ContinueWith(
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

        StartupDiagnostics.Log($"{releaseLogPrefix}. WorkspaceKey={registration.WorkspaceKey}.");
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

    private sealed class WorkspaceProcessRegistration
    {
        public WorkspaceProcessRegistration(
            string workspaceKey,
            Mutex workspaceMutex,
            CancellationTokenSource activationListenerCts,
            Task activationListenerTask)
        {
            WorkspaceKey = workspaceKey ?? throw new ArgumentNullException(nameof(workspaceKey));
            WorkspaceMutex = workspaceMutex ?? throw new ArgumentNullException(nameof(workspaceMutex));
            ActivationListenerCts = activationListenerCts ?? throw new ArgumentNullException(nameof(activationListenerCts));
            ActivationListenerTask = activationListenerTask ?? throw new ArgumentNullException(nameof(activationListenerTask));
        }

        public string WorkspaceKey { get; }

        public Mutex WorkspaceMutex { get; }

        public CancellationTokenSource ActivationListenerCts { get; }

        public Task ActivationListenerTask { get; }
    }
}
