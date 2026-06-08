using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class BackgroundWorkSchedulerTests
{
    [Fact]
    public async Task RunAsync_ShouldUseRunnerAndStatusProgress()
    {
        var runner = new TestBackgroundTaskRunner();
        var statusService = new RecordingBackgroundTaskStatusService();
        var scheduler = new BackgroundWorkScheduler(runner, statusService);

        var result = await scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.BatchExtraction,
                context =>
                {
                    Assert.Equal(BackgroundWorkProfile.BatchExtraction, context.Profile);
                    Assert.Equal(BackgroundWorkSemanticKind.VisibleOutput, context.SemanticKind);
                    Assert.Equal("workspace-1", context.WorkspaceId);
                    context.Progress.ReportProgress(1, 3, "asset-a");
                    return Task.FromResult(42);
                })
            {
                SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                WorkspaceId = "workspace-1",
                StatusTitle = "Batch extraction",
                TotalCount = 3
            });

        Assert.Equal(42, result);
        Assert.Equal(1, runner.RunCallCount);
        var task = Assert.Single(statusService.Tasks);
        Assert.Equal("Batch extraction", task.Title);
        Assert.Equal(3, task.TotalCount);
        Assert.True(task.IsDisposed);
        var report = Assert.Single(task.ProgressReports);
        Assert.Equal(1, report.CompletedCount);
        Assert.Equal(3, report.TotalCount);
        Assert.Equal("asset-a", report.Detail);
    }

    [Fact]
    public async Task RunAsync_ShouldSerializeBatchExtractionProfile()
    {
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            NullAppBackgroundTaskStatusService.Instance);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.BatchExtraction,
                async _ =>
                {
                    firstEntered.SetResult();
                    await releaseFirst.Task;
                    return 1;
                })
            {
                ShowInStatusBar = false
            });
        await firstEntered.Task;

        var secondTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.BatchExtraction,
                _ =>
                {
                    secondEntered.SetResult();
                    return Task.FromResult(2);
                })
            {
                ShowInStatusBar = false
            });

        var enteredBeforeRelease = await Task.WhenAny(secondEntered.Task, Task.Delay(50));
        Assert.NotSame(secondEntered.Task, enteredBeforeRelease);

        releaseFirst.SetResult();
        Assert.Equal(1, await firstTask);
        Assert.Equal(2, await secondTask);
        Assert.True(secondEntered.Task.IsCompleted);
    }

    [Fact]
    public async Task RunAsync_ShouldSerializeMaintenanceProfile()
    {
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            NullAppBackgroundTaskStatusService.Instance);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.Maintenance,
                async _ =>
                {
                    firstEntered.SetResult();
                    await releaseFirst.Task;
                    return 1;
                })
            {
                ShowInStatusBar = false
            });
        await firstEntered.Task;

        var secondTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.Maintenance,
                _ =>
                {
                    secondEntered.SetResult();
                    return Task.FromResult(2);
                })
            {
                ShowInStatusBar = false
            });

        var enteredBeforeRelease = await Task.WhenAny(secondEntered.Task, Task.Delay(50));
        Assert.NotSame(secondEntered.Task, enteredBeforeRelease);

        releaseFirst.SetResult();
        Assert.Equal(1, await firstTask);
        Assert.Equal(2, await secondTask);
        Assert.True(secondEntered.Task.IsCompleted);
    }

    [Fact]
    public async Task RunAsync_ShouldRunTransientRead_WhenPreviewPrewarmSlotsAreDisabled()
    {
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            NullAppBackgroundTaskStatusService.Instance,
            previewWorkerCount: 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.TransientRead,
                _ =>
                {
                    entered.SetResult();
                    return Task.FromResult(7);
                })
            {
                ShowInStatusBar = false
            });

        await entered.Task;
        Assert.Equal(7, await task);
    }

    [Fact]
    public async Task RunAsync_ShouldBeginStatusAfterProfileGateIsAcquired()
    {
        var statusService = new RecordingBackgroundTaskStatusService();
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            statusService);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.BatchExtraction,
                async _ =>
                {
                    firstEntered.SetResult();
                    await releaseFirst.Task;
                    return 1;
                })
            {
                StatusTitle = "first"
            });
        await firstEntered.Task;
        Assert.Equal("first", Assert.Single(statusService.Tasks).Title);

        var secondTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.BatchExtraction,
                _ =>
                {
                    secondEntered.SetResult();
                    return Task.FromResult(2);
                })
            {
                StatusTitle = "second"
            });

        var enteredBeforeRelease = await Task.WhenAny(secondEntered.Task, Task.Delay(50));
        Assert.NotSame(secondEntered.Task, enteredBeforeRelease);
        Assert.Equal("first", Assert.Single(statusService.Tasks).Title);

        releaseFirst.SetResult();
        Assert.Equal(1, await firstTask);
        Assert.Equal(2, await secondTask);
        Assert.Equal(["first", "second"], statusService.Tasks.Select(static task => task.Title).ToArray());
    }

    [Fact]
    public async Task RunAsync_ShouldHoldProfileGateUntilAbandonedTimedOutTaskCompletes()
    {
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            NullAppBackgroundTaskStatusService.Instance,
            previewWorkerCount: 1);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.PreviewForeground,
                async _ =>
                {
                    firstEntered.SetResult();
                    await releaseFirst.Task;
                    return 1;
                })
            {
                ShowInStatusBar = false,
                Timeout = TimeSpan.FromMilliseconds(10)
            });
        await firstEntered.Task;
        await Assert.ThrowsAsync<TimeoutException>(() => firstTask);

        var secondTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.PreviewForeground,
                _ =>
                {
                    secondEntered.SetResult();
                    return Task.FromResult(2);
                })
            {
                ShowInStatusBar = false
            });

        var enteredBeforeFirstCompleted = await Task.WhenAny(secondEntered.Task, Task.Delay(50));
        Assert.NotSame(secondEntered.Task, enteredBeforeFirstCompleted);

        releaseFirst.SetResult();
        Assert.Equal(2, await secondTask);
        Assert.True(secondEntered.Task.IsCompleted);
    }

    [Fact]
    public async Task RunAsync_ShouldReleaseProfileGateOnCallerCancellation_WhenRequestOptsIn()
    {
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            NullAppBackgroundTaskStatusService.Instance,
            previewWorkerCount: 1);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellationSource = new CancellationTokenSource();

        var firstTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.PreviewForeground,
                async _ =>
                {
                    firstEntered.SetResult();
                    await releaseFirst.Task;
                    return 1;
                })
            {
                ShowInStatusBar = false,
                ReleaseProfileGateOnCallerCancellation = true
            },
            cancellationSource.Token);
        await firstEntered.Task;
        await cancellationSource.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstTask);

        var secondTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.PreviewForeground,
                _ =>
                {
                    secondEntered.SetResult();
                    return Task.FromResult(2);
                })
            {
                ShowInStatusBar = false
            });

        await secondEntered.Task;
        Assert.Equal(2, await secondTask);
        releaseFirst.SetResult();
    }

    [Fact]
    public async Task RunAsync_ShouldRunSpineRuntimeFrameWhilePreviewForegroundIsBusy()
    {
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            NullAppBackgroundTaskStatusService.Instance,
            previewWorkerCount: 1);
        var previewEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePreview = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var spineFrameEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var previewTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.PreviewForeground,
                async _ =>
                {
                    previewEntered.SetResult();
                    await releasePreview.Task;
                    return 1;
                })
            {
                ShowInStatusBar = false
            });
        await previewEntered.Task;

        var spineFrameTask = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.SpineRuntimeFrame,
                _ =>
                {
                    spineFrameEntered.SetResult();
                    return Task.FromResult(2);
                })
            {
                ShowInStatusBar = false
            });

        var enteredBeforePreviewRelease = await Task.WhenAny(spineFrameEntered.Task, Task.Delay(50));
        Assert.Same(spineFrameEntered.Task, enteredBeforePreviewRelease);
        Assert.Equal(2, await spineFrameTask);

        releasePreview.SetResult();
        Assert.Equal(1, await previewTask);
    }

    [Fact]
    public async Task StartAsync_ShouldExposeCurrentTaskSnapshotUntilCompletion()
    {
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            NullAppBackgroundTaskStatusService.Instance);
        var taskChangedCount = 0;
        scheduler.TasksChanged += (_, _) => taskChangedCount++;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var handle = scheduler.StartAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.BatchExtraction,
                async context =>
                {
                    context.Progress.ReportProgress(1, 5, "asset-a");
                    entered.SetResult();
                    await release.Task;
                    return 7;
                })
            {
                SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                WorkspaceId = "workspace-1",
                StatusTitle = "Batch extraction",
                TotalCount = 5,
                CanCancel = true
            });
        await entered.Task;

        var snapshot = Assert.Single(scheduler.CurrentTasks);
        Assert.Equal(handle.TaskId, snapshot.TaskId);
        Assert.Equal(BackgroundWorkProfile.BatchExtraction, snapshot.Profile);
        Assert.Equal(BackgroundWorkTaskStatus.Running, snapshot.Status);
        Assert.Equal("workspace-1", snapshot.WorkspaceId);
        Assert.Equal("Batch extraction", snapshot.Title);
        Assert.Equal(1, snapshot.CompletedCount);
        Assert.Equal(5, snapshot.TotalCount);
        Assert.Equal("asset-a", snapshot.Detail);
        Assert.True(snapshot.CanCancel);
        Assert.True(taskChangedCount >= 2);

        release.SetResult();
        Assert.Equal(7, await handle.ResultTask);
        Assert.Empty(scheduler.CurrentTasks);
    }

    [Fact]
    public async Task StartAsync_ShouldCancelCancellableTaskFromHandle()
    {
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            NullAppBackgroundTaskStatusService.Instance);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var handle = scheduler.StartAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.BatchExtraction,
                async context =>
                {
                    entered.SetResult();
                    await Task.Delay(TimeSpan.FromMinutes(5), context.CancellationToken);
                    return 7;
                })
            {
                ShowInStatusBar = false,
                CanCancel = true
            });
        await entered.Task;

        Assert.True(handle.Cancel());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handle.ResultTask);
        Assert.Empty(scheduler.CurrentTasks);
    }

    [Fact]
    public async Task RunAsync_ShouldExposeSchedulerCancelActionToStatusService()
    {
        var statusService = new AppBackgroundTaskStatusService(new TestLocalizationService());
        var scheduler = new BackgroundWorkScheduler(
            new TestBackgroundTaskRunner(),
            statusService);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = scheduler.RunAsync(
            new BackgroundWorkRequest<int>(
                BackgroundWorkProfile.BatchExtraction,
                async context =>
                {
                    entered.SetResult();
                    await Task.Delay(TimeSpan.FromMinutes(5), context.CancellationToken);
                    return 7;
                })
            {
                StatusTitle = "Batch extraction",
                CanCancel = true
            });
        await entered.Task;

        Assert.True(statusService.CanCancelCurrentTask);

        statusService.CancelCurrentTask();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Empty(scheduler.CurrentTasks);
        Assert.False(statusService.CanCancelCurrentTask);
    }

    [Fact]
    public void AppBackgroundTaskStatusService_ShouldExposeCancelActionForCurrentTask()
    {
        var service = new AppBackgroundTaskStatusService(new TestLocalizationService());
        var cancelCallCount = 0;

        using var handle = service.BeginTask(new BackgroundTaskStatusRequest(
            "Batch extraction",
            TotalCount: 2,
            CancelAction: () => cancelCallCount++));

        Assert.True(service.CanCancelCurrentTask);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, service.CancelVisibility);

        service.CancelCurrentTask();

        Assert.Equal(1, cancelCallCount);
        Assert.False(service.CanCancelCurrentTask);
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, service.CancelVisibility);
    }

    private sealed class RecordingBackgroundTaskStatusService : IAppBackgroundTaskStatusService
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public List<RecordingBackgroundTaskHandle> Tasks { get; } = [];

        public string StatusText => string.Empty;

        public string ProgressText => string.Empty;

        public double ProgressValue => 0d;

        public bool IsProgressIndeterminate => false;

        public Microsoft.UI.Xaml.Visibility ProgressVisibility => Microsoft.UI.Xaml.Visibility.Collapsed;

        public string StatusGlyph => string.Empty;

        public IAppBackgroundTaskHandle BeginTask(string title, int? totalCount = null)
        {
            var task = new RecordingBackgroundTaskHandle(title, totalCount);
            Tasks.Add(task);
            return task;
        }
    }

    private sealed class RecordingBackgroundTaskHandle : IAppBackgroundTaskHandle
    {
        public RecordingBackgroundTaskHandle(string title, int? totalCount)
        {
            Title = title;
            TotalCount = totalCount;
        }

        public string Title { get; }

        public int? TotalCount { get; }

        public bool IsDisposed { get; private set; }

        public List<ProgressReport> ProgressReports { get; } = [];

        public void ReportProgress(int completedCount, int totalCount, string? detail = null)
        {
            ProgressReports.Add(new ProgressReport(completedCount, totalCount, detail));
        }

        public void ReportIndeterminate(string? detail = null)
        {
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed record ProgressReport(int CompletedCount, int TotalCount, string? Detail);
}
