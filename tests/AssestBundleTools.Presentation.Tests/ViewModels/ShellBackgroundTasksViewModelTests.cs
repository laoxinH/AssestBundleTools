using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Bindings;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class ShellBackgroundTasksViewModelTests
{
    [Fact]
    public void SchedulerChange_ShouldProjectTaskCountAndOrderedItems()
    {
        var scheduler = new TestBackgroundWorkScheduler();
        using var viewModel = new ShellBackgroundTasksViewModel(
            scheduler,
            new TestLocalizationService());
        var olderTaskId = Guid.NewGuid();
        var newerTaskId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        scheduler.Publish(
            CreateSnapshot(
                olderTaskId,
                BackgroundWorkProfile.WorkspaceImport,
                "Import workspace",
                BackgroundWorkTaskStatus.Running,
                completedCount: 1,
                totalCount: 4,
                detail: "a.ab",
                canCancel: true,
                createdAtUtc: now.AddSeconds(-10),
                lastUpdatedAtUtc: now.AddSeconds(-8)),
            CreateSnapshot(
                newerTaskId,
                BackgroundWorkProfile.BatchExtraction,
                "Batch extraction",
                BackgroundWorkTaskStatus.Running,
                completedCount: 3,
                totalCount: 10,
                detail: "b.ab",
                canCancel: true,
                createdAtUtc: now.AddSeconds(-9),
                lastUpdatedAtUtc: now));

        Assert.Equal(2, viewModel.TaskCount);
        Assert.Equal(Visibility.Visible, viewModel.TaskSummaryVisibility);
        Assert.Equal($"{ResourceKeys.AppStatusBarTaskCountFormat}:2", viewModel.TaskCountText);
        Assert.Equal(newerTaskId, viewModel.Tasks[0].TaskId);
        Assert.Equal("Batch extraction", viewModel.Tasks[0].Title);
        Assert.Equal($"{ResourceKeys.AppStatusBarProgressFormat}:3|10", viewModel.Tasks[0].ProgressText);
    }

    [Fact]
    public void SchedulerChange_ShouldFallbackTitleAndHideSummaryWhenTasksComplete()
    {
        var scheduler = new TestBackgroundWorkScheduler();
        using var viewModel = new ShellBackgroundTasksViewModel(
            scheduler,
            new TestLocalizationService());
        var taskId = Guid.NewGuid();

        scheduler.Publish(CreateSnapshot(
            taskId,
            BackgroundWorkProfile.PreviewPrewarm,
            title: string.Empty,
            BackgroundWorkTaskStatus.Queued,
            completedCount: 0,
            totalCount: null,
            detail: null,
            canCancel: false,
            createdAtUtc: DateTimeOffset.UtcNow,
            lastUpdatedAtUtc: DateTimeOffset.UtcNow));

        Assert.Single(viewModel.Tasks);
        Assert.Equal(ResourceKeys.AppStatusBarTaskProfilePreviewPrewarm, viewModel.Tasks[0].Title);
        Assert.Equal(Visibility.Collapsed, viewModel.Tasks[0].ProgressVisibility);

        scheduler.Publish();

        Assert.Empty(viewModel.Tasks);
        Assert.Equal(0, viewModel.TaskCount);
        Assert.Equal(Visibility.Collapsed, viewModel.TaskSummaryVisibility);
    }

    [Fact]
    public void TaskCancelCommand_ShouldCancelMatchingSchedulerTask()
    {
        var scheduler = new TestBackgroundWorkScheduler();
        using var viewModel = new ShellBackgroundTasksViewModel(
            scheduler,
            new TestLocalizationService());
        var taskId = Guid.NewGuid();

        scheduler.Publish(CreateSnapshot(
            taskId,
            BackgroundWorkProfile.BatchExtraction,
            "Batch extraction",
            BackgroundWorkTaskStatus.Running,
            completedCount: 0,
            totalCount: 8,
            detail: null,
            canCancel: true,
            createdAtUtc: DateTimeOffset.UtcNow,
            lastUpdatedAtUtc: DateTimeOffset.UtcNow));

        viewModel.Tasks[0].CancelCommand.Execute(null);

        Assert.Equal([taskId], scheduler.CancelledTaskIds);
    }

    private static BackgroundWorkSnapshot CreateSnapshot(
        Guid taskId,
        BackgroundWorkProfile profile,
        string title,
        BackgroundWorkTaskStatus status,
        int completedCount,
        int? totalCount,
        string? detail,
        bool canCancel,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastUpdatedAtUtc)
    {
        return new BackgroundWorkSnapshot(
            taskId,
            profile,
            BackgroundWorkSemanticKind.VisibleOutput,
            BackgroundWorkPriority.Visible,
            status,
            WorkspaceId: null,
            WorkspaceKey: null,
            BundleCacheKey: null,
            ResourceKey: null,
            title,
            completedCount,
            totalCount,
            detail,
            IsIndeterminate: totalCount is null,
            ShowInStatusBar: true,
            canCancel,
            createdAtUtc,
            StartedAtUtc: status == BackgroundWorkTaskStatus.Queued ? null : createdAtUtc,
            lastUpdatedAtUtc);
    }

    private sealed class TestBackgroundWorkScheduler : IBackgroundWorkScheduler
    {
        private IReadOnlyList<BackgroundWorkSnapshot> _currentTasks = [];

        public event EventHandler? TasksChanged;

        public IReadOnlyList<BackgroundWorkSnapshot> CurrentTasks => _currentTasks;

        public List<Guid> CancelledTaskIds { get; } = [];

        public BackgroundWorkHandle<TResult> StartAsync<TResult>(
            BackgroundWorkRequest<TResult> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<TResult> RunAsync<TResult>(
            BackgroundWorkRequest<TResult> request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public bool TryCancel(Guid taskId)
        {
            CancelledTaskIds.Add(taskId);
            return true;
        }

        public void Publish(params BackgroundWorkSnapshot[] snapshots)
        {
            _currentTasks = snapshots;
            TasksChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
