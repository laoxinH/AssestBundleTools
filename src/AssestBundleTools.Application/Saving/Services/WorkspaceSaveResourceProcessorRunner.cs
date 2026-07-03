using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Saving.Services;

/// <summary>
/// 编排工作区保存中的资源处理器计划、并发执行和结果汇总。
/// </summary>
internal sealed class WorkspaceSaveResourceProcessorRunner
{
    private readonly IResourceProcessorCatalogStore? _resourceProcessorCatalogStore;
    private readonly IWorkspaceResourceProcessorExecutor? _resourceProcessorExecutor;

    public WorkspaceSaveResourceProcessorRunner(
        IResourceProcessorCatalogStore? resourceProcessorCatalogStore,
        IWorkspaceResourceProcessorExecutor? resourceProcessorExecutor)
    {
        _resourceProcessorCatalogStore = resourceProcessorCatalogStore;
        _resourceProcessorExecutor = resourceProcessorExecutor;
    }

    public Result<WorkspaceResourceProcessorPlan> BuildPlan(
        IReadOnlyList<WorkspaceExportProcessorSelectionDto> processorSelections)
    {
        var enabledSelections = processorSelections
            .Where(static selection => !string.IsNullOrWhiteSpace(selection.ProcessorId))
            .ToArray();
        if (enabledSelections.Length == 0)
        {
            return Result<WorkspaceResourceProcessorPlan>.Success(WorkspaceResourceProcessorPlan.Empty);
        }

        if (_resourceProcessorCatalogStore is null || _resourceProcessorExecutor is null)
        {
            return Result<WorkspaceResourceProcessorPlan>.Failure(
                ApplicationErrors.ResourceProcessorExecutionFailed(
                    enabledSelections[0].ProcessorId!,
                    "ExecutorNotConfigured"));
        }

        var catalogEntries = _resourceProcessorCatalogStore.Load();
        var processorsByKind = new Dictionary<string, ResourceProcessorCatalogEntryDto>(StringComparer.Ordinal);
        foreach (var selection in enabledSelections)
        {
            var resourceKind = NormalizeResourceKind(selection.ResourceKind);
            var processorId = selection.ProcessorId!.Trim();
            var processor = catalogEntries.FirstOrDefault(entry =>
                string.Equals(entry.Id, processorId, StringComparison.OrdinalIgnoreCase));
            if (processor is null)
            {
                return Result<WorkspaceResourceProcessorPlan>.Failure(
                    ApplicationErrors.ResourceProcessorNotFound(processorId));
            }

            if (processor.Status != ResourceProcessorStatus.Available)
            {
                return Result<WorkspaceResourceProcessorPlan>.Failure(
                    ApplicationErrors.ResourceProcessorUnavailable(processorId));
            }

            if (!processor.ResourceKinds.Any(kind =>
                    string.Equals(NormalizeResourceKind(kind), resourceKind, StringComparison.Ordinal)))
            {
                return Result<WorkspaceResourceProcessorPlan>.Failure(
                    ApplicationErrors.ResourceProcessorUnsupportedResourceKind(processorId, resourceKind));
            }

            processorsByKind[resourceKind] = processor;
        }

        return Result<WorkspaceResourceProcessorPlan>.Success(new WorkspaceResourceProcessorPlan(processorsByKind));
    }

