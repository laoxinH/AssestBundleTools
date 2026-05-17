namespace AssestBundleTools.Presentation.WinUI.Services;

public enum BackgroundWorkProfile
{
    WorkspaceOpen,
    WorkspaceImport,
    PreviewForeground,
    PreviewPrewarm,
    SpineRuntimeFrame,
    SingleExtraction,
    BatchExtraction,
    BatchReplacement,
    Export,
    SaveRebuild,
    Maintenance
}

public enum BackgroundWorkSemanticKind
{
    TransientRead,
    VisibleOutput,
    WorkspaceMutation
}

public enum BackgroundWorkPriority
{
    Maintenance,
    Background,
    Visible,
    UserBlocking
}

public enum BackgroundWorkCompletionKind
{
    Succeeded,
    Failed,
    Cancelled,
    TimedOut,
    Faulted
}

public enum BackgroundWorkTaskStatus
{
    Queued,
    Running,
    CancellationRequested
}

public interface IBackgroundWorkScheduler
{
    event EventHandler? TasksChanged;

    IReadOnlyList<BackgroundWorkSnapshot> CurrentTasks { get; }

    BackgroundWorkHandle<TResult> StartAsync<TResult>(
        BackgroundWorkRequest<TResult> request,
        CancellationToken cancellationToken = default);

    Task<TResult> RunAsync<TResult>(
        BackgroundWorkRequest<TResult> request,
        CancellationToken cancellationToken = default);

    bool TryCancel(Guid taskId);
}

public sealed class BackgroundWorkRequest<TResult>
{
    public BackgroundWorkRequest(
        BackgroundWorkProfile profile,
        Func<BackgroundWorkExecutionContext, Task<TResult>> operation)
    {
        Profile = profile;
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }

    public BackgroundWorkProfile Profile { get; }

    public BackgroundWorkSemanticKind SemanticKind { get; init; } = BackgroundWorkSemanticKind.TransientRead;

    public BackgroundWorkPriority Priority { get; init; } = BackgroundWorkPriority.Visible;

    public string? WorkspaceId { get; init; }

    public string? WorkspaceKey { get; init; }

    public string? BundleCacheKey { get; init; }

    public string? ResourceKey { get; init; }

    public string? StatusTitle { get; init; }

    public int? TotalCount { get; init; }

    public bool ShowInStatusBar { get; init; } = true;

    public bool CanCancel { get; init; }

    public TimeSpan? Timeout { get; init; }

    public bool ReleaseProfileGateOnCallerCancellation { get; init; }

    internal Func<BackgroundWorkExecutionContext, Task<TResult>> Operation { get; }
}

public sealed class BackgroundWorkHandle<TResult>
{
    public BackgroundWorkHandle(
        Guid taskId,
        Task<TResult> resultTask,
        Func<bool> cancel)
    {
        TaskId = taskId;
        ResultTask = resultTask ?? throw new ArgumentNullException(nameof(resultTask));
        _cancel = cancel ?? throw new ArgumentNullException(nameof(cancel));
    }

    private readonly Func<bool> _cancel;

    public Guid TaskId { get; }

    public Task<TResult> ResultTask { get; }

    public bool Cancel() => _cancel();
}

public sealed record BackgroundWorkSnapshot(
    Guid TaskId,
    BackgroundWorkProfile Profile,
    BackgroundWorkSemanticKind SemanticKind,
    BackgroundWorkPriority Priority,
    BackgroundWorkTaskStatus Status,
    string? WorkspaceId,
    string? WorkspaceKey,
    string? BundleCacheKey,
    string? ResourceKey,
    string Title,
    int CompletedCount,
    int? TotalCount,
    string? Detail,
    bool IsIndeterminate,
    bool ShowInStatusBar,
    bool CanCancel,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset LastUpdatedAtUtc);

public sealed class BackgroundWorkExecutionContext
{
    public BackgroundWorkExecutionContext(
        Guid taskId,
        BackgroundWorkProfile profile,
        BackgroundWorkSemanticKind semanticKind,
        string? workspaceId,
        string? workspaceKey,
        string? bundleCacheKey,
        string? resourceKey,
        CancellationToken cancellationToken,
        IBackgroundWorkProgress progress)
    {
        TaskId = taskId;
        Profile = profile;
        SemanticKind = semanticKind;
        WorkspaceId = workspaceId;
        WorkspaceKey = workspaceKey;
        BundleCacheKey = bundleCacheKey;
        ResourceKey = resourceKey;
        CancellationToken = cancellationToken;
        Progress = progress ?? throw new ArgumentNullException(nameof(progress));
    }

    public Guid TaskId { get; }

    public BackgroundWorkProfile Profile { get; }

    public BackgroundWorkSemanticKind SemanticKind { get; }

    public string? WorkspaceId { get; }

    public string? WorkspaceKey { get; }

    public string? BundleCacheKey { get; }

    public string? ResourceKey { get; }

    public CancellationToken CancellationToken { get; }

    public IBackgroundWorkProgress Progress { get; }
}

public interface IBackgroundWorkProgress
{
    void ReportProgress(int completedCount, int totalCount, string? detail = null);

    void ReportIndeterminate(string? detail = null);
}
