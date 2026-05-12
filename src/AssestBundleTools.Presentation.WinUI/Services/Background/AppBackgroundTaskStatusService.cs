using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 维护 Shell 底部信息条的当前后台任务状态。
/// </summary>
public sealed class AppBackgroundTaskStatusService : ObservableObject, IAppBackgroundTaskStatusService
{
    private const string IdleGlyph = "\uE73E";
    private const string ActiveGlyph = "\uE895";

    private readonly object _sync = new();
    private readonly ILocalizationService _localizationService;
    private DispatcherQueue? _dispatcherQueue;
    private readonly List<BackgroundTaskState> _tasks = [];
    private int _isApplyQueued;
    private string _statusText;
    private string _progressText = string.Empty;
    private double _progressValue;
    private bool _isProgressIndeterminate;
    private Visibility _progressVisibility = Visibility.Collapsed;
    private string _statusGlyph = IdleGlyph;

    public AppBackgroundTaskStatusService(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _dispatcherQueue = TryGetCurrentDispatcherQueue();
        _statusText = GetString(ResourceKeys.AppStatusBarReadyText);
    }

    public void AttachDispatcherQueue(DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);

        _dispatcherQueue = dispatcherQueue;
        RequestApplyCurrentTask();
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public Visibility ProgressVisibility
    {
        get => _progressVisibility;
        private set => SetProperty(ref _progressVisibility, value);
    }

    public string StatusGlyph
    {
        get => _statusGlyph;
        private set => SetProperty(ref _statusGlyph, value);
    }

    public IAppBackgroundTaskHandle BeginTask(string title, int? totalCount = null)
    {
        var normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? GetString(ResourceKeys.AppStatusBarBackgroundTaskFallbackText)
            : title.Trim();
        var task = new BackgroundTaskState(
            Guid.NewGuid(),
            normalizedTitle,
            totalCount is > 0 ? totalCount : null);

        lock (_sync)
        {
            _tasks.Add(task);
        }

        RequestApplyCurrentTask();

        return new BackgroundTaskHandle(this, task.Id);
    }

    private void ReportProgress(
        Guid taskId,
        int completedCount,
        int totalCount,
        string? detail)
    {
        lock (_sync)
        {
            var task = FindTask(taskId);
            if (task is null)
            {
                return;
            }

            task.CompletedCount = Math.Max(0, completedCount);
            task.TotalCount = Math.Max(1, totalCount);
            task.Detail = NormalizeDetail(detail);
            task.IsIndeterminate = false;
            task.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        RequestApplyCurrentTask();
    }

    private void ReportIndeterminate(Guid taskId, string? detail)
    {
        lock (_sync)
        {
            var task = FindTask(taskId);
            if (task is null)
            {
                return;
            }

            task.Detail = NormalizeDetail(detail);
            task.IsIndeterminate = true;
            task.LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        RequestApplyCurrentTask();
    }

    private void CompleteTask(Guid taskId)
    {
        lock (_sync)
        {
            _tasks.RemoveAll(task => task.Id == taskId);
        }

        RequestApplyCurrentTask();
    }

    private void ApplyCurrentTask()
    {
        BackgroundTaskState? currentTask;
        lock (_sync)
        {
            currentTask = _tasks
                .OrderByDescending(static task => task.LastUpdatedAtUtc)
                .FirstOrDefault();
        }

        if (currentTask is null)
        {
            StatusText = GetString(ResourceKeys.AppStatusBarReadyText);
            ProgressText = string.Empty;
            ProgressValue = 0d;
            IsProgressIndeterminate = false;
            ProgressVisibility = Visibility.Collapsed;
            StatusGlyph = IdleGlyph;
            return;
        }

        StatusText = string.IsNullOrWhiteSpace(currentTask.Detail)
            ? currentTask.Title
            : $"{currentTask.Title} - {currentTask.Detail}";
        StatusGlyph = ActiveGlyph;
        ProgressVisibility = Visibility.Visible;
        IsProgressIndeterminate = currentTask.IsIndeterminate || currentTask.TotalCount is null;

        if (currentTask.TotalCount is { } totalCount)
        {
            var completedCount = Math.Clamp(currentTask.CompletedCount, 0, totalCount);
            ProgressValue = totalCount == 0
                ? 0d
                : completedCount * 100d / totalCount;
            ProgressText = _localizationService.GetFormattedString(
                ResourceKeys.AppStatusBarProgressFormat,
                completedCount,
                totalCount);
        }
        else
        {
            ProgressValue = 0d;
            ProgressText = GetString(ResourceKeys.AppStatusBarWorkingText);
        }
    }

    private BackgroundTaskState? FindTask(Guid taskId) =>
        _tasks.FirstOrDefault(task => task.Id == taskId);

    private void RequestApplyCurrentTask()
    {
        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            if (Interlocked.Exchange(ref _isApplyQueued, 1) != 0)
            {
                return;
            }

            if (!_dispatcherQueue.TryEnqueue(RunQueuedApply))
            {
                Interlocked.Exchange(ref _isApplyQueued, 0);
            }

            return;
        }

        ApplyCurrentTask();
    }

    private void RunQueuedApply()
    {
        Interlocked.Exchange(ref _isApplyQueued, 0);
        ApplyCurrentTask();
    }

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);

    private static string? NormalizeDetail(string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? null
            : detail.Trim();

    private static DispatcherQueue? TryGetCurrentDispatcherQueue()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch (COMException)
        {
            return null;
        }
    }

    private sealed class BackgroundTaskHandle : IAppBackgroundTaskHandle
    {
        private readonly AppBackgroundTaskStatusService _owner;
        private readonly Guid _taskId;
        private bool _isDisposed;

        public BackgroundTaskHandle(
            AppBackgroundTaskStatusService owner,
            Guid taskId)
        {
            _owner = owner;
            _taskId = taskId;
        }

        public void ReportProgress(int completedCount, int totalCount, string? detail = null)
        {
            if (!_isDisposed)
            {
                _owner.ReportProgress(_taskId, completedCount, totalCount, detail);
            }
        }

        public void ReportIndeterminate(string? detail = null)
        {
            if (!_isDisposed)
            {
                _owner.ReportIndeterminate(_taskId, detail);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _owner.CompleteTask(_taskId);
        }
    }

    private sealed class BackgroundTaskState
    {
        public BackgroundTaskState(
            Guid id,
            string title,
            int? totalCount)
        {
            Id = id;
            Title = title;
            TotalCount = totalCount;
            IsIndeterminate = totalCount is null;
            LastUpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        public Guid Id { get; }

        public string Title { get; }

        public int CompletedCount { get; set; }

        public int? TotalCount { get; set; }

        public string? Detail { get; set; }

        public bool IsIndeterminate { get; set; }

        public DateTimeOffset LastUpdatedAtUtc { get; set; }
    }
}
