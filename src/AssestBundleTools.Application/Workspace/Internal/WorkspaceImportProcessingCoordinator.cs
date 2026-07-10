using System.Diagnostics;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 统一协调工作区导入来源到索引缓存的处理流程，供导入命令与工作区打开链路复用。
/// </summary>
internal sealed class WorkspaceImportProcessingCoordinator
{
    private readonly IWorkspaceImportExecutionGate _workspaceImportExecutionGate;
    private readonly WorkspaceImportProcessingPlanBuilder _workspaceImportProcessingPlanBuilder;
    private readonly WorkspaceImportWorkerPool _workspaceImportWorkerPool;
    private readonly WorkspaceImportCommitter _workspaceImportCommitter;

    public WorkspaceImportProcessingCoordinator(
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceImportSourceDiscoveryService workspaceImportSourceDiscoveryService,
        IWorkspaceImportFileSystemService workspaceImportFileSystemService,
        IWorkspaceImportExecutionGate workspaceImportExecutionGate,
        IWorkspaceImportExecutionOptionsResolver workspaceImportExecutionOptionsResolver,
        IAssetBundleSourceInspector assetBundleSourceInspector,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        IAssetBundleReader assetBundleReader,
        IBundleArtifactSessionInvalidator? bundleArtifactSessionInvalidator = null,
        IBundleArtifactCachePrunePolicy? bundleArtifactCachePrunePolicy = null)
    {
        _workspaceImportExecutionGate = workspaceImportExecutionGate ?? throw new ArgumentNullException(nameof(workspaceImportExecutionGate));
        var sessionInvalidator = bundleArtifactSessionInvalidator ?? NoOpBundleArtifactSessionInvalidator.Instance;
        var prunePolicy = bundleArtifactCachePrunePolicy ?? NoOpBundleArtifactCachePrunePolicy.Instance;
        var screeningCoordinator = new WorkspaceImportCandidateScreeningCoordinator(
            assetBundleSourceInspector ?? throw new ArgumentNullException(nameof(assetBundleSourceInspector)),
            assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry)));
        _workspaceImportProcessingPlanBuilder = new WorkspaceImportProcessingPlanBuilder(
            workspaceMetadataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory)),
            workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory)),
            workspaceImportSourceDiscoveryService ?? throw new ArgumentNullException(nameof(workspaceImportSourceDiscoveryService)),
            screeningCoordinator,
            workspaceImportExecutionOptionsResolver ?? throw new ArgumentNullException(nameof(workspaceImportExecutionOptionsResolver)),
            sessionInvalidator,
            prunePolicy);
        _workspaceImportWorkerPool = new WorkspaceImportWorkerPool(
            new WorkspaceImportBundleWorker(
                workspaceImportFileSystemService ?? throw new ArgumentNullException(nameof(workspaceImportFileSystemService)),
                assetBundleReader ?? throw new ArgumentNullException(nameof(assetBundleReader))));
        _workspaceImportCommitter = new WorkspaceImportCommitter(sessionInvalidator);
    }

    public async Task<Result<WorkspaceImportProcessingResultDto>> ProcessAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceImportProcessingResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return Result<WorkspaceImportProcessingResultDto>.Failure(ApplicationErrors.WorkspaceImportWorkspaceKeyRequired());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return Result<WorkspaceImportProcessingResultDto>.Failure(ApplicationErrors.WorkspaceImportDataRootPathRequired());
        }

        try
        {
            return await _workspaceImportExecutionGate.ExecuteExclusiveAsync(
                command.WorkspaceKey,
                gateCancellationToken => ProcessCoreAsync(command, gateCancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceImportProcessingResultDto>.Cancelled();
        }
    }

    private async Task<Result<WorkspaceImportProcessingResultDto>> ProcessCoreAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken)
    {
        command.Progress?.Report(new WorkspaceImportProgressDto(
            WorkspaceImportProgressStage.Planning,
            CompletedBundleCount: 0,
            TotalBundleCount: 0,
            CurrentBundleLogicalPath: null));

        var totalStopwatch = Stopwatch.StartNew();
        var planningStopwatch = Stopwatch.StartNew();
        var planResult = await _workspaceImportProcessingPlanBuilder.BuildAsync(command, cancellationToken);
        planningStopwatch.Stop();
        if (!planResult.IsSuccess || planResult.Value is null)
        {
            return Result<WorkspaceImportProcessingResultDto>.Failure(planResult.Errors);
        }

        var workerStopwatch = Stopwatch.StartNew();
        var workerResults = await _workspaceImportWorkerPool.ExecuteAsync(planResult.Value, cancellationToken);
        workerStopwatch.Stop();

        command.Progress?.Report(new WorkspaceImportProgressDto(
            WorkspaceImportProgressStage.Committing,
            CompletedBundleCount: planResult.Value.DecryptorResolution.BundleCandidates.Count,
            TotalBundleCount: planResult.Value.DecryptorResolution.BundleCandidates.Count,
            CurrentBundleLogicalPath: null));

        var commitStopwatch = Stopwatch.StartNew();
        var processingResult = _workspaceImportCommitter.Commit(planResult.Value, workerResults);
        commitStopwatch.Stop();
        totalStopwatch.Stop();

        var performanceDiagnostic = new WorkspaceImportPerformanceDiagnosticDto(
            TotalDurationMs: totalStopwatch.ElapsedMilliseconds,
            PlanningDurationMs: planningStopwatch.ElapsedMilliseconds,
            ScreeningDurationMs: planResult.Value.ScreeningDurationMs,
            ProbeDurationMs: planResult.Value.DecryptorResolution.ProbeTelemetry.DurationMs,
            WorkerDurationMs: workerStopwatch.ElapsedMilliseconds,
            CommitDurationMs: commitStopwatch.ElapsedMilliseconds,
            BundleWorkerCount: planResult.Value.ExecutionOptions.BundleWorkerCount,
            ProbeDecryptorCount: planResult.Value.DecryptorResolution.ProbeTelemetry.DecryptorCount,
            ProbeEvaluationCount: planResult.Value.DecryptorResolution.ProbeTelemetry.FileEvaluationCount);
        return Result<WorkspaceImportProcessingResultDto>.Success(processingResult with
        {
            PerformanceDiagnostic = performanceDiagnostic
        });
    }
}
