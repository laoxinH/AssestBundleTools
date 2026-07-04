using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Saving.Commands;

/// <summary>
/// 执行“另存为新包”。
/// </summary>
/// <remarks>
/// 当组合根尚未提供真实写出端口时仍返回计划摘要；
/// 提供写出端口后，按保存计划并发写出 AB 文件并持久化导出清单。
/// </remarks>
public sealed class SaveWorkspaceAsNewBundleHandler : ICommandHandler<SaveWorkspaceAsNewBundleCommand, Result<WorkspaceSaveExecutionResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly WorkspaceSaveArtifactInputResolver? _artifactInputResolver;
    private readonly IWorkspaceSaveOutputProbe? _outputProbe;
    private readonly IWorkspaceBundleExporter? _bundleExporter;
    private readonly IWorkspaceBundleExportManifestStore? _manifestStore;
    private readonly WorkspaceSaveResourceProcessorRunner _resourceProcessorRunner;
    private readonly IWorkspaceDataStoreFactory? _workspaceDataStoreFactory;

    public SaveWorkspaceAsNewBundleHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory? workspaceDataStoreFactory = null,
        IWorkspaceSaveOutputProbe? outputProbe = null,
        IWorkspaceBundleExporter? bundleExporter = null,
        IWorkspaceBundleExportManifestStore? manifestStore = null,
        IResourceProcessorCatalogStore? resourceProcessorCatalogStore = null,
        IWorkspaceResourceProcessorExecutor? resourceProcessorExecutor = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory;
        _artifactInputResolver = workspaceDataStoreFactory is null
            ? null
            : new WorkspaceSaveArtifactInputResolver(workspaceDataStoreFactory);
        _outputProbe = outputProbe;
        _bundleExporter = bundleExporter;
        _manifestStore = manifestStore;
        _resourceProcessorRunner = new WorkspaceSaveResourceProcessorRunner(
            resourceProcessorCatalogStore,
            resourceProcessorExecutor);
    }

    public async Task<Result<WorkspaceSaveExecutionResultDto>> HandleAsync(
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId));
        }

        var plan = WorkspaceSavePlanner.BuildPlan(
            _workspaceSessionRegistry,
            command.WorkspaceId,
            command.OutputDirectory,
            _artifactInputResolver,
            _outputProbe,
            command.WorkspaceKey,
            command.WorkspaceDataRootPath,
            command.OutputConflictPolicy);
        if (plan.BundlePlans.Count == 0)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(ApplicationErrors.NoSaveCandidates(command.WorkspaceId));
        }

        if (!TrySelectBundlePlans(plan, command.SelectedBundleIds, out var selectedPlans, out var selectionErrors))
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(selectionErrors);
        }

        if (_bundleExporter is null)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Success(
                CreateSummaryResult(plan, selectedPlans, DateTimeOffset.UtcNow));
        }

        var validationErrors = ValidateSelectedPlansForWrite(selectedPlans);
        if (validationErrors.Count > 0)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(validationErrors);
        }

        command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
            WorkspaceSaveExecutionStageDto.Planning,
            CompletedBundleCount: 0,
            TotalBundleCount: selectedPlans.Count));

        var operationId = $"save-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var processorSelections = NormalizeProcessorSelections(command.ProcessorSelections);
        var resourceProcessorPlanResult = _resourceProcessorRunner.BuildPlan(processorSelections);
        if (resourceProcessorPlanResult.IsFailure)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Failure(resourceProcessorPlanResult.Errors);
        }

        var resourceProcessorPlan = resourceProcessorPlanResult.Value ?? WorkspaceResourceProcessorPlan.Empty;
        IReadOnlyList<WorkspaceSaveBundleExecutionResultDto> bundleResults;
        try
        {
            var processorResult = await _resourceProcessorRunner.ExecuteAsync(
                plan,
                selectedPlans,
                resourceProcessorPlan,
                new WorkspaceResourceProcessorExecutionContext(
                    OperationId: operationId,
                    WorkspaceKey: command.WorkspaceKey,
                    WorkspaceDataRootPath: command.WorkspaceDataRootPath,
                    Progress: command.Progress),
                cancellationToken);
            if (processorResult.IsCancelled)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
            }

            if (processorResult.IsFailure)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Failure(processorResult.Errors);
            }

            bundleResults = await ExportBundlesAsync(
                plan,
                selectedPlans,
                processorSelections,
                processorResult.Value ?? WorkspaceResourceProcessorBatchResult.Empty,
                operationId,
                command,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
        }
        var successfulResults = bundleResults
            .Where(static result => result.Status == WorkspaceSaveBundleExecutionStatusDto.Succeeded)
            .ToArray();

        string? manifestPath = null;
        if (successfulResults.Length > 0 && _manifestStore is not null)
        {
            command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
                WorkspaceSaveExecutionStageDto.ManifestWriting,
                CompletedBundleCount: bundleResults.Count,
                TotalBundleCount: selectedPlans.Count,
                SucceededBundleCount: successfulResults.Length,
                FailedBundleCount: bundleResults.Count - successfulResults.Length));

            var manifestResult = await _manifestStore.SaveAsync(
                new WorkspaceBundleExportManifestSaveRequestDto(
                    WorkspaceId: plan.WorkspaceId,
                    WorkspaceKey: command.WorkspaceKey,
                    WorkspaceDisplayName: plan.WorkspaceDisplayName,
                    OutputDirectory: plan.OutputDirectory,
                    ExportedAtUtc: DateTimeOffset.UtcNow,
                    BundleResults: bundleResults,
                    ProcessorSelections: processorSelections),
                cancellationToken);
            if (manifestResult.IsCancelled)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Cancelled();
            }

            if (manifestResult.IsFailure)
            {
                return Result<WorkspaceSaveExecutionResultDto>.Failure(manifestResult.Errors);
            }

            manifestPath = manifestResult.Value?.ManifestPath;
        }

        var completedAtUtc = DateTimeOffset.UtcNow;
        command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
            WorkspaceSaveExecutionStageDto.Completed,
            CompletedBundleCount: bundleResults.Count,
            TotalBundleCount: selectedPlans.Count,
            SucceededBundleCount: successfulResults.Length,
            FailedBundleCount: bundleResults.Count - successfulResults.Length));

        return Result<WorkspaceSaveExecutionResultDto>.Success(
            CreateExecutionResult(plan, selectedPlans, bundleResults, manifestPath, completedAtUtc));
    }

    private async Task<IReadOnlyList<WorkspaceSaveBundleExecutionResultDto>> ExportBundlesAsync(
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
        var tasks = selectedPlans.Select(async selectedPlan =>
        {
            if (processorBatchResult.FailedBundleDiagnostics.TryGetValue(selectedPlan.BundleId.Value, out var processorFailure))
            {
                Interlocked.Increment(ref failedCount);
                Interlocked.Increment(ref completedCount);
                return CreateFailedBundleResult(
                    selectedPlan,
                    processorFailure,
                    WorkspaceSaveResourceProcessorRunner.SummarizeResults(
                        processorBatchResult.ResolveResults(selectedPlan.BundleId)));
            }

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
                    WorkspaceSaveExecutionStageDto.BundleWriting,
                    CompletedBundleCount: Volatile.Read(ref completedCount),
                    TotalBundleCount: selectedPlans.Count,
                    CurrentBundleDisplayName: selectedPlan.BundleDisplayName,
                    SucceededBundleCount: Volatile.Read(ref succeededCount),
                    FailedBundleCount: Volatile.Read(ref failedCount)));

                var exportResult = await _bundleExporter!.ExportAsync(
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
                        OriginalCompression: selectedPlan.OriginalCompression),
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

                return bundleResult;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref failedCount);
                return CreateFailedBundleResult(
                    selectedPlan,
                    ApplicationErrors.BundleExportWriteFailed(selectedPlan.OutputFilePath, exception.GetType().Name));
            }
            finally
            {
                var completed = Interlocked.Increment(ref completedCount);
                command.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
                    WorkspaceSaveExecutionStageDto.BundleWriting,
                    CompletedBundleCount: completed,
                    TotalBundleCount: selectedPlans.Count,
                    CurrentBundleDisplayName: selectedPlan.BundleDisplayName,
                    SucceededBundleCount: Volatile.Read(ref succeededCount),
                    FailedBundleCount: Volatile.Read(ref failedCount)));
                semaphore.Release();
            }
        });

        var unorderedResults = await Task.WhenAll(tasks);
        var orderByBundleId = selectedPlans
            .Select((selectedPlan, index) => (selectedPlan.BundleId.Value, Index: index))
            .ToDictionary(static item => item.Value, static item => item.Index, StringComparer.Ordinal);
        return unorderedResults
            .OrderBy(result => orderByBundleId.TryGetValue(result.BundleId.Value, out var index) ? index : int.MaxValue)
            .ToArray();
    }

    private static WorkspaceSaveExecutionResultDto CreateSummaryResult(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        DateTimeOffset completedAtUtc)
    {
        var selectedPlan = CreatePlanSnapshot(plan, selectedPlans);
        return new WorkspaceSaveExecutionResultDto(
            WorkspaceId: plan.WorkspaceId,
            WorkspaceDisplayName: plan.WorkspaceDisplayName,
            OutputDirectory: plan.OutputDirectory,
            PlannedBundleCount: selectedPlans.Count,
            TotalChangeCount: selectedPlans.Sum(static bundle => bundle.ChangeCount),
            CompletedAtUtc: completedAtUtc,
            ResolvedArtifactInputCount: selectedPlans.Count(static bundle => bundle.ArtifactInput.HasStandardizedArtifact),
            UnresolvedArtifactInputCount: selectedPlans.Count(static bundle => !bundle.ArtifactInput.HasStandardizedArtifact),
            OutputReadyBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.Ready),
            OutputBlockedBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.IsWriteBlocked),
            OutputUnprobedBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.NotProbed),
            IsReadyForWrite: selectedPlan.IsReadyForWrite,
            BundleResults: Array.Empty<WorkspaceSaveBundleExecutionResultDto>());
    }

    private static WorkspaceSaveExecutionResultDto CreateExecutionResult(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        IReadOnlyList<WorkspaceSaveBundleExecutionResultDto> bundleResults,
        string? manifestPath,
        DateTimeOffset completedAtUtc)
    {
        var succeededCount = bundleResults.Count(static result => result.Status == WorkspaceSaveBundleExecutionStatusDto.Succeeded);
        var failedCount = bundleResults.Count - succeededCount;
        var selectedPlan = CreatePlanSnapshot(plan, selectedPlans);
        var status = succeededCount == selectedPlans.Count
            ? WorkspaceSaveExecutionStatusDto.Succeeded
            : succeededCount == 0
                ? WorkspaceSaveExecutionStatusDto.Failed
                : WorkspaceSaveExecutionStatusDto.PartiallySucceeded;

        return new WorkspaceSaveExecutionResultDto(
            WorkspaceId: plan.WorkspaceId,
            WorkspaceDisplayName: plan.WorkspaceDisplayName,
            OutputDirectory: plan.OutputDirectory,
            PlannedBundleCount: selectedPlans.Count,
            TotalChangeCount: selectedPlans.Sum(static bundle => bundle.ChangeCount),
            CompletedAtUtc: completedAtUtc,
            ResolvedArtifactInputCount: selectedPlans.Count(static bundle => bundle.ArtifactInput.HasStandardizedArtifact),
            UnresolvedArtifactInputCount: selectedPlans.Count(static bundle => !bundle.ArtifactInput.HasStandardizedArtifact),
            OutputReadyBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.Ready),
            OutputBlockedBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.IsWriteBlocked),
            OutputUnprobedBundleCount: selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.NotProbed),
            IsReadyForWrite: selectedPlan.IsReadyForWrite,
            Status: status,
            SucceededBundleCount: succeededCount,
            FailedBundleCount: failedCount,
            WrittenBundleCount: succeededCount,
            ExportManifestPath: manifestPath,
            BundleResults: bundleResults);
    }

    private static WorkspaceSavePlan CreatePlanSnapshot(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans)
    {
        var unresolvedArtifactInputCount = selectedPlans.Count(static bundle => !bundle.ArtifactInput.HasStandardizedArtifact);
        var outputBlockedBundleCount = selectedPlans.Count(static bundle => bundle.OutputPreflight.IsWriteBlocked);
        var outputUnprobedBundleCount = selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.NotProbed);
        return plan with
        {
            ResolvedArtifactInputCount = selectedPlans.Count(static bundle => bundle.ArtifactInput.HasStandardizedArtifact),
            UnresolvedArtifactInputCount = unresolvedArtifactInputCount,
            OutputReadyBundleCount = selectedPlans.Count(static bundle => bundle.OutputPreflight.Readiness == WorkspaceSaveOutputReadiness.Ready),
            OutputBlockedBundleCount = outputBlockedBundleCount,
            OutputUnprobedBundleCount = outputUnprobedBundleCount,
            IsReadyForWrite = unresolvedArtifactInputCount == 0 &&
                              outputBlockedBundleCount == 0 &&
                              outputUnprobedBundleCount == 0
        };
    }

    private static bool TrySelectBundlePlans(
        WorkspaceSavePlan plan,
        IReadOnlyList<Domain.Bundles.BundleId>? selectedBundleIds,
        out IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        out IReadOnlyList<Domain.Common.Errors.AppError> errors)
    {
        if (selectedBundleIds is null)
        {
            selectedPlans = plan.BundlePlans;
            errors = Array.Empty<Domain.Common.Errors.AppError>();
            return true;
        }

        var selectedIdValues = selectedBundleIds
            .Where(static bundleId => !string.IsNullOrWhiteSpace(bundleId.Value))
            .Select(static bundleId => bundleId.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (selectedIdValues.Length == 0)
        {
            selectedPlans = Array.Empty<PlannedWorkspaceBundleSave>();
            errors = [ApplicationErrors.NoSaveCandidates(plan.WorkspaceId)];
            return false;
        }

        var selectedIdSet = selectedIdValues.ToHashSet(StringComparer.Ordinal);
        selectedPlans = plan.BundlePlans
            .Where(plan => selectedIdSet.Contains(plan.BundleId.Value))
            .ToArray();
        var foundIdSet = selectedPlans
            .Select(static selectedPlan => selectedPlan.BundleId.Value)
            .ToHashSet(StringComparer.Ordinal);
        errors = selectedIdValues
            .Where(selectedId => !foundIdSet.Contains(selectedId))
            .Select(selectedId => ApplicationErrors.BundleNotFound(new Domain.Bundles.BundleId(selectedId)))
            .ToArray();
        return errors.Count == 0;
    }

    private static IReadOnlyList<Domain.Common.Errors.AppError> ValidateSelectedPlansForWrite(
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans)
    {
        var errors = new List<Domain.Common.Errors.AppError>();
        foreach (var selectedPlan in selectedPlans)
        {
            if (!selectedPlan.ArtifactInput.HasStandardizedArtifact ||
                string.IsNullOrWhiteSpace(selectedPlan.ArtifactInput.ArtifactBundlePath))
            {
                errors.Add(ApplicationErrors.BundleExportArtifactMissing(
                    selectedPlan.BundleDisplayName,
                    selectedPlan.ArtifactInput.ArtifactDiagnosticCode));
            }

            if (selectedPlan.OutputPreflight.IsWriteBlocked)
            {
                errors.Add(ApplicationErrors.BundleExportOutputBlocked(
                    selectedPlan.OutputFilePath,
                    selectedPlan.OutputPreflight.DiagnosticCode));
            }
        }

        return errors;
    }

    private static IReadOnlyList<WorkspaceExportProcessorSelectionDto> NormalizeProcessorSelections(
        IReadOnlyList<WorkspaceExportProcessorSelectionDto>? processorSelections)
    {
        if (processorSelections is null || processorSelections.Count == 0)
        {
            return Array.Empty<WorkspaceExportProcessorSelectionDto>();
        }

        return processorSelections
            .Where(static selection => !string.IsNullOrWhiteSpace(selection.ResourceKind))
            .GroupBy(
                static selection => selection.ResourceKind.Trim().ToLowerInvariant(),
                StringComparer.Ordinal)
            .Select(static group =>
            {
                var selection = group.Last();
                return new WorkspaceExportProcessorSelectionDto(
                    group.Key,
                    string.IsNullOrWhiteSpace(selection.ProcessorId) ? null : selection.ProcessorId.Trim());
            })
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
            WrittenCompression: exportResult.WrittenCompression);

    private static WorkspaceSaveBundleExecutionResultDto CreateFailedBundleResult(
        PlannedWorkspaceBundleSave selectedPlan,
        Domain.Common.Errors.AppError? error,
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
