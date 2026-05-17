namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 维护当前会话内后台任务的可观察快照。
/// </summary>
internal sealed class BackgroundWorkTaskRegistry
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, BackgroundWorkTaskState> _taskStates = [];

    public event EventHandler? Changed;

    public IReadOnlyList<BackgroundWorkSnapshot> CurrentTasks
    {
        get
        {
            lock (_syncRoot)
            {
                return _taskStates.Values
                    .Select(static state => state.CreateSnapshot())
                    .OrderBy(static snapshot => snapshot.CreatedAtUtc)
                    .ToArray();
            }
        }
    }

    public void Register<TResult>(
        BackgroundWorkRequest<TResult> request,
        Guid taskId,
        CancellationTokenSource? cancellationSource)
    {
        ArgumentNullException.ThrowIfNull(request);

        var taskState = new BackgroundWorkTaskState(
            taskId,
            request.Profile,
            request.SemanticKind,
            request.Priority,
            request.WorkspaceId,
            request.WorkspaceKey,
            request.BundleCacheKey,
            request.ResourceKey,
            request.StatusTitle ?? string.Empty,
            request.TotalCount,
            request.ShowInStatusBar,
            request.CanCancel,
            cancellationSource);

        lock (_syncRoot)
        {
            _taskStates[taskId] = taskState;
        }

        NotifyChanged();
    }

    public void Start(Guid taskId)
    {
        lock (_syncRoot)
        {
            if (!_taskStates.TryGetValue(taskId, out var taskState))
            {
                return;
            }

            taskState.Status = BackgroundWorkTaskStatus.Running;
            taskState.StartedAtUtc ??= DateTimeOffset.UtcNow;
            taskState.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        NotifyChanged();
    }

    public void ReportProgress(
        Guid taskId,
        int completedCount,
        int? totalCount,
        string? detail,
        bool isIndeterminate)
    {
        lock (_syncRoot)
        {
            if (!_taskStates.TryGetValue(taskId, out var taskState))
            {
                return;
            }

            taskState.CompletedCount = Math.Max(0, completedCount);
            taskState.TotalCount = totalCount is > 0
                ? totalCount
                : taskState.TotalCount;
            taskState.Detail = NormalizeDetail(detail);
            taskState.IsIndeterminate = isIndeterminate;
            taskState.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        NotifyChanged();
    }

    public CancellationTokenSource? Complete(Guid taskId)
    {
        CancellationTokenSource? cancellationSource = null;
        lock (_syncRoot)
        {
            if (_taskStates.Remove(taskId, out var taskState))
            {
                cancellationSource = taskState.CancellationSource;
            }
        }

        NotifyChanged();
        return cancellationSource;
    }

    public bool TryCancel(Guid taskId)
    {
        CancellationTokenSource? cancellationSource;
        lock (_syncRoot)
        {
            if (!_taskStates.TryGetValue(taskId, out var taskState) ||
                !taskState.CanCancel ||
                taskState.CancellationSource is null)
            {
                return false;
            }

            taskState.Status = BackgroundWorkTaskStatus.CancellationRequested;
            taskState.CanCancel = false;
            taskState.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
            cancellationSource = taskState.CancellationSource;
        }

        NotifyChanged();
        cancellationSource.Cancel();
        return true;
    }

    private void NotifyChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string? NormalizeDetail(string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? null
            : detail.Trim();

    private sealed class BackgroundWorkTaskState
    {
        public BackgroundWorkTaskState(
            Guid taskId,
            BackgroundWorkProfile profile,
            BackgroundWorkSemanticKind semanticKind,
            BackgroundWorkPriority priority,
            string? workspaceId,
            string? workspaceKey,
            string? bundleCacheKey,
            string? resourceKey,
            string title,
            int? totalCount,
            bool showInStatusBar,
            bool canCancel,
            CancellationTokenSource? cancellationSource)
        {
            TaskId = taskId;
            Profile = profile;
            SemanticKind = semanticKind;
            Priority = priority;
            WorkspaceId = workspaceId;
            WorkspaceKey = workspaceKey;
            BundleCacheKey = bundleCacheKey;
            ResourceKey = resourceKey;
            Title = title;
            TotalCount = totalCount;
            ShowInStatusBar = showInStatusBar;
            CanCancel = canCancel;
            CancellationSource = cancellationSource;
            CreatedAtUtc = DateTimeOffset.UtcNow;
            LastUpdatedAtUtc = CreatedAtUtc;
            IsIndeterminate = totalCount is null;
        }

        public Guid TaskId { get; }

        public BackgroundWorkProfile Profile { get; }

        public BackgroundWorkSemanticKind SemanticKind { get; }

        public BackgroundWorkPriority Priority { get; }

        public BackgroundWorkTaskStatus Status { get; set; } = BackgroundWorkTaskStatus.Queued;

        public string? WorkspaceId { get; }

        public string? WorkspaceKey { get; }

        public string? BundleCacheKey { get; }

        public string? ResourceKey { get; }

        public string Title { get; }

        public int CompletedCount { get; set; }

        public int? TotalCount { get; set; }

        public string? Detail { get; set; }

        public bool IsIndeterminate { get; set; }

        public bool ShowInStatusBar { get; }

        public bool CanCancel { get; set; }

        public DateTimeOffset CreatedAtUtc { get; }

        public DateTimeOffset? StartedAtUtc { get; set; }

        public DateTimeOffset LastUpdatedAtUtc { get; set; }

        public CancellationTokenSource? CancellationSource { get; }

        public BackgroundWorkSnapshot CreateSnapshot() =>
            new(
                TaskId,
                Profile,
                SemanticKind,
                Priority,
                Status,
                WorkspaceId,
                WorkspaceKey,
                BundleCacheKey,
                ResourceKey,
                Title,
                CompletedCount,
                TotalCount,
                Detail,
                IsIndeterminate,
                ShowInStatusBar,
                CanCancel,
                CreatedAtUtc,
                StartedAtUtc,
                LastUpdatedAtUtc);
    }
}
