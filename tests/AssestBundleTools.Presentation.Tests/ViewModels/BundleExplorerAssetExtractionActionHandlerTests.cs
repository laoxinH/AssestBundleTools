using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetExtractionActionHandlerTests
{
    [Fact]
    public async Task ExtractSelectedAssetsAsync_ShouldAskForOutputDirectory_AndPassItToEachExtractionCommand()
    {
        var gateway = new RecordingWorkspaceAssetOperationGateway();
        var filePicker = new RecordingFilePickerService
        {
            BatchExtractionOutputDirectory = @"D:\Exports\Selected"
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
        var workspaceSummary = CreateWorkspaceSummary();
        var firstEntry = CreateAssetEntry("asset-1", "hero");
        var secondEntry = CreateAssetEntry("asset-2", "hero_alpha");
        var busyStates = new List<bool>();

        await handler.ExtractSelectedAssetsAsync(
            workspaceSummary,
            [firstEntry, secondEntry],
            "char_1001_amiya2.ab",
            busyStates.Add,
            _ => throw new InvalidOperationException("Batch extraction should not fail in this test."));

        Assert.Equal(1, filePicker.BatchExtractionOutputDirectoryPickCount);
        Assert.Equal(2, gateway.ExtractAssetCommands.Count);
        Assert.All(
            gateway.ExtractAssetCommands,
            command => Assert.StartsWith(
                Path.Combine(filePicker.BatchExtractionOutputDirectory!, "char_1001_amiya2.ab", "Texture2D"),
                command.SuggestedOutputPath,
                StringComparison.OrdinalIgnoreCase));
        Assert.Equal(firstEntry.Item.AssetId, gateway.ExtractAssetCommands[0].AssetId);
        Assert.EndsWith("hero", gateway.ExtractAssetCommands[0].SuggestedOutputPath, StringComparison.Ordinal);
        Assert.Equal(secondEntry.Item.AssetId, gateway.ExtractAssetCommands[1].AssetId);
        Assert.EndsWith("hero_alpha", gateway.ExtractAssetCommands[1].SuggestedOutputPath, StringComparison.Ordinal);
        Assert.Equal([true, false], busyStates);
        Assert.Equal(1, backgroundTaskRunner.RunCallCount);
        var task = Assert.Single(backgroundTaskStatusService.Tasks);
        Assert.Equal(ResourceKeys.BundleExplorerStatusExtractSelectedAssets, task.Title);
        Assert.Equal(2, task.TotalCount);
        Assert.True(task.IsDisposed);
        Assert.Contains(task.ProgressReports, report => report.CompletedCount == 2 && report.TotalCount == 2);
    }

    [Fact]
    public async Task ExtractSelectedAssetsAsync_ShouldCancelWithoutExtraction_WhenOutputDirectoryIsNotSelected()
    {
        var gateway = new RecordingWorkspaceAssetOperationGateway();
        var filePicker = new RecordingFilePickerService
        {
            BatchExtractionOutputDirectory = null
        };
        var handler = new BundleExplorerAssetExtractionActionHandler(
            gateway,
            new TestAppLogService(),
            new TestAppNotificationService(),
            new TestLocalizationService(),
            filePicker);
        var busyStates = new List<bool>();

        await handler.ExtractSelectedAssetsAsync(
            CreateWorkspaceSummary(),
            [CreateAssetEntry("asset-1", "hero")],
            "char_1001_amiya2.ab",
            busyStates.Add,
            _ => throw new InvalidOperationException("Cancelled extraction should not report command failures."));

        Assert.Equal(1, filePicker.BatchExtractionOutputDirectoryPickCount);
        Assert.Empty(gateway.ExtractAssetCommands);
        Assert.Equal([true, false], busyStates);
    }

    [Fact]
    public async Task ExtractSelectedSpineAssetGroupsAsync_ShouldUseBundleDirectoryUnderSelectedRoot()
    {
        var gateway = new RecordingWorkspaceAssetOperationGateway();
        var filePicker = new RecordingFilePickerService
        {
            BatchExtractionOutputDirectory = @"D:\Exports\Selected"
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
        var firstEntry = CreateSpineGroupEntry("group-1", "char_1001_amiya2.ab");
        var secondEntry = CreateSpineGroupEntry("group-2", "char_1001_amiya2.ab");
        var busyStates = new List<bool>();

        await handler.ExtractSelectedSpineAssetGroupsAsync(
            CreateWorkspaceSummary(),
            [firstEntry, secondEntry],
            defaultBundleDisplayName: "fallback.ab",
            busyStates.Add,
            _ => throw new InvalidOperationException("Spine group batch extraction should not fail in this test."));

        Assert.Equal(1, filePicker.BatchExtractionOutputDirectoryPickCount);
        Assert.Equal(2, gateway.ExtractSpineAssetGroupCommands.Count);
        Assert.Equal(firstEntry.Item.GroupId, gateway.ExtractSpineAssetGroupCommands[0].GroupId);
        Assert.Equal(secondEntry.Item.GroupId, gateway.ExtractSpineAssetGroupCommands[1].GroupId);
        Assert.All(
            gateway.ExtractSpineAssetGroupCommands,
            command => Assert.Equal(
                Path.Combine(filePicker.BatchExtractionOutputDirectory!, "char_1001_amiya2.ab"),
                command.OutputDirectory));
        Assert.Equal([true, false], busyStates);
        Assert.Equal(1, backgroundTaskRunner.RunCallCount);
        var task = Assert.Single(backgroundTaskStatusService.Tasks);
        Assert.Equal(ResourceKeys.BundleExplorerStatusExtractSelectedSpineGroups, task.Title);
        Assert.Equal(2, task.TotalCount);
        Assert.True(task.IsDisposed);
        Assert.Contains(task.ProgressReports, report => report.CompletedCount == 2 && report.TotalCount == 2);
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
        var handler = new BundleExplorerAssetExtractionActionHandler(
            gateway,
            new TestAppLogService(),
            new TestAppNotificationService(),
            new TestLocalizationService(),
            filePicker);
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
        Assert.Null(groupTab.Item.SkeletonAssets[0].LastExtractedAtUtc);
    }

    [Fact]
    public async Task ExtractSelectedAssetsAsync_ShouldRouteSpineGroupMultiSelection_ToBatchSpineExtraction()
    {
        var gateway = new RecordingWorkspaceAssetOperationGateway();
        var filePicker = new RecordingFilePickerService
        {
            BatchExtractionOutputDirectory = @"D:\Exports\Selected"
        };
        var appLogService = new TestAppLogService();
        var notificationService = new TestAppNotificationService();
        var localizationService = new TestLocalizationService();
        var modificationActionHandler = new BundleExplorerAssetModificationActionHandler(
            gateway,
            appLogService,
            notificationService,
            localizationService,
            filePicker);
        var primaryPane = new AssetPaneViewModel
        {
            SupportsBatchExtractionQuickAction = true,
            ViewMode = AssetPaneViewMode.SpineGroups
        };
        var firstEntry = CreateSpineGroupEntry("group-1", "char_1001_amiya2.ab");
        var secondEntry = CreateSpineGroupEntry("group-2", "char_1001_amiya2.ab");
        primaryPane.ReplaceSpineGroups("char_1001_amiya2.ab", [firstEntry, secondEntry], "no spine");
        primaryPane.UpdateSpineGroupSelection([firstEntry, secondEntry], secondEntry);
        var isBusy = false;

        var coordinator = new BundleExplorerAssetCommandCoordinator(
            appLogService,
            localizationService,
            new TestPathNavigationService(),
            new TestWorkspaceShellHost(CreateWorkspaceSummary()),
            primaryPane,
            modificationActionHandler,
            new BundleExplorerOperationFeedbackBridge(appLogService, notificationService, localizationService),
            () => isBusy,
            value => isBusy = value,
            () => throw new NotSupportedException(),
            () => null,
            () => null,
            (_, _) => throw new NotSupportedException());

        await coordinator.ExtractSelectedAssetsAsync();

        Assert.Equal(2, gateway.ExtractSpineAssetGroupCommands.Count);
        Assert.Empty(gateway.ExtractAssetCommands);
    }

    [Fact]
    public async Task ModifyFromFileReplacementSourceAsync_ShouldPickAndApplyFileImmediately()
    {
        var gateway = new RecordingWorkspaceAssetOperationGateway();
        var filePicker = new RecordingFilePickerService
        {
            ReplacementSourceFilePath = @"D:\Imports\hero.png"
        };
        var handler = new BundleExplorerAssetModificationActionHandler(
            gateway,
            new TestAppLogService(),
            new TestAppNotificationService(),
            new TestLocalizationService(),
            filePicker);
        var workspaceSummary = CreateWorkspaceSummary();
        var assetTab = new AssetDocumentTabViewModel(
            new BundleId("bundle-ui"),
            new AssetId("asset-1"),
            "hero",
            CreateInspectorTextSet());
        var details = CreateAssetDetails();
        var busyStates = new List<bool>();
        var reapplyCount = 0;

        await handler.ModifyFromFileReplacementSourceAsync(
            workspaceSummary,
            assetTab,
            details,
            busyStates.Add,
            _ => throw new InvalidOperationException("File modification should not fail in this test."));

        Assert.Equal(1, filePicker.ReplacementSourceFilePickCount);
        Assert.Single(gateway.FileReplacementCandidateQueries);
        Assert.Single(gateway.ReplaceAssetFromFileCommands);
        Assert.Equal(filePicker.ReplacementSourceFilePath, gateway.ReplaceAssetFromFileCommands[0].SourceFilePath);
        Assert.Null(assetTab.PendingFileReplacementCandidate);
        Assert.Equal([true, false], busyStates);
        Assert.Equal(0, reapplyCount);
    }

    private static WorkspaceSummaryDto CreateWorkspaceSummary() =>
        new(
            WorkspaceId: new WorkspaceId("workspace-1"),
            DisplayName: "Workspace",
            SourcePath: @"C:\Workspace",
            Mode: WorkspaceMode.SinglePane,
            ActivePaneId: PaneId.New(),
            BundleCount: 1,
            AssetCount: 2,
            OpenedAtUtc: DateTimeOffset.UtcNow,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: @"C:\WorkspaceData");

    private static AssetListEntryViewModel CreateAssetEntry(string assetId, string name) =>
        new(
            new AssetListItemDto(
                new AssetId(assetId),
                new BundleId("bundle-ui"),
                name,
                $"Assets/{name}",
                AssetType.Texture2D,
                SizeInBytes: 128,
                PreferredDisplaySizeInBytes: null,
                HasPreview: true,
                IsModified: false),
            "Texture2D",
            "128 B",
            "Original");

    private static SpineAssetGroupEntryViewModel CreateSpineGroupEntry(string groupId, string bundleDisplayName) =>
        new(
            new SpineAssetGroupDto(
                groupId,
                new BundleId("bundle-ui"),
                groupId,
                $"characters/{groupId}",
                SkeletonAssets:
                [
                    new SpineAssetGroupPartDto(
                        SpineAssetGroupPartRole.Skeleton,
                        new BundleId("bundle-ui"),
                        new AssetId($"{groupId}-json"),
                        bundleDisplayName,
                        $"{groupId}.json",
                        $"Assets/{groupId}.json",
                        AssetType.TextAsset,
                        "TextAsset")
                ],
                AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
                Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>()),
            "Skeleton 1 / Atlas 0",
            "0 pages",
            "Ready");

    private static AssetDetailsDto CreateAssetDetails() =>
        new(
            WorkspaceId: new WorkspaceId("workspace-1"),
            BundleId: new BundleId("bundle-ui"),
            AssetId: new AssetId("asset-1"),
            Name: "hero",
            AssetPath: "Assets/hero.png",
            ContainerPath: "Assets/hero.png",
            AssetType: AssetType.Texture2D,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: null,
            PathId: 1,
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>());

    private static InspectorInformationTextSet CreateInspectorTextSet() =>
        new(
            BasicInfoTitle: "Basic",
            PreviewInfoTitle: "Preview",
            PreviewDiagnosticTitle: "Diagnostic",
            AssetNameLabel: "Name",
            AssetTypeLabel: "Type",
            BundleLabel: "Bundle",
            ContainerPathLabel: "Path",
            SerializedFileLabel: "Serialized",
            AssetSizeLabel: "Size",
            PathIdLabel: "PathId",
            PreviewCacheStatusLabel: "Cache",
            PreviewFallbackReasonLabel: "Fallback",
            PreviewDiagnosticDetailLabel: "Detail",
            CopyToolTipText: "Copy",
            ItemCountFormat: "{0}");

    private sealed class RecordingFilePickerService : IFilePickerService
    {
        public string? BatchExtractionOutputDirectory { get; init; }

        public string? SpineGroupExtractionDirectory { get; init; }

        public string? ReplacementSourceFilePath { get; init; }

        public int BatchExtractionOutputDirectoryPickCount { get; private set; }

        public int SpineGroupExtractionDirectoryPickCount { get; private set; }

        public int ReplacementSourceFilePickCount { get; private set; }

        public Task<string?> PickWorkspaceDataDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<string>> PickBundleFilePathsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<string?> PickBundleImportDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickSpineFrameSequenceExportDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickSpineAssetGroupExtractionDirectoryAsync(CancellationToken cancellationToken = default)
        {
            SpineGroupExtractionDirectoryPickCount++;
            return Task.FromResult(SpineGroupExtractionDirectory);
        }

        public Task<string?> PickAssetExtractionOutputFileAsync(
            AssetType assetType,
            string? assetTypeDisplayName,
            string assetName,
            string? assetPath,
            string? containerPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickAssetBatchExtractionOutputDirectoryAsync(CancellationToken cancellationToken = default)
        {
            BatchExtractionOutputDirectoryPickCount++;
            return Task.FromResult(BatchExtractionOutputDirectory);
        }

        public Task<string?> PickReplacementSourceFileAsync(
            AssetType targetAssetType,
            CancellationToken cancellationToken = default)
        {
            ReplacementSourceFilePickCount++;
            return Task.FromResult(ReplacementSourceFilePath);
        }
    }

    private sealed class RecordingWorkspaceAssetOperationGateway : IWorkspaceAssetOperationGateway
    {
        public List<ExtractAssetCommand> ExtractAssetCommands { get; } = [];

        public List<ExtractSpineAssetGroupCommand> ExtractSpineAssetGroupCommands { get; } = [];

        public List<GetFileReplacementCandidateQuery> FileReplacementCandidateQueries { get; } = [];

        public List<ReplaceAssetFromFileCommand> ReplaceAssetFromFileCommands { get; } = [];

        public DateTimeOffset NextExtractedAtUtc { get; init; } = DateTimeOffset.UtcNow;

        public Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
            ExtractAssetCommand command,
            CancellationToken cancellationToken = default)
        {
            ExtractAssetCommands.Add(command);
            return Task.FromResult(Result<AssetExtractionResultDto>.Success(
                new AssetExtractionResultDto(
                    command.WorkspaceId,
                    command.BundleId,
                    command.AssetId,
                    command.AssetId.Value,
                    command.SuggestedOutputPath ?? string.Empty,
                    DateTimeOffset.UtcNow,
                    OutputPath: command.SuggestedOutputPath)));
        }

        public Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineAssetGroupAsync(
            ExtractSpineAssetGroupCommand command,
            CancellationToken cancellationToken = default)
        {
            ExtractSpineAssetGroupCommands.Add(command);
            var skeletonAssetId = new AssetId($"{command.GroupId}-json");
            var assetResult = new AssetExtractionResultDto(
                command.WorkspaceId,
                command.BundleId,
                skeletonAssetId,
                skeletonAssetId.Value,
                Path.Combine(command.OutputDirectory, $"{command.GroupId}.json"),
                NextExtractedAtUtc,
                OutputPath: Path.Combine(command.OutputDirectory, $"{command.GroupId}.json"));
            return Task.FromResult(Result<SpineAssetGroupExtractionResultDto>.Success(
                new SpineAssetGroupExtractionResultDto(
                    command.WorkspaceId,
                    command.BundleId,
                    command.GroupId,
                    command.GroupId,
                    command.OutputDirectory,
                    ExtractedAssetCount: 3,
                    FailedAssetCount: 0,
                    NextExtractedAtUtc,
                    AssetResults: [assetResult],
                    PlannedParts: Array.Empty<SpineAssetGroupPartDto>())));
        }

        public Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
            GetFileReplacementCandidateQuery query,
            CancellationToken cancellationToken = default)
        {
            FileReplacementCandidateQueries.Add(query);
            return Task.FromResult(Result<FileReplacementCandidateDto>.Success(
                new FileReplacementCandidateDto(
                    SourceFilePath: query.SourceFilePath,
                    SourceFileName: Path.GetFileName(query.SourceFilePath),
                    SourceAssetType: AssetType.Texture2D)));
        }

        public Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
            GetWorkspaceReplacementCandidatesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
            ReplaceAssetFromFileCommand command,
            CancellationToken cancellationToken = default)
        {
            ReplaceAssetFromFileCommands.Add(command);
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Success(
                new AssetFileReplacementResultDto(
                    command.WorkspaceId,
                    command.TargetBundleId,
                    command.TargetAssetId,
                    "hero",
                    command.SourceFilePath,
                    Path.GetFileName(command.SourceFilePath),
                    command.SourceAssetType,
                    DateTimeOffset.UtcNow)));
        }

        public Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
            ReplaceAssetFromWorkspaceCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
            UndoAssetReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementRedoResultDto>> RedoAssetReplacementAsync(
            RedoAssetReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetSpriteEditAppliedResultDto>> ApplySpriteEditAsync(
            ApplySpriteEditCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceOperationRecordDeletedResultDto>> DeleteWorkspaceOperationRecordAsync(
            DeleteWorkspaceOperationRecordCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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

        public List<string?> IndeterminateReports { get; } = [];

        public void ReportProgress(int completedCount, int totalCount, string? detail = null)
        {
            ProgressReports.Add(new ProgressReport(completedCount, totalCount, detail));
        }

        public void ReportIndeterminate(string? detail = null)
        {
            IndeterminateReports.Add(detail);
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed record ProgressReport(int CompletedCount, int TotalCount, string? Detail);

    private sealed class TestPathNavigationService : IPathNavigationService
    {
        public bool TryOpenContainingFolder(string path) => false;
    }

    private sealed class TestWorkspaceShellHost : IBundleExplorerWorkspaceShellHost
    {
        public TestWorkspaceShellHost(WorkspaceSummaryDto workspaceSummary)
        {
            WorkspaceSummary = workspaceSummary;
        }

        public WorkspaceSummaryDto? WorkspaceSummary { get; }

        public WorkspaceNavigationRequest? ActiveWorkspaceNavigationRequest => null;
    }
}
