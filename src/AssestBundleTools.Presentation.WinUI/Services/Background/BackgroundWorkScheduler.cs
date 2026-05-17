namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// Presentation 层统一后台任务调度器；业务语义仍由 Application 用例负责。
/// </summary>
public sealed class BackgroundWorkScheduler : IBackgroundWorkScheduler
{
    private const int MaxPreviewWorkerCount = 4;

    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IAppBackgroundTaskStatusService _backgroundTaskStatusService;
    private readonly BackgroundWorkTaskRegistry _taskRegistry = new();
    private readonly SemaphoreSlim _workspaceOpenGate = new(1, 1);
    private readonly SemaphoreSlim _workspaceImportGate = new(1, 1);
    private readonly SemaphoreSlim _previewForegroundGate = new(1, 1);
    private readonly DynamicConcurrencyGate _previewPrewarmGate;
    private readonly SemaphoreSlim _spineRuntimeFrameGate = new(1, 1);
    private readonly SemaphoreSlim _batchExtractionGate = new(1, 1);
    private readonly SemaphoreSlim _batchReplacementGate = new(1, 1);
    private readonly SemaphoreSlim _saveRebuildGate = new(1, 1);

    public BackgroundWorkScheduler(
        IBackgroundTaskRunner backgroundTaskRunner,
        IAppBackgroundTaskStatusService backgroundTaskStatusService,
        int? previewWorkerCount = null)
        : this(
            backgroundTaskRunner,
            backgroundTaskStatusService,
            previewWorkerCount is > 0
                ? () => previewWorkerCount.Value
                : static () => 0)
    {
    }

    public BackgroundWorkScheduler(
        IBackgroundTaskRunner backgroundTaskRunner,
        IAppBackgroundTaskStatusService backgroundTaskStatusService,
        Func<int> previewWorkerCountProvider)
    {
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _backgroundTaskStatusService = backgroundTaskStatusService ?? throw new ArgumentNullException(nameof(backgroundTaskStatusService));
        ArgumentNullException.ThrowIfNull(previewWorkerCountProvider);
        _previewPrewarmGate = new DynamicConcurrencyGate(
            () => Math.Max(0, NormalizePreviewWorkerCount(previewWorkerCountProvider()) - 1));
        _taskRegistry.Changed += OnTaskRegistryChanged;
    }

    public event EventHandler? TasksChanged;

    public IReadOnlyList<BackgroundWorkSnapshot> CurrentTasks => _taskRegistry.CurrentTasks;

    public BackgroundWorkHandle<TResult> StartAsync<TResult>(
        BackgroundWorkRequest<TResult> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskId = Guid.NewGuid();
        var taskCancellationSource = request.CanCancel
            ? new CancellationTokenSource()
            : null;
        _taskRegistry.Register(request, taskId, taskCancellationSource);

        var resultTask = RunRegisteredAsync(
            request,
            taskId,
            taskCancellationSource,
            cancellationToken);
        return new BackgroundWorkHandle<TResult>(
            taskId,
            resultTask,
            () => TryCancel(taskId));
    }

    public async Task<TResult> RunAsync<TResult>(
        BackgroundWorkRequest<TResult> request,
        CancellationToken cancellationToken = default)
    {
        return await StartAsync(request, cancellationToken).ResultTask;
    }

    public bool TryCancel(Guid taskId) => _taskRegistry.TryCancel(taskId);

