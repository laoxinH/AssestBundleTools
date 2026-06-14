using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 执行工作区级批量提取。
/// </summary>
public sealed class ExtractWorkspaceAssetsHandler : ICommandHandler<ExtractWorkspaceAssetsCommand, Result<WorkspaceBatchExtractionResultDto>>
{
    private static readonly TimeSpan WorkerSlotRetryDelay = TimeSpan.FromMilliseconds(25);

    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly ExtractAssetHandler? _extractAssetHandler;
    private readonly ExtractSpineAssetGroupHandler? _extractSpineAssetGroupHandler;
    private readonly IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? _loadSpineAssetGroupsHandler;
    private readonly WorkspaceExtractionOperationRecorder? _operationRecorder;
    private readonly IWorkspaceExportManifestStore? _exportManifestStore;
    private readonly WorkspaceBatchExtractionConcurrencyPolicy _concurrencyPolicy;

    public ExtractWorkspaceAssetsHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        ExtractAssetHandler? extractAssetHandler = null,
        WorkspaceExtractionOperationRecorder? operationRecorder = null,
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? loadSpineAssetGroupsHandler = null,
        ExtractSpineAssetGroupHandler? extractSpineAssetGroupHandler = null,
        IWorkspaceExportManifestStore? exportManifestStore = null,
        Func<int>? processorCountProvider = null,
        Func<bool>? memoryPressureEvaluator = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _extractAssetHandler = extractAssetHandler;
        _loadSpineAssetGroupsHandler = loadSpineAssetGroupsHandler;
        _extractSpineAssetGroupHandler = extractSpineAssetGroupHandler;
        _operationRecorder = operationRecorder;
        _exportManifestStore = exportManifestStore;
        _concurrencyPolicy = new WorkspaceBatchExtractionConcurrencyPolicy(
            processorCountProvider,
            memoryPressureEvaluator);
    }

    public async Task<Result<WorkspaceBatchExtractionResultDto>> HandleAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Result<WorkspaceBatchExtractionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Result<WorkspaceBatchExtractionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId));
        }

        var plan = WorkspaceBatchExtractionPlanner.BuildPlan(
            _workspaceSessionRegistry,
            command.WorkspaceId,
            command.AssetTypeFilter,
            command.OutputDirectory,
            selectedBundleIds: command.SelectedBundleIds,
            selectedCategories: command.SelectedCategories,
            selectedAssetIds: command.SelectedAssetIds);
        var spineGroups = await WorkspaceBatchExtractionSpinePlanner.BuildSpineGroupsAsync(
            _loadSpineAssetGroupsHandler,
            command.WorkspaceId,
            plan.SuggestedOutputDirectory,
            command.WorkspaceDataRootPath,
            plan.BundleScopes.Select(static scope => (scope.BundleId, scope.BundleDisplayName)),
            plan.SelectedCategories,
            cancellationToken);
        plan = plan with { SpineGroups = spineGroups };
        WorkspaceBatchExtractionDebugLog.Write(
            $"Plan ready. WorkspaceId={command.WorkspaceId.Value}; " +
            $"Assets={plan.Assets.Count}; SpineGroups={plan.SpineGroups.Count}; BundleScopes={plan.BundleScopes.Count}; " +
            $"SelectedCategories={WorkspaceBatchExtractionDebugLog.FormatCategories(plan.SelectedCategories)}; " +
            $"OutputDirectory={plan.SuggestedOutputDirectory}; ManagedThreadId={Environment.CurrentManagedThreadId}; " +
            $"TaskId={WorkspaceBatchExtractionDebugLog.FormatTaskId()}.");

        if (plan.Assets.Count == 0 && plan.SpineGroups.Count == 0)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Failure(
                ApplicationErrors.NoBatchExtractionCandidates(command.WorkspaceId));
        }

        BatchExtractionOutcome?[] outcomes;
        try
        {
            outcomes = await ExecutePlannedItemsAsync(command, plan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Cancelled();
        }

        if (outcomes.Any(static outcome => outcome?.IsCancelled == true))
        {
            return Result<WorkspaceBatchExtractionResultDto>.Cancelled();
        }

        var extractedResults = outcomes
            .Where(static outcome => outcome?.AssetResults is { Count: > 0 })
            .SelectMany(static outcome => outcome!.AssetResults!)
            .ToArray();
        var spineGroupResults = outcomes
            .Where(static outcome => outcome?.SpineGroupResult is not null)
            .Select(static outcome => outcome!.SpineGroupResult!)
            .ToArray();
        var failures = outcomes
            .Where(static outcome => outcome?.Errors is { Count: > 0 })
            .SelectMany(static outcome => outcome!.Errors!)
            .ToArray();

        if (extractedResults.Length == 0)
        {
            return failures.Length == 0
                ? Result<WorkspaceBatchExtractionResultDto>.Failure(
                    ApplicationErrors.NoBatchExtractionCandidates(command.WorkspaceId))
                : Result<WorkspaceBatchExtractionResultDto>.Failure(failures);
        }

        var affectedBundles = extractedResults
            .Select(static asset => asset.BundleId.Value)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var batchResult = new WorkspaceBatchExtractionResultDto(
            WorkspaceId: command.WorkspaceId,
            AssetTypeFilter: command.AssetTypeFilter,
            SuggestedOutputDirectory: plan.SuggestedOutputDirectory,
            ExtractedAssetCount: extractedResults.Length,
            ExtractedBundleCount: affectedBundles,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            FailedAssetCount: failures.Length,
            AssetResults: extractedResults,
            SelectedCategories: WorkspaceBatchExtractionPlanner.NormalizeForResult(
                command.AssetTypeFilter,
                command.SelectedCategories),
            ExtractedSpineGroupCount: spineGroupResults.Length,
            SpineGroupResults: spineGroupResults);

        var manifestResult = await TrySaveManifestAsync(command, plan, outcomes, batchResult, cancellationToken);
        if (manifestResult.IsCancelled)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Cancelled();
        }

        if (!manifestResult.IsSuccess)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Failure(manifestResult.Errors);
        }

        if (manifestResult.Value is not null)
        {
            batchResult = batchResult with
            {
                ExportManifestPath = manifestResult.Value.ManifestPath,
                ExportManifestItemCount = manifestResult.Value.Manifest.Items.Count
            };
        }

        _operationRecorder?.RecordBatchExtraction(command, batchResult);

        return Result<WorkspaceBatchExtractionResultDto>.Success(batchResult);
    }

    private async Task<Result<WorkspaceExportManifestSaveResultDto?>> TrySaveManifestAsync(
        ExtractWorkspaceAssetsCommand command,
        WorkspaceBatchExtractionPlan plan,
        IReadOnlyList<BatchExtractionOutcome?> outcomes,
        WorkspaceBatchExtractionResultDto batchResult,
        CancellationToken cancellationToken)
    {
        if (_exportManifestStore is null)
        {
            return Result<WorkspaceExportManifestSaveResultDto?>.Success(null);
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return Result<WorkspaceExportManifestSaveResultDto?>.Success(null);
        }

        var sourceItems = WorkspaceExportManifestSourceBuilder.Build(outcomes);
        if (sourceItems.Count == 0)
        {
            return Result<WorkspaceExportManifestSaveResultDto?>.Success(null);
        }

        var saveResult = await _exportManifestStore.SaveAsync(
            new WorkspaceExportManifestSaveRequestDto(
                WorkspaceKey: command.WorkspaceKey.Trim(),
                ExportedAtUtc: batchResult.CompletedAtUtc,
                OutputRoot: plan.SuggestedOutputDirectory,
                Items: sourceItems),
            cancellationToken);
        if (saveResult.IsCancelled)
        {
            return Result<WorkspaceExportManifestSaveResultDto?>.Cancelled();
        }

        return saveResult.IsSuccess && saveResult.Value is not null
            ? Result<WorkspaceExportManifestSaveResultDto?>.Success(saveResult.Value)
            : Result<WorkspaceExportManifestSaveResultDto?>.Failure(saveResult.Errors);
    }

    private async Task<BatchExtractionOutcome?[]> ExecutePlannedItemsAsync(
        ExtractWorkspaceAssetsCommand command,
        WorkspaceBatchExtractionPlan plan,
        CancellationToken cancellationToken)
    {
        var items = BuildIndexedWorkItems(plan);
        var outcomes = new BatchExtractionOutcome?[items.Count];
        var bundleGroups = items
            .GroupBy(static item => item.BundleId.Value, StringComparer.Ordinal)
            .Select(static group => new PlannedBundleExtractionGroup(
                group.Key,
                group.OrderBy(static item => item.Index).ToArray()))
            .OrderBy(static group => group.Items[0].Index)
            .ToArray();
        var progressState = new BatchExtractionProgressState(items.Count, bundleGroups.Length);

        var workerCount = _concurrencyPolicy.ResolveMaximumWorkerCount(bundleGroups.Length);
        WorkspaceBatchExtractionDebugLog.Write(
            $"Execution prepared. WorkspaceId={command.WorkspaceId.Value}; Items={items.Count}; " +
            $"BundleGroups={bundleGroups.Length}; MaxWorkers={workerCount}; " +
            $"Mode={(workerCount <= 1 ? "Sequential" : "Parallel")}; ManagedThreadId={Environment.CurrentManagedThreadId}; " +
            $"TaskId={WorkspaceBatchExtractionDebugLog.FormatTaskId()}.");
        ReportProgress(command, progressState.CreatePreparingProgress());
        if (workerCount <= 1)
        {
            foreach (var group in bundleGroups)
            {
                await ExtractBundleGroupAsync(
                    command,
                    group,
                    outcomes,
                    progressState,
                    workerOrdinal: null,
                    isMemoryThrottled: false,
                    cancellationToken);
            }

            return outcomes;
        }

        var nextIndex = new WorkerIndexBox();
        var workerActivity = new WorkerActivityBox();
        var tasks = Enumerable.Range(0, workerCount)
            .Select(workerOrdinal => Task.Run(
                () => ExecuteBundleWorkerLoopAsync(
                    command,
                    bundleGroups,
                    outcomes,
                    nextIndex,
                    workerActivity,
                    progressState,
                    workerOrdinal,
                    cancellationToken),
                cancellationToken))
            .ToArray();
        await Task.WhenAll(tasks);
        return outcomes;
    }

    private async Task ExecuteBundleWorkerLoopAsync(
        ExtractWorkspaceAssetsCommand command,
        IReadOnlyList<PlannedBundleExtractionGroup> bundleGroups,
        BatchExtractionOutcome?[] outcomes,
        WorkerIndexBox nextIndex,
        WorkerActivityBox workerActivity,
        BatchExtractionProgressState progressState,
        int workerOrdinal,
        CancellationToken cancellationToken)
    {
        WorkspaceBatchExtractionDebugLog.Write(
            $"Worker started. Worker={workerOrdinal}; ManagedThreadId={Environment.CurrentManagedThreadId}; " +
            $"TaskId={WorkspaceBatchExtractionDebugLog.FormatTaskId()}.");
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var slotLease = await AcquireWorkerSlotAsync(bundleGroups.Count, workerActivity, cancellationToken);

                PlannedBundleExtractionGroup? group = null;
                try
                {
                    var groupIndex = Interlocked.Increment(ref nextIndex.NextIndex);
                    if (groupIndex >= bundleGroups.Count)
                    {
                        return;
                    }

                    group = bundleGroups[groupIndex];
                    WorkspaceBatchExtractionDebugLog.Write(
                        $"Worker slot acquired. Worker={workerOrdinal}; BundleId={group.BundleId}; " +
                        $"ActiveWorkers={slotLease.ActiveWorkerCount}; CurrentLimit={slotLease.CurrentWorkerLimit}; " +
                        $"MaxWorkers={slotLease.MaximumWorkerCount}; MemoryThrottled={slotLease.IsMemoryThrottled}; " +
                        $"ManagedThreadId={Environment.CurrentManagedThreadId}; TaskId={WorkspaceBatchExtractionDebugLog.FormatTaskId()}.");
                    await ExtractBundleGroupAsync(
                        command,
                        group,
                        outcomes,
                        progressState,
                        workerOrdinal,
                        slotLease.IsMemoryThrottled,
                        cancellationToken);
                }
                finally
                {
                    ReleaseWorkerSlot(workerActivity);
                }
            }
        }
        finally
        {
            WorkspaceBatchExtractionDebugLog.Write(
                $"Worker stopped. Worker={workerOrdinal}; ManagedThreadId={Environment.CurrentManagedThreadId}; " +
                $"TaskId={WorkspaceBatchExtractionDebugLog.FormatTaskId()}.");
        }
    }

    private async Task<WorkerSlotLease> AcquireWorkerSlotAsync(
        int bundleGroupCount,
        WorkerActivityBox workerActivity,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (workerActivity.SyncRoot)
            {
                var maximumWorkerCount = _concurrencyPolicy.ResolveMaximumWorkerCount(bundleGroupCount);
                var currentLimit = _concurrencyPolicy.ResolveCurrentWorkerLimit(bundleGroupCount);
                if (workerActivity.ActiveWorkerCount < currentLimit)
                {
                    workerActivity.ActiveWorkerCount++;
                    return new WorkerSlotLease(
                        workerActivity.ActiveWorkerCount,
                        currentLimit,
                        maximumWorkerCount,
                        currentLimit < maximumWorkerCount);
                }
            }

            await Task.Delay(WorkerSlotRetryDelay, cancellationToken);
        }
    }

    private static void ReleaseWorkerSlot(WorkerActivityBox workerActivity)
    {
        lock (workerActivity.SyncRoot)
        {
            if (workerActivity.ActiveWorkerCount > 0)
            {
                workerActivity.ActiveWorkerCount--;
            }
        }
    }

    private async Task ExtractBundleGroupAsync(
        ExtractWorkspaceAssetsCommand command,
        PlannedBundleExtractionGroup group,
        BatchExtractionOutcome?[] outcomes,
        BatchExtractionProgressState progressState,
        int? workerOrdinal,
        bool isMemoryThrottled,
        CancellationToken cancellationToken)
    {
        var workerLabel = workerOrdinal.HasValue
            ? workerOrdinal.Value.ToString()
            : "sequential";
        var processedCount = 0;
        WorkspaceBatchExtractionDebugLog.Write(
            $"Bundle group started. Worker={workerLabel}; BundleId={group.BundleId}; Items={group.Items.Count}; " +
            $"ManagedThreadId={Environment.CurrentManagedThreadId}; TaskId={WorkspaceBatchExtractionDebugLog.FormatTaskId()}.");
        foreach (var item in group.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ReportProgress(command, progressState.CreateItemProgress(item, isMemoryThrottled));
            var outcome = item.Kind == BatchExtractionWorkItemKind.SpineGroup
                ? await ExtractSpineGroupAsync(command, item, cancellationToken)
                : await ExtractAssetAsync(command, item.Asset!, cancellationToken);
            if (outcome.IsCancelled)
            {
                outcomes[item.Index] = BatchExtractionOutcome.Cancelled();
                WorkspaceBatchExtractionDebugLog.Write(
                    $"Bundle group cancelled. Worker={workerLabel}; BundleId={group.BundleId}; ProcessedItems={processedCount}; " +
                    $"ManagedThreadId={Environment.CurrentManagedThreadId}; TaskId={WorkspaceBatchExtractionDebugLog.FormatTaskId()}.");
                return;
            }

            outcomes[item.Index] = outcome with { WorkItem = item };
            processedCount++;
            progressState.RecordOutcome(outcome);
            ReportProgress(command, progressState.CreateItemProgress(item, isMemoryThrottled));
        }

        WorkspaceBatchExtractionDebugLog.Write(
            $"Bundle group completed. Worker={workerLabel}; BundleId={group.BundleId}; ProcessedItems={processedCount}; " +
            $"ManagedThreadId={Environment.CurrentManagedThreadId}; TaskId={WorkspaceBatchExtractionDebugLog.FormatTaskId()}.");
    }

    private async Task<BatchExtractionOutcome> ExtractAssetAsync(
        ExtractWorkspaceAssetsCommand command,
        PlannedAssetExtraction asset,
        CancellationToken cancellationToken)
    {
        if (_extractAssetHandler is not null)
        {
            var handlerResult = await _extractAssetHandler.HandleAsync(
                new ExtractAssetCommand(
                    command.WorkspaceId,
                    asset.BundleId,
                    asset.AssetId,
                    command.WorkspaceKey,
                    command.WorkspaceDataRootPath,
                    asset.SuggestedOutputPath),
                cancellationToken);
            return ConvertAssetResult(handlerResult);
        }

        _workspaceSessionRegistry.TryExtractAsset(
            command.WorkspaceId,
            asset.BundleId,
            asset.AssetId,
            out var extractionResult);

        if (extractionResult is null)
        {
            return BatchExtractionOutcome.Failure(
                new[] { ApplicationErrors.AssetExtractionNotSupported(asset.AssetId) });
        }

        return BatchExtractionOutcome.AssetSuccess(extractionResult!);
    }

    private async Task<BatchExtractionOutcome> ExtractSpineGroupAsync(
        ExtractWorkspaceAssetsCommand command,
        IndexedPlannedExtractionWorkItem item,
        CancellationToken cancellationToken)
    {
        if (_extractSpineAssetGroupHandler is null || item.SpineGroup is null)
        {
            return BatchExtractionOutcome.Failure(new[]
            {
                ApplicationErrors.NoSpineAssetGroupExtractionCandidates(item.SpineGroup?.GroupId ?? string.Empty)
            });
        }

        var result = await _extractSpineAssetGroupHandler.HandleAsync(
            new ExtractSpineAssetGroupCommand(
                command.WorkspaceId,
                command.WorkspaceKey ?? string.Empty,
                command.WorkspaceDataRootPath ?? string.Empty,
                item.SpineGroup.BundleId,
                item.SpineGroup.GroupId,
                item.SpineGroup.SuggestedOutputDirectory),
            cancellationToken);
        if (result.IsCancelled)
        {
            return BatchExtractionOutcome.Cancelled();
        }

        return result.IsSuccess && result.Value is not null
            ? BatchExtractionOutcome.SpineSuccess(result.Value)
            : BatchExtractionOutcome.Failure(result.Errors);
    }

    private static BatchExtractionOutcome ConvertAssetResult(Result<AssetExtractionResultDto> result)
    {
        if (result.IsCancelled)
        {
            return BatchExtractionOutcome.Cancelled();
        }

        return result.IsSuccess && result.Value is not null
            ? BatchExtractionOutcome.AssetSuccess(result.Value)
            : BatchExtractionOutcome.Failure(result.Errors);
    }

    private static IReadOnlyList<IndexedPlannedExtractionWorkItem> BuildIndexedWorkItems(
        WorkspaceBatchExtractionPlan plan)
    {
        var items = new List<IndexedPlannedExtractionWorkItem>(
            plan.Assets.Count + plan.SpineGroups.Count);
        var index = 0;
        foreach (var asset in plan.Assets)
        {
            items.Add(IndexedPlannedExtractionWorkItem.ForAsset(index++, asset));
        }

        foreach (var spineGroup in plan.SpineGroups)
        {
            items.Add(IndexedPlannedExtractionWorkItem.ForSpineGroup(index++, spineGroup));
        }

        return items;
    }

    private static void ReportProgress(
        ExtractWorkspaceAssetsCommand command,
        WorkspaceBatchExtractionProgressDto progress)
    {
        command.Progress?.Report(progress);
    }

}
