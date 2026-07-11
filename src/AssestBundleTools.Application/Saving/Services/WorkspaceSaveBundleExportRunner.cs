using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using System.Diagnostics;

namespace AssestBundleTools.Application.Saving.Services;

internal sealed class WorkspaceSaveBundleExportRunner
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceDataStoreFactory? _workspaceDataStoreFactory;
    private readonly IWorkspaceBundleExporter _bundleExporter;

    public WorkspaceSaveBundleExportRunner(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory? workspaceDataStoreFactory,
        IWorkspaceBundleExporter bundleExporter)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory;
        _bundleExporter = bundleExporter ?? throw new ArgumentNullException(nameof(bundleExporter));
    }

    public async Task<IReadOnlyList<WorkspaceSaveBundleExecutionResultDto>> ExportAsync(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        IReadOnlyList<WorkspaceExportProcessorSelectionDto> processorSelections,
        WorkspaceResourceProcessorBatchResult processorBatchResult,
        string operationId,
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken)
    {
        var concurrency = ResolveBundleWriteConcurrency(command.BundleWriteConcurrency, selectedPlans.Count);
        var assetWritePlanBuilder = new WorkspaceBundleAssetWritePlanBuilder(
            _workspaceSessionRegistry,
            plan.WorkspaceId,
            _workspaceDataStoreFactory);
        var semaphore = new SemaphoreSlim(concurrency, concurrency);
        var completedCount = 0;
        var succeededCount = 0;
        var failedCount = 0;
        var activeWriteCount = 0;
        command.Diagnostics?.Report(new WorkspaceSaveExecutionDiagnosticDto(
            WorkspaceSaveExecutionDiagnosticKindDto.BundleWritePlan,
            WorkspaceSaveExecutionStageDto.BundleWriting,
            TotalBundleCount: selectedPlans.Count,
            BundleWriteConcurrency: concurrency,
            ActiveBundleWriteCount: 0,
            CompletedBundleCount: 0));

        var tasks = selectedPlans.Select(selectedPlan => Task.Run(async () =>
        {
            if (processorBatchResult.FailedBundleDiagnostics.TryGetValue(selectedPlan.BundleId.Value, out var processorFailure))
            {
                Interlocked.Increment(ref failedCount);
                var skippedCompleted = Interlocked.Increment(ref completedCount);
                command.Diagnostics?.Report(new WorkspaceSaveExecutionDiagnosticDto(
                    WorkspaceSaveExecutionDiagnosticKindDto.BundleWriteSkipped,
                    WorkspaceSaveExecutionStageDto.BundleWriting,
                    TotalBundleCount: selectedPlans.Count,
                    BundleWriteConcurrency: concurrency,
                    ActiveBundleWriteCount: Volatile.Read(ref activeWriteCount),
                    CompletedBundleCount: skippedCompleted,
                    BundleId: selectedPlan.BundleId.Value,
                    BundleDisplayName: selectedPlan.BundleDisplayName,
                    OutputFilePath: selectedPlan.OutputFilePath,
                    Status: WorkspaceSaveBundleExecutionStatusDto.Failed.ToString(),
                    DiagnosticCode: processorFailure.Code));
                return CreateFailedBundleResult(
                    selectedPlan,
                    processorFailure,
                    WorkspaceSaveResourceProcessorRunner.SummarizeResults(
                        processorBatchResult.ResolveResults(selectedPlan.BundleId)));
            }

            await semaphore.WaitAsync(cancellationToken);
            var activeStarted = Interlocked.Increment(ref activeWriteCount);
            var elapsed = Stopwatch.StartNew();
            var status = "Unknown";
            try
            {
                command.Diagnostics?.Report(new WorkspaceSaveExecutionDiagnosticDto(
                    WorkspaceSaveExecutionDiagnosticKindDto.BundleWriteStarted,
                    WorkspaceSaveExecutionStageDto.BundleWriting,
                    TotalBundleCount: selectedPlans.Count,
                    BundleWriteConcurrency: concurrency,
                    ActiveBundleWriteCount: activeStarted,
                    CompletedBundleCount: Volatile.Read(ref completedCount),
                    BundleId: selectedPlan.BundleId.Value,
                    BundleDisplayName: selectedPlan.BundleDisplayName,
                    OutputFilePath: selectedPlan.OutputFilePath,
                    ManagedThreadId: Environment.CurrentManagedThreadId));

                command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
                    WorkspaceSaveExecutionStageDto.BundleWriting,
                    CompletedBundleCount: Volatile.Read(ref completedCount),
                    TotalBundleCount: selectedPlans.Count,
                    CurrentBundleDisplayName: selectedPlan.BundleDisplayName,
                    SucceededBundleCount: Volatile.Read(ref succeededCount),
                    FailedBundleCount: Volatile.Read(ref failedCount)));

                var exportResult = await _bundleExporter.ExportAsync(
                    new WorkspaceBundleExportRequestDto(
                        WorkspaceId: plan.WorkspaceId,
                        WorkspaceDisplayName: plan.WorkspaceDisplayName,
                        BundleId: selectedPlan.BundleId,
                        BundleDisplayName: selectedPlan.BundleDisplayName,
                        OutputFilePath: selectedPlan.OutputFilePath,
                        SourceArtifactPath: selectedPlan.ArtifactInput.ArtifactBundlePath!,
                        BundleCacheKey: selectedPlan.ArtifactInput.BundleCacheKey,
                        ChangeItems: selectedPlan.ChangeItems,
                        AssetWriteItems: assetWritePlanBuilder.Build(
                            selectedPlan,
                            command.WorkspaceKey,
                            command.WorkspaceDataRootPath,
                            processorBatchResult.ResolveResults(selectedPlan.BundleId)),
                        ProcessorSelections: processorSelections,
                        ProcessorResults: processorBatchResult.ResolveResults(selectedPlan.BundleId),
                        OperationId: operationId,
                        CompressionMode: command.CompressionMode,
                        OutputConflictPolicy: command.OutputConflictPolicy,
                        OriginalCompression: selectedPlan.OriginalCompression,
                        BundleSessionCacheKey: selectedPlan.ArtifactInput.BundleSessionCacheKey,
                        AudioWriteMode: command.AudioWriteMode,
                        WorkspaceKey: command.WorkspaceKey,
                        WorkspaceDataRootPath: command.WorkspaceDataRootPath,
                        SourceCodecId: command.UseSourceCodec
                            ? selectedPlan.ArtifactInput.SourceCodecId
                            : null,
                        SourceCodecVersion: command.UseSourceCodec
                            ? selectedPlan.ArtifactInput.SourceCodecVersion
                            : null,
                        SourceCodecParameters: command.UseSourceCodec
                            ? selectedPlan.ArtifactInput.SourceCodecParameters
                            : null),
                    cancellationToken);

                if (exportResult.IsCancelled)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                var bundleResult = exportResult.IsSuccess && exportResult.Value is not null
                    ? CreateSuccessfulBundleResult(selectedPlan, exportResult.Value)
                    : CreateFailedBundleResult(selectedPlan, exportResult.Errors.FirstOrDefault());

                if (bundleResult.Status == WorkspaceSaveBundleExecutionStatusDto.Succeeded)
                {
                    Interlocked.Increment(ref succeededCount);
                }
                else
                {
                    Interlocked.Increment(ref failedCount);
                }

                status = bundleResult.Status.ToString();
                return bundleResult;
            }
            catch (OperationCanceledException)
            {
                status = "Cancelled";
                throw;
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref failedCount);
                status = WorkspaceSaveBundleExecutionStatusDto.Failed.ToString();
                return CreateFailedBundleResult(
                    selectedPlan,
                    ApplicationErrors.BundleExportWriteFailed(selectedPlan.OutputFilePath, exception.GetType().Name));
            }
            finally
            {
                elapsed.Stop();
                var activeRemaining = Interlocked.Decrement(ref activeWriteCount);
                var completed = Interlocked.Increment(ref completedCount);
                command.Diagnostics?.Report(new WorkspaceSaveExecutionDiagnosticDto(
                    WorkspaceSaveExecutionDiagnosticKindDto.BundleWriteFinished,
                    WorkspaceSaveExecutionStageDto.BundleWriting,
                    TotalBundleCount: selectedPlans.Count,
                    BundleWriteConcurrency: concurrency,
                    ActiveBundleWriteCount: activeRemaining,
                    CompletedBundleCount: completed,
                    BundleId: selectedPlan.BundleId.Value,
                    BundleDisplayName: selectedPlan.BundleDisplayName,
                    OutputFilePath: selectedPlan.OutputFilePath,
                    ElapsedMilliseconds: elapsed.ElapsedMilliseconds,
                    Status: status));
                command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
                    WorkspaceSaveExecutionStageDto.BundleWriting,
                    CompletedBundleCount: completed,
                    TotalBundleCount: selectedPlans.Count,
                    CurrentBundleDisplayName: selectedPlan.BundleDisplayName,
                    SucceededBundleCount: Volatile.Read(ref succeededCount),
                    FailedBundleCount: Volatile.Read(ref failedCount)));
                semaphore.Release();
            }
        }, cancellationToken)).ToArray();

        var unorderedResults = await Task.WhenAll(tasks);
        var orderByBundleId = selectedPlans
            .Select((selectedPlan, index) => (selectedPlan.BundleId.Value, Index: index))
            .ToDictionary(static item => item.Value, static item => item.Index, StringComparer.Ordinal);
        return unorderedResults
            .OrderBy(result => orderByBundleId.TryGetValue(result.BundleId.Value, out var index) ? index : int.MaxValue)
            .ToArray();
    }

    private static WorkspaceSaveBundleExecutionResultDto CreateSuccessfulBundleResult(
        PlannedWorkspaceBundleSave selectedPlan,
        WorkspaceBundleExportResultDto exportResult) =>
        new(
            BundleId: selectedPlan.BundleId,
            BundleDisplayName: selectedPlan.BundleDisplayName,
            OutputFilePath: exportResult.OutputFilePath,
            Status: WorkspaceSaveBundleExecutionStatusDto.Succeeded,
            ChangeCount: selectedPlan.ChangeCount,
            ModifiedAssetCount: selectedPlan.ModifiedAssetCount,
            WorkspaceReplacementCount: selectedPlan.WorkspaceReplacementCount,
            FileReplacementCount: selectedPlan.FileReplacementCount,
            SourceArtifactPath: exportResult.SourceArtifactPath,
            BundleCacheKey: exportResult.BundleCacheKey,
            WrittenFileSizeInBytes: exportResult.WrittenFileSizeInBytes,
            WrittenFileHashAlgorithm: exportResult.WrittenFileHashAlgorithm,
            WrittenFileHash: exportResult.WrittenFileHash,
            WrittenFileLastWriteTimeUtc: exportResult.WrittenFileLastWriteTimeUtc,
            WrittenFileMd5: exportResult.WrittenFileMd5,
            WrittenFileSha256: exportResult.WrittenFileSha256,
            ProcessorResults: exportResult.ProcessorResults,
            CompressionMode: exportResult.CompressionMode,
            OriginalCompression: exportResult.OriginalCompression,
            WrittenCompression: exportResult.WrittenCompression,
            OutputCodecId: exportResult.OutputCodecId,
            OutputCodecVersion: exportResult.OutputCodecVersion);

    private static WorkspaceSaveBundleExecutionResultDto CreateFailedBundleResult(
        PlannedWorkspaceBundleSave selectedPlan,
        AppError? error,
        IReadOnlyList<WorkspaceBundleProcessorExecutionSummaryDto>? processorResults = null) =>
        new(
            BundleId: selectedPlan.BundleId,
            BundleDisplayName: selectedPlan.BundleDisplayName,
            OutputFilePath: selectedPlan.OutputFilePath,
            Status: WorkspaceSaveBundleExecutionStatusDto.Failed,
            ChangeCount: selectedPlan.ChangeCount,
            ModifiedAssetCount: selectedPlan.ModifiedAssetCount,
            WorkspaceReplacementCount: selectedPlan.WorkspaceReplacementCount,
            FileReplacementCount: selectedPlan.FileReplacementCount,
            SourceArtifactPath: selectedPlan.ArtifactInput.ArtifactBundlePath,
            BundleCacheKey: selectedPlan.ArtifactInput.BundleCacheKey,
            DiagnosticCode: error?.Code,
            DiagnosticDetail: error is null
                ? null
                : string.Join(
                    "; ",
                    error.Parameters
                        .Where(static parameter => parameter.Value is not null)
                        .Select(static parameter => $"{parameter.Key}={parameter.Value}")),
            ProcessorResults: processorResults);

    private static int ResolveBundleWriteConcurrency(int? requestedConcurrency, int selectedBundleCount)
    {
        if (selectedBundleCount <= 1)
        {
            return 1;
        }

        if (requestedConcurrency.HasValue)
        {
            return Math.Clamp(requestedConcurrency.Value, 1, selectedBundleCount);
        }

        return Math.Clamp(Environment.ProcessorCount / 2, 1, Math.Min(4, selectedBundleCount));
    }
}
