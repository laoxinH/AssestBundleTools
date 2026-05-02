using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class WorkspaceImportCoordinatorServiceTests
{
    [Fact]
    public async Task RequestDragDropImportAsync_ShouldAppendPerformanceDiagnosticLog_WhenProcessingResultProvidesTimings()
    {
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var notificationService = new TestAppNotificationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var navigationRequest = PresentationTestDataFactory.CreateNavigationRequest("ImportPerformance");
        var workspaceSummary = PresentationTestDataFactory.CreateWorkspaceSummary(navigationRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            navigationRequest.WorkspaceDataRootPath,
            navigationRequest.AssetBundleDecryptorId);
        var service = new WorkspaceImportCoordinatorService(
            workspaceContextService,
            new SuccessfulImportWorkspaceExplorerGateway(workspaceSummary.DisplayName),
            new StubFilePickerService(),
            appLogService,
            notificationService,
            localizationService,
            new InlineBackgroundTaskRunner());

        var result = await service.RequestDragDropImportAsync(
            [@"C:\Bundles\hero.ab"]);

        Assert.True(result);
        Assert.Contains(
            appLogService.InfoMessages,
            message => string.Equals(
                message,
                $"{ResourceKeys.WorkspaceImportLogPerformanceFormat}:2|120|30|12|70|8",
                StringComparison.Ordinal));
        Assert.Contains(
            appLogService.InfoMessages,
            message => string.Equals(
                message,
                $"{ResourceKeys.WorkspaceImportLogProbePerformanceFormat}:12|1|1",
                StringComparison.Ordinal));
    }

    private sealed class SuccessfulImportWorkspaceExplorerGateway : IWorkspaceExplorerGateway
    {
        private readonly string _workspaceDisplayName;

        public SuccessfulImportWorkspaceExplorerGateway(string workspaceDisplayName)
        {
            _workspaceDisplayName = workspaceDisplayName;
        }

        public Task<Result<WorkspaceSummaryDto>> OpenWorkspaceAsync(
            OpenWorkspaceCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceImportRegistrationResultDto>> RegisterWorkspaceImportAsync(
            RegisterWorkspaceImportCommand command,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<WorkspaceImportRegistrationResultDto>.Success(
                new WorkspaceImportRegistrationResultDto(
                    WorkspaceKey: command.WorkspaceKey,
                    WorkspaceDisplayName: _workspaceDisplayName,
                    ImportOrigin: command.ImportOrigin,
                    RequestedPathCount: command.Paths.Count,
                    AddedPathCount: command.Paths.Count,
                    UpdatedPathCount: 0,
                    FileCount: command.Paths.Count,
                    DirectoryCount: 0,
                    TotalRegisteredSourceCount: command.Paths.Count,
                    RecordedAtUtc: DateTimeOffset.UtcNow)));
        }

        public Task<Result<WorkspaceImportProcessingResultDto>> ProcessWorkspaceImportsAsync(
            ProcessWorkspaceImportsCommand command,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result<WorkspaceImportProcessingResultDto>.Success(
                new WorkspaceImportProcessingResultDto(
                    WorkspaceKey: command.WorkspaceKey,
                    WorkspaceDisplayName: _workspaceDisplayName,
                    ResolvedDecryptorId: "builtin.pass_through",
                    UsedAutoDetection: false,
                    AutoDetectionDiagnostic: null,
                    RegisteredSourceCount: 1,
                    DiscoveredFileCount: 1,
                    ScreenedOutFileCount: 0,
                    IndexedBundleCount: 1,
                    ReusedBundleCount: 0,
                    SkippedFileCount: 0,
                    FailedFileCount: 0,
                    CacheDiagnostics: Array.Empty<WorkspaceImportCacheDecisionDiagnosticDto>(),
                    IssueDiagnostics: Array.Empty<WorkspaceImportIssueDiagnosticDto>(),
                    ProcessedAtUtc: DateTimeOffset.UtcNow,
                    PerformanceDiagnostic: new WorkspaceImportPerformanceDiagnosticDto(
                        TotalDurationMs: 120,
                        PlanningDurationMs: 30,
                        ScreeningDurationMs: 12,
                        ProbeDurationMs: 12,
                        WorkerDurationMs: 70,
                        CommitDurationMs: 8,
                        BundleWorkerCount: 2,
                        ProbeDecryptorCount: 1,
                        ProbeEvaluationCount: 1))));
        }

        public Task<Result<WorkspaceBundleRemovalResultDto>> RemoveWorkspaceBundleAsync(
            RemoveWorkspaceBundleCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<BundleTreeNodeDto>>> LoadBundleTreeAsync(
            LoadBundleTreeQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetListResultDto>> LoadAssetListAsync(
            LoadAssetListQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubFilePickerService : IFilePickerService
    {
        public Task<string?> PickWorkspaceDataDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<string>> PickBundleFilePathsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<string?> PickBundleImportDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickReplacementSourceFileAsync(
            AssetType targetAssetType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class InlineBackgroundTaskRunner : IBackgroundTaskRunner
    {
        public Task<TResult> RunAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            return operation(cancellationToken);
        }
    }
}
