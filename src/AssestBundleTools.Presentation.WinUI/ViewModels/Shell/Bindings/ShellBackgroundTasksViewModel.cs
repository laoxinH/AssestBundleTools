using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Bindings;

/// <summary>
/// 将统一后台调度器的当前任务快照投影给 Shell 状态条和任务悬浮面板。
/// </summary>
public sealed class ShellBackgroundTasksViewModel : ObservableObject, IDisposable
{
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly ILocalizationService _localizationService;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly Dictionary<Guid, ShellBackgroundTaskItemViewModel> _itemsByTaskId = [];
    private int _isRefreshQueued;
    private int _taskCount;
    private string _taskCountText = string.Empty;
    private string _taskSummaryToolTipText = string.Empty;
    private string _taskSummaryAutomationName = string.Empty;
    private Visibility _taskSummaryVisibility = Visibility.Collapsed;
    private Visibility _emptyVisibility = Visibility.Visible;

    public ShellBackgroundTasksViewModel(
        IBackgroundWorkScheduler backgroundWorkScheduler,
        ILocalizationService localizationService,
        DispatcherQueue? dispatcherQueue = null)
    {
        _backgroundWorkScheduler = backgroundWorkScheduler ?? throw new ArgumentNullException(nameof(backgroundWorkScheduler));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _dispatcherQueue = dispatcherQueue;
        PanelTitle = _localizationService.GetRequiredString(ResourceKeys.AppStatusBarTaskFlyoutTitle);
        EmptyText = _localizationService.GetRequiredString(ResourceKeys.AppStatusBarTaskFlyoutEmptyText);
        _backgroundWorkScheduler.TasksChanged += OnSchedulerTasksChanged;
        Refresh();
    }

    public ObservableCollection<ShellBackgroundTaskItemViewModel> Tasks { get; } = [];

    public string PanelTitle { get; }

    public string EmptyText { get; }

    public int TaskCount
    {
        get => _taskCount;
        private set => SetProperty(ref _taskCount, value);
    }

    public string TaskCountText
    {
        get => _taskCountText;
        private set => SetProperty(ref _taskCountText, value);
    }

    public string TaskSummaryToolTipText
    {
        get => _taskSummaryToolTipText;
        private set => SetProperty(ref _taskSummaryToolTipText, value);
    }

    public string TaskSummaryAutomationName
    {
        get => _taskSummaryAutomationName;
        private set => SetProperty(ref _taskSummaryAutomationName, value);
    }

    public Visibility TaskSummaryVisibility
    {
        get => _taskSummaryVisibility;
        private set => SetProperty(ref _taskSummaryVisibility, value);
    }

    public Visibility EmptyVisibility
    {
        get => _emptyVisibility;
        private set => SetProperty(ref _emptyVisibility, value);
    }

    public void Dispose()
    {
        _backgroundWorkScheduler.TasksChanged -= OnSchedulerTasksChanged;
    }

    private void OnSchedulerTasksChanged(object? sender, EventArgs e)
    {
        RequestRefresh();
    }

    private void RequestRefresh()
    {
        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            if (Interlocked.Exchange(ref _isRefreshQueued, 1) != 0)
            {
                return;
            }

            if (!_dispatcherQueue.TryEnqueue(RunQueuedRefresh))
            {
                Interlocked.Exchange(ref _isRefreshQueued, 0);
            }

            return;
        }

        Refresh();
    }

    private void RunQueuedRefresh()
    {
        Interlocked.Exchange(ref _isRefreshQueued, 0);
        Refresh();
    }

    private void Refresh()
    {
        var snapshots = _backgroundWorkScheduler.CurrentTasks
            .OrderByDescending(static snapshot => snapshot.LastUpdatedAtUtc)
            .ThenBy(static snapshot => snapshot.CreatedAtUtc)
            .ToArray();

        RemoveCompletedItems(snapshots);
        UpsertItems(snapshots);
        ApplyTaskCount(snapshots.Length);
    }

    private void RemoveCompletedItems(IReadOnlyCollection<BackgroundWorkSnapshot> snapshots)
    {
        var activeTaskIds = snapshots
            .Select(static snapshot => snapshot.TaskId)
            .ToHashSet();

        for (var index = Tasks.Count - 1; index >= 0; index--)
        {
            var item = Tasks[index];
            if (activeTaskIds.Contains(item.TaskId))
            {
                continue;
            }

            Tasks.RemoveAt(index);
            _itemsByTaskId.Remove(item.TaskId);
        }
    }

    private void UpsertItems(IReadOnlyList<BackgroundWorkSnapshot> snapshots)
    {
        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            if (!_itemsByTaskId.TryGetValue(snapshot.TaskId, out var item))
            {
                item = new ShellBackgroundTaskItemViewModel(
                    _localizationService,
                    CancelTask,
                    snapshot);
                _itemsByTaskId[snapshot.TaskId] = item;
            }
            else
            {
                item.Update(snapshot);
            }

            var currentIndex = Tasks.IndexOf(item);
            if (currentIndex < 0)
            {
                Tasks.Insert(index, item);
            }
            else if (currentIndex != index)
            {
                Tasks.Move(currentIndex, index);
            }
        }
    }

    private void ApplyTaskCount(int taskCount)
    {
        TaskCount = taskCount;
        TaskSummaryVisibility = taskCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyVisibility = taskCount > 0 ? Visibility.Collapsed : Visibility.Visible;
        TaskCountText = taskCount > 0
            ? _localizationService.GetFormattedString(ResourceKeys.AppStatusBarTaskCountFormat, taskCount)
            : string.Empty;
        TaskSummaryToolTipText = taskCount > 0
            ? _localizationService.GetFormattedString(ResourceKeys.AppStatusBarTaskSummaryToolTipFormat, taskCount)
            : string.Empty;
        TaskSummaryAutomationName = taskCount > 0
            ? _localizationService.GetFormattedString(ResourceKeys.AppStatusBarTaskSummaryAutomationNameFormat, taskCount)
            : string.Empty;
    }

    private void CancelTask(Guid taskId)
    {
        _backgroundWorkScheduler.TryCancel(taskId);
    }
}