    public async Task<Result<WorkspaceResourceProcessorBatchResult>> ExecuteAsync(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        WorkspaceResourceProcessorPlan resourceProcessorPlan,
        WorkspaceResourceProcessorExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (resourceProcessorPlan.IsEmpty)
        {
            return Result<WorkspaceResourceProcessorBatchResult>.Success(WorkspaceResourceProcessorBatchResult.Empty);
        }

        if (_resourceProcessorExecutor is null)
        {
            return Result<WorkspaceResourceProcessorBatchResult>.Failure(
                ApplicationErrors.ResourceProcessorExecutionFailed("unknown", "ExecutorNotConfigured"));
        }

        if (string.IsNullOrWhiteSpace(context.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(context.WorkspaceDataRootPath))
        {
            return Result<WorkspaceResourceProcessorBatchResult>.Failure(
                ApplicationErrors.ResourceProcessorExecutionFailed(
                    ResolveFirstProcessorId(resourceProcessorPlan),
                    "WorkspaceContextMissing"));
        }

        var workItems = BuildWorkItems(
            plan,
            selectedPlans,
            resourceProcessorPlan,
            context);
        if (workItems.Count == 0)
        {
            return Result<WorkspaceResourceProcessorBatchResult>.Success(WorkspaceResourceProcessorBatchResult.Empty);
        }

        context.Progress?.Report(new WorkspaceSaveExecutionProgressDto(
            WorkspaceSaveExecutionStageDto.ResourceProcessing,
            CompletedBundleCount: 0,
            TotalBundleCount: selectedPlans.Count));

        var concurrency = ResolveResourceProcessorConcurrency(workItems.Count);
        var semaphore = new SemaphoreSlim(concurrency, concurrency);
        var tasks = workItems.Select(async workItem =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await _resourceProcessorExecutor.ExecuteAsync(workItem, cancellationToken);
                return new WorkspaceResourceProcessorWorkItemResult(workItem, result);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var executionResults = await Task.WhenAll(tasks);
        var results = new List<WorkspaceResourceProcessorExecutionResultDto>();
        var failedBundleDiagnostics = new Dictionary<string, AppError>(StringComparer.Ordinal);
        foreach (var itemResult in executionResults)
        {
            var executionResult = itemResult.Result;
            if (executionResult.IsCancelled)
            {
                return Result<WorkspaceResourceProcessorBatchResult>.Cancelled();
            }

            if (executionResult.IsFailure)
            {
                var error = executionResult.Errors.FirstOrDefault()
                            ?? ApplicationErrors.ResourceProcessorExecutionFailed(
                                itemResult.WorkItem.Processor.Id,
                                itemResult.WorkItem.AssetName);
                failedBundleDiagnostics.TryAdd(itemResult.WorkItem.BundleId.Value, error);
                results.Add(CreateFailedExecutionResult(itemResult.WorkItem, error));
                continue;
            }

            var result = executionResult.Value!;
            results.Add(result);
            if (result.Status == WorkspaceResourceProcessorStatusDto.Failed)
            {
                failedBundleDiagnostics.TryAdd(
                    result.BundleId.Value,
                    ApplicationErrors.ResourceProcessorExecutionFailed(result.ProcessorId, result.AssetName));
            }
        }

        return Result<WorkspaceResourceProcessorBatchResult>.Success(
            new WorkspaceResourceProcessorBatchResult(
                results
                    .OrderBy(static result => result.BundleId.Value, StringComparer.Ordinal)
                    .ThenBy(static result => result.ResourceKind, StringComparer.Ordinal)
                    .ThenBy(static result => result.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static result => result.AssetId.Value, StringComparer.Ordinal)
                    .ToArray(),
                failedBundleDiagnostics));
    }

    public static IReadOnlyList<WorkspaceBundleProcessorExecutionSummaryDto> SummarizeResults(
        IReadOnlyList<WorkspaceResourceProcessorExecutionResultDto> processorResults)
    {
        if (processorResults.Count == 0)
        {
            return Array.Empty<WorkspaceBundleProcessorExecutionSummaryDto>();
        }

        return processorResults
            .GroupBy(
                static result => new ProcessorSummaryKey(result.ResourceKind, result.ProcessorId),
                ProcessorSummaryKeyComparer.Instance)
            .Select(static group =>
            {
                var results = group.ToArray();
                var failedCount = results.Count(static result => result.Status == WorkspaceResourceProcessorStatusDto.Failed);
                var skippedCount = results.Count(static result => result.Status == WorkspaceResourceProcessorStatusDto.Skipped);
                var status = failedCount > 0
                    ? WorkspaceResourceProcessorStatusDto.Failed
                    : skippedCount == results.Length
                        ? WorkspaceResourceProcessorStatusDto.Skipped
                        : WorkspaceResourceProcessorStatusDto.Succeeded;

                return new WorkspaceBundleProcessorExecutionSummaryDto(
                    group.Key.ResourceKind,
                    group.Key.ProcessorId,
                    status,
                    InputCount: results.Length,
                    ProcessedCount: results.Count(static result => result.Status == WorkspaceResourceProcessorStatusDto.Succeeded),
                    FailedCount: failedCount,
                    ResultPaths: results
                        .Select(static result => result.ResultPath)
                        .Where(static path => !string.IsNullOrWhiteSpace(path))
                        .Select(static path => path!)
                        .ToArray(),
                    Diagnostics: results
                        .SelectMany(static result => result.Diagnostics)
                        .ToArray());
            })
            .OrderBy(static summary => summary.ResourceKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static summary => summary.ProcessorId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<WorkspaceResourceProcessorWorkItemDto> BuildWorkItems(
        WorkspaceSavePlan plan,
        IReadOnlyList<PlannedWorkspaceBundleSave> selectedPlans,
        WorkspaceResourceProcessorPlan resourceProcessorPlan,
        WorkspaceResourceProcessorExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.WorkspaceKey) ||
            string.IsNullOrWhiteSpace(context.WorkspaceDataRootPath))
        {
            return Array.Empty<WorkspaceResourceProcessorWorkItemDto>();
        }

        var workItems = new List<WorkspaceResourceProcessorWorkItemDto>();
        foreach (var selectedPlan in selectedPlans)
        {
            foreach (var changeItem in selectedPlan.ChangeItems.OrderBy(static item => item.AssetName, StringComparer.OrdinalIgnoreCase))
            {
                var resourceKind = ResolveResourceKind(changeItem);
                if (resourceKind is null ||
                    !resourceProcessorPlan.ProcessorsByResourceKind.TryGetValue(resourceKind, out var processor) ||
                    string.IsNullOrWhiteSpace(changeItem.FileReplacementSourcePath))
                {
                    continue;
                }

                workItems.Add(new WorkspaceResourceProcessorWorkItemDto(
                    WorkspaceId: plan.WorkspaceId,
                    WorkspaceKey: context.WorkspaceKey.Trim(),
                    WorkspaceDataRootPath: context.WorkspaceDataRootPath.Trim(),
                    OperationId: context.OperationId,
                    BundleId: selectedPlan.BundleId,
                    BundleDisplayName: selectedPlan.BundleDisplayName,
                    BundleCacheKey: selectedPlan.ArtifactInput.BundleCacheKey,
                    AssetId: changeItem.AssetId,
                    AssetName: changeItem.AssetName,
                    AssetType: changeItem.AssetType,
                    ResourceKind: resourceKind,
                    InputPayloadPath: changeItem.FileReplacementSourcePath,
                    StagingDirectoryPath: BuildStagingDirectory(
                        context.WorkspaceDataRootPath,
                        context.OperationId,
                        selectedPlan.BundleId,
                        resourceKind,
                        changeItem.AssetId),
                    Processor: processor));
            }
        }

        return workItems;
    }

    private static WorkspaceResourceProcessorExecutionResultDto CreateFailedExecutionResult(
        WorkspaceResourceProcessorWorkItemDto workItem,
        AppError error)
    {
        var detail = string.Join(
            "; ",
            error.Parameters
                .Where(static parameter => parameter.Value is not null)
                .Select(static parameter => $"{parameter.Key}={parameter.Value}"));

        return new WorkspaceResourceProcessorExecutionResultDto(
            BundleId: workItem.BundleId,
            AssetId: workItem.AssetId,
            AssetName: workItem.AssetName,
            ResourceKind: workItem.ResourceKind,
            ProcessorId: workItem.Processor.Id,
            Status: WorkspaceResourceProcessorStatusDto.Failed,
            InputPath: workItem.InputPayloadPath,
            OutputPath: null,
            ResultPath: null,
            Diagnostics:
            [
                new WorkspaceResourceProcessorDiagnosticDto(
                    error.Code,
                    error.Severity.ToString(),
                    string.IsNullOrWhiteSpace(detail) ? null : detail)
            ]);
    }

    private static string BuildStagingDirectory(
        string workspaceDataRootPath,
        string operationId,
        BundleId bundleId,
        string resourceKind,
        AssetId assetId)
    {
        return Path.Combine(
            workspaceDataRootPath.Trim(),
            "staging",
            "exports",
            SanitizePathSegment(operationId),
            "resource-processing",
            SanitizePathSegment(bundleId.Value),
            SanitizePathSegment(resourceKind),
            SanitizePathSegment(assetId.Value));
    }

    private static string? ResolveResourceKind(WorkspaceChangeItemDto changeItem)
    {
        return changeItem.AssetType switch
        {
            AssetType.Texture2D or AssetType.Sprite => "texture2d",
            AssetType.AudioClip => "audio",
            AssetType.TextAsset when LooksLikeAtlas(changeItem.AssetName) => "atlas",
            AssetType.TextAsset when LooksLikeSkeleton(changeItem.AssetName) => "skel",
            _ => null
        };
    }

    private static bool LooksLikeAtlas(string assetName) =>
        assetName.EndsWith(".atlas", StringComparison.OrdinalIgnoreCase) ||
        assetName.EndsWith(".atlas.txt", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSkeleton(string assetName) =>
        assetName.EndsWith(".skel", StringComparison.OrdinalIgnoreCase) ||
        assetName.EndsWith(".skel.bytes", StringComparison.OrdinalIgnoreCase) ||
        assetName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeResourceKind(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static int ResolveResourceProcessorConcurrency(int workItemCount)
    {
        if (workItemCount <= 1)
        {
            return 1;
        }

        return Math.Clamp(Environment.ProcessorCount / 2, 1, Math.Min(4, workItemCount));
    }

    private static string ResolveFirstProcessorId(WorkspaceResourceProcessorPlan resourceProcessorPlan)
    {
        return resourceProcessorPlan.ProcessorsByResourceKind.Values.FirstOrDefault()?.Id ?? "unknown";
    }

    private static string SanitizePathSegment(string value)
    {
        var source = string.IsNullOrWhiteSpace(value) ? "item" : value.Trim();
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(source.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }

    private sealed record WorkspaceResourceProcessorWorkItemResult(
        WorkspaceResourceProcessorWorkItemDto WorkItem,
        Result<WorkspaceResourceProcessorExecutionResultDto> Result);

    private readonly record struct ProcessorSummaryKey(string ResourceKind, string ProcessorId);

    private sealed class ProcessorSummaryKeyComparer : IEqualityComparer<ProcessorSummaryKey>
    {
        public static ProcessorSummaryKeyComparer Instance { get; } = new();

        public bool Equals(ProcessorSummaryKey x, ProcessorSummaryKey y) =>
            string.Equals(x.ResourceKind, y.ResourceKind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ProcessorId, y.ProcessorId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ProcessorSummaryKey obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ResourceKind),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProcessorId));
    }
}

internal sealed record WorkspaceResourceProcessorPlan(
    IReadOnlyDictionary<string, ResourceProcessorCatalogEntryDto> ProcessorsByResourceKind)
{
    public static WorkspaceResourceProcessorPlan Empty { get; } = new(
        new Dictionary<string, ResourceProcessorCatalogEntryDto>(StringComparer.Ordinal));

    public bool IsEmpty => ProcessorsByResourceKind.Count == 0;
}

internal sealed record WorkspaceResourceProcessorBatchResult(
    IReadOnlyList<WorkspaceResourceProcessorExecutionResultDto> Results,
    IReadOnlyDictionary<string, AppError> FailedBundleDiagnostics)
{
    public static WorkspaceResourceProcessorBatchResult Empty { get; } = new(
        Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
        new Dictionary<string, AppError>(StringComparer.Ordinal));

    public IReadOnlyList<WorkspaceResourceProcessorExecutionResultDto> ResolveResults(BundleId bundleId)
    {
        return Results
            .Where(result => string.Equals(result.BundleId.Value, bundleId.Value, StringComparison.Ordinal))
            .ToArray();
    }
}

internal sealed record WorkspaceResourceProcessorExecutionContext(
    string OperationId,
    string? WorkspaceKey,
    string? WorkspaceDataRootPath,
    IProgress<WorkspaceSaveExecutionProgressDto>? Progress);
