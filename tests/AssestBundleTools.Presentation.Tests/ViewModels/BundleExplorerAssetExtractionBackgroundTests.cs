using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed partial class BundleExplorerAssetExtractionActionHandlerTests
{
    [Fact]
    public async Task ExtractSelectedAssetAsync_ShouldRunSingleExtractionThroughBackgroundProfile()
    {
        var gateway = new RecordingWorkspaceAssetOperationGateway();
        var filePicker = new RecordingFilePickerService
        {
            AssetExtractionOutputFilePath = @"D:\Exports\hero.png"
        };
        var backgroundTaskRunner = new TestBackgroundTaskRunner();
        var backgroundTaskStatusService = new RecordingBackgroundTaskStatusService();
        var handler = new BundleExplorerAssetExtractionActionHandler(
            gateway,
            new TestAppLogService(),
            new TestAppNotificationService(),
            new TestLocalizationService(),
            filePicker,
            backgroundTaskRunner,
            backgroundTaskStatusService);
        var busyStates = new List<bool>();

        await handler.ExtractSelectedAssetAsync(
            CreateWorkspaceSummary(),
            CreateAssetDetails(),
            busyStates.Add,
            _ => throw new InvalidOperationException("Single asset extraction should not fail in this test."));

        Assert.Equal(1, filePicker.AssetExtractionOutputFilePickCount);
        var command = Assert.Single(gateway.ExtractAssetCommands);
        Assert.Equal(filePicker.AssetExtractionOutputFilePath, command.SuggestedOutputPath);
        Assert.True(command.PreserveSuggestedFileName);
        Assert.Equal([true, false], busyStates);
        Assert.Equal(1, backgroundTaskRunner.RunCallCount);
        Assert.Equal(typeof(Result<AssetExtractionResultDto>), backgroundTaskRunner.LastResultType);
        var task = Assert.Single(backgroundTaskStatusService.Tasks);
        Assert.Equal(ResourceKeys.BundleExplorerStatusExtractSelectedAssets, task.Title);
        Assert.Equal(1, task.TotalCount);
        Assert.True(task.IsDisposed);
        Assert.Contains(task.ProgressReports, report => report.CompletedCount == 1 && report.TotalCount == 1);
    }

    [Fact]
    public async Task ExtractSpineAssetGroupAsync_ShouldLeaveTabRefreshToOperationNotification()
    {
        var extractedAtUtc = new DateTimeOffset(2026, 5, 12, 8, 30, 0, TimeSpan.Zero);
        var gateway = new RecordingWorkspaceAssetOperationGateway
        {
            NextExtractedAtUtc = extractedAtUtc
        };
        var filePicker = new RecordingFilePickerService
        {
            SpineGroupExtractionDirectory = @"D:\Exports\Spine"
        };
        var backgroundTaskRunner = new TestBackgroundTaskRunner();
        var backgroundTaskStatusService = new RecordingBackgroundTaskStatusService();
        var handler = new BundleExplorerAssetExtractionActionHandler(
            gateway,
            new TestAppLogService(),
            new TestAppNotificationService(),
            new TestLocalizationService(),
            filePicker,
            backgroundTaskRunner,
            backgroundTaskStatusService);
        var groupTab = new SpineAssetGroupDocumentTabViewModel(
            CreateSpineGroupEntry("group-1", "char_1001_amiya2.ab").Item,
            CreateInspectorTextSet());

        await handler.ExtractSpineAssetGroupAsync(
            CreateWorkspaceSummary(),
            groupTab,
            _ => { },
            _ => throw new InvalidOperationException("Spine group extraction should not fail in this test."));

        Assert.Equal(1, filePicker.SpineGroupExtractionDirectoryPickCount);
        Assert.Single(gateway.ExtractSpineAssetGroupCommands);
        Assert.Equal(1, backgroundTaskRunner.RunCallCount);
        Assert.Equal(typeof(Result<SpineAssetGroupExtractionResultDto>), backgroundTaskRunner.LastResultType);
        var task = Assert.Single(backgroundTaskStatusService.Tasks);
        Assert.Equal(ResourceKeys.BundleExplorerStatusExtractSelectedSpineGroups, task.Title);
        Assert.Equal(1, task.TotalCount);
        Assert.True(task.IsDisposed);
        Assert.Contains(task.ProgressReports, report => report.CompletedCount == 1 && report.TotalCount == 1);
        Assert.Null(groupTab.Item.SkeletonAssets[0].LastExtractedAtUtc);
    }
}