    private async Task<TResult> RunRegisteredAsync<TResult>(
        BackgroundWorkRequest<TResult> request,
        Guid taskId,
        CancellationTokenSource? taskCancellationSource,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource? timeoutCancellationSource = request.Timeout is { } timeout
            ? new CancellationTokenSource(timeout)
            : null;
        var linkedCancellationSource = CreateLinkedCancellationSource(
            cancellationToken,
            timeoutCancellationSource?.Token,
            taskCancellationSource?.Token);
        Task<TResult>? backgroundTask = null;
        var profileGateLeaseSource = new TaskCompletionSource<ProfileGateLease?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeCancellationSources = true;

        try
        {
            backgroundTask = _backgroundTaskRunner.RunAsync(
                token => ExecuteWithGateAsync(request, taskId, token, profileGateLeaseSource),
                linkedCancellationSource.Token);
            var result = await backgroundTask.WaitAsync(linkedCancellationSource.Token);
            CompleteTask(taskId, BackgroundWorkCompletionKind.Succeeded);
            return result;
        }
        catch (OperationCanceledException) when (timeoutCancellationSource?.IsCancellationRequested == true)
        {
            CompleteTask(taskId, BackgroundWorkCompletionKind.TimedOut);
            if (TryObserveAbandonedTask(
                backgroundTask,
                linkedCancellationSource,
                timeoutCancellationSource,
                taskCancellationSource))
            {
                disposeCancellationSources = false;
            }

            throw new TimeoutException();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            CompleteTask(taskId, BackgroundWorkCompletionKind.Cancelled);
            if (request.ReleaseProfileGateOnCallerCancellation)
            {
                TryReleaseProfileGateLease(profileGateLeaseSource);
            }

            if (TryObserveAbandonedTask(
                backgroundTask,
                linkedCancellationSource,
                timeoutCancellationSource,
                taskCancellationSource))
            {
                disposeCancellationSources = false;
            }

            throw;
        }
        catch (OperationCanceledException)
        {
            CompleteTask(taskId, BackgroundWorkCompletionKind.Cancelled);
            if (TryObserveAbandonedTask(
                backgroundTask,
                linkedCancellationSource,
                timeoutCancellationSource,
                taskCancellationSource))
            {
                disposeCancellationSources = false;
            }

            throw;
        }
        catch
        {
            CompleteTask(taskId, BackgroundWorkCompletionKind.Faulted);
            throw;
        }
        finally
        {
            if (disposeCancellationSources)
            {
                timeoutCancellationSource?.Dispose();
                taskCancellationSource?.Dispose();
                linkedCancellationSource.Dispose();
            }
        }
    }

    private async Task<TResult> ExecuteWithGateAsync<TResult>(
        BackgroundWorkRequest<TResult> request,
        Guid taskId,
        CancellationToken cancellationToken,
        TaskCompletionSource<ProfileGateLease?> profileGateLeaseSource)
    {
        var gateLease = await AcquireProfileGateAsync(request.Profile, cancellationToken);
        profileGateLeaseSource.TrySetResult(gateLease);
        _taskRegistry.Start(taskId);

        try
        {
            using var statusHandle = request.ShowInStatusBar
                ? _backgroundTaskStatusService.BeginTask(
                    new BackgroundTaskStatusRequest(
                        request.StatusTitle ?? string.Empty,
                        request.TotalCount,
                        taskId,
                        request.CanCancel ? () => TryCancel(taskId) : null))
                : null;
            var progress = new BackgroundWorkProgress(
                statusHandle,
                (completedCount, totalCount, detail, isIndeterminate) => _taskRegistry.ReportProgress(
                    taskId,
                    completedCount,
                    totalCount,
                    detail,
                    isIndeterminate));
            var context = new BackgroundWorkExecutionContext(
                taskId,
                request.Profile,
                request.SemanticKind,
                request.WorkspaceId,
                request.WorkspaceKey,
                request.BundleCacheKey,
                request.ResourceKey,
                cancellationToken,
                progress);
            return await request.Operation(context);
        }
        finally
        {
            gateLease?.Release();
        }
    }

    private void CompleteTask(Guid taskId, BackgroundWorkCompletionKind completionKind)
    {
        var cancellationSource = _taskRegistry.Complete(taskId);
        if (completionKind != BackgroundWorkCompletionKind.Cancelled)
        {
            return;
        }

        cancellationSource?.Cancel();
    }

    private void OnTaskRegistryChanged(object? sender, EventArgs e)
    {
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<ProfileGateLease?> AcquireProfileGateAsync(
        BackgroundWorkProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile == BackgroundWorkProfile.PreviewPrewarm)
        {
            return await _previewPrewarmGate.AcquireAsync(cancellationToken);
        }

        var semaphore = ResolveSemaphoreProfileGate(profile);
        if (semaphore is null)
        {
            return null;
        }

        await semaphore.WaitAsync(cancellationToken);
        return new ProfileGateLease(semaphore);
    }

    private SemaphoreSlim? ResolveSemaphoreProfileGate(BackgroundWorkProfile profile) =>
        profile switch
        {
            BackgroundWorkProfile.WorkspaceOpen => _workspaceOpenGate,
            BackgroundWorkProfile.WorkspaceImport => _workspaceImportGate,
            BackgroundWorkProfile.PreviewForeground => _previewForegroundGate,
            BackgroundWorkProfile.SpineRuntimeFrame => _spineRuntimeFrameGate,
            BackgroundWorkProfile.BatchExtraction => _batchExtractionGate,
            BackgroundWorkProfile.BatchReplacement => _batchReplacementGate,
            BackgroundWorkProfile.SaveRebuild => _saveRebuildGate,
            _ => null
        };

    private static int NormalizePreviewWorkerCount(int? previewWorkerCount)
    {
        if (previewWorkerCount is > 0)
        {
            return Math.Clamp(previewWorkerCount.Value, 1, MaxPreviewWorkerCount);
        }

        return Math.Clamp(Environment.ProcessorCount / 2, 1, MaxPreviewWorkerCount);
    }

    private static void TryReleaseProfileGateLease(TaskCompletionSource<ProfileGateLease?> profileGateLeaseSource)
    {
        if (!profileGateLeaseSource.Task.IsCompletedSuccessfully)
        {
            return;
        }

        profileGateLeaseSource.Task.Result?.Release();
    }

    private static CancellationTokenSource CreateLinkedCancellationSource(
        CancellationToken cancellationToken,
        CancellationToken? timeoutCancellationToken,
        CancellationToken? taskCancellationToken)
    {
        return (timeoutCancellationToken, taskCancellationToken) switch
        {
            ({ } timeoutToken, { } taskToken) => CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutToken,
                taskToken),
            ({ } timeoutToken, null) => CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutToken),
            (null, { } taskToken) => CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                taskToken),
            _ => CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };
    }

    private static bool TryObserveAbandonedTask(
        Task? task,
        CancellationTokenSource linkedCancellationSource,
        CancellationTokenSource? timeoutCancellationSource,
        CancellationTokenSource? taskCancellationSource = null)
    {
        if (task is null || task.IsCompleted)
        {
            return false;
        }

        _ = ObserveAbandonedTaskAsync(
            task,
            linkedCancellationSource,
            timeoutCancellationSource,
            taskCancellationSource);
        return true;
    }

    private static async Task ObserveAbandonedTaskAsync(
        Task task,
        CancellationTokenSource linkedCancellationSource,
        CancellationTokenSource? timeoutCancellationSource,
        CancellationTokenSource? taskCancellationSource)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // 任务调用方已因取消或超时离开，后台异常只需被观察，避免未处理任务噪声。
        }
        finally
        {
            timeoutCancellationSource?.Dispose();
            taskCancellationSource?.Dispose();
            linkedCancellationSource.Dispose();
        }
    }

    private sealed class BackgroundWorkProgress : IBackgroundWorkProgress
    {
        private readonly IAppBackgroundTaskHandle? _statusHandle;
        private readonly Action<int, int?, string?, bool> _reportProgress;

        public BackgroundWorkProgress(
            IAppBackgroundTaskHandle? statusHandle,
            Action<int, int?, string?, bool> reportProgress)
        {
            _statusHandle = statusHandle;
            _reportProgress = reportProgress ?? throw new ArgumentNullException(nameof(reportProgress));
        }

        public void ReportProgress(int completedCount, int totalCount, string? detail = null)
        {
            _reportProgress(completedCount, totalCount, detail, false);
            _statusHandle?.ReportProgress(completedCount, totalCount, detail);
        }

        public void ReportIndeterminate(string? detail = null)
        {
            _reportProgress(0, null, detail, true);
            _statusHandle?.ReportIndeterminate(detail);
        }
    }

    private sealed class DynamicConcurrencyGate
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

        private readonly Func<int> _limitProvider;
        private readonly object _syncRoot = new();
        private int _activeCount;

        public DynamicConcurrencyGate(Func<int> limitProvider)
        {
            _limitProvider = limitProvider ?? throw new ArgumentNullException(nameof(limitProvider));
        }

        public async Task<ProfileGateLease> AcquireAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (_syncRoot)
                {
                    var limit = Math.Max(0, _limitProvider());
                    if (limit <= 0)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    if (_activeCount < limit)
                    {
                        _activeCount++;
                        return new ProfileGateLease(Release);
                    }
                }

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        private void Release()
        {
            lock (_syncRoot)
            {
                if (_activeCount > 0)
                {
                    _activeCount--;
                }
            }
        }
    }

    private sealed class ProfileGateLease
    {
        private readonly SemaphoreSlim? _semaphore;
        private readonly Action? _releaseAction;
        private int _released;

        public ProfileGateLease(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore ?? throw new ArgumentNullException(nameof(semaphore));
        }

        public ProfileGateLease(Action releaseAction)
        {
            _releaseAction = releaseAction ?? throw new ArgumentNullException(nameof(releaseAction));
        }

        public void Release()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            if (_semaphore is not null)
            {
                _semaphore.Release();
                return;
            }

            _releaseAction?.Invoke();
        }
    }
}
