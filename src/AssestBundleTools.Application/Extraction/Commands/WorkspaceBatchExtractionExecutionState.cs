using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Extraction.Commands;

internal sealed record PlannedBundleExtractionGroup(
    string BundleId,
    IReadOnlyList<IndexedPlannedExtractionWorkItem> Items);

internal sealed record WorkerSlotLease(
    int ActiveWorkerCount,
    int CurrentWorkerLimit,
    int MaximumWorkerCount,
    bool IsMemoryThrottled);

internal sealed record IndexedPlannedExtractionWorkItem(
    int Index,
    BatchExtractionWorkItemKind Kind,
    PlannedAssetExtraction? Asset,
    PlannedSpineGroupExtraction? SpineGroup)
{
    public BundleId BundleId => Kind == BatchExtractionWorkItemKind.SpineGroup
        ? SpineGroup!.BundleId
        : Asset!.BundleId;

    public string BundleDisplayName => Kind == BatchExtractionWorkItemKind.SpineGroup
        ? SpineGroup!.BundleDisplayName
        : Asset!.BundleDisplayName;

    public string ItemDisplayName => Kind == BatchExtractionWorkItemKind.SpineGroup
        ? SpineGroup!.GroupDisplayName
        : Asset!.AssetName;

    public AssetType? AssetType => Kind == BatchExtractionWorkItemKind.Asset
        ? Asset!.AssetType
        : null;

    public string? AssetTypeDisplayName => Kind == BatchExtractionWorkItemKind.Asset
        ? Asset!.AssetTypeDisplayName
        : null;

    public static IndexedPlannedExtractionWorkItem ForAsset(int index, PlannedAssetExtraction asset) =>
        new(index, BatchExtractionWorkItemKind.Asset, asset, null);

    public static IndexedPlannedExtractionWorkItem ForSpineGroup(int index, PlannedSpineGroupExtraction spineGroup) =>
        new(index, BatchExtractionWorkItemKind.SpineGroup, null, spineGroup);
}

internal enum BatchExtractionWorkItemKind
{
    Asset,
    SpineGroup
}

internal sealed record BatchExtractionOutcome(
    IReadOnlyList<AssetExtractionResultDto>? AssetResults,
    SpineAssetGroupExtractionResultDto? SpineGroupResult,
    IReadOnlyList<AppError>? Errors,
    bool IsCancelled,
    IndexedPlannedExtractionWorkItem? WorkItem = null)
{
    public static BatchExtractionOutcome AssetSuccess(
        AssetExtractionResultDto result,
        IndexedPlannedExtractionWorkItem? workItem = null) =>
        new(new[] { result }, null, null, IsCancelled: false, workItem);

    public static BatchExtractionOutcome SpineSuccess(
        SpineAssetGroupExtractionResultDto result,
        IndexedPlannedExtractionWorkItem? workItem = null) =>
        new(result.AssetResults, result, null, IsCancelled: false, workItem);

    public static BatchExtractionOutcome Failure(IReadOnlyList<AppError> errors) =>
        new(null, null, errors, IsCancelled: false);

    public static BatchExtractionOutcome Cancelled() =>
        new(null, null, null, IsCancelled: true);
}

internal sealed class WorkerIndexBox
{
    public int NextIndex = -1;
}

internal sealed class WorkerActivityBox
{
    public object SyncRoot { get; } = new();

    public int ActiveWorkerCount { get; set; }
}

internal sealed class BatchExtractionProgressState
{
    private readonly int _totalItemCount;
    private readonly int _candidateBundleCount;
    private int _completedItemCount;
    private int _succeededItemCount;
    private int _failedItemCount;

    public BatchExtractionProgressState(int totalItemCount, int candidateBundleCount)
    {
        _totalItemCount = Math.Max(0, totalItemCount);
        _candidateBundleCount = Math.Max(0, candidateBundleCount);
    }

    public void RecordOutcome(BatchExtractionOutcome outcome)
    {
        Interlocked.Increment(ref _completedItemCount);
        if (outcome.Errors is { Count: > 0 })
        {
            Interlocked.Increment(ref _failedItemCount);
            return;
        }

        if (!outcome.IsCancelled)
        {
            Interlocked.Increment(ref _succeededItemCount);
        }
    }

    public WorkspaceBatchExtractionProgressDto CreatePreparingProgress() =>
        new(
            WorkspaceBatchExtractionProgressKind.Preparing,
            CompletedItemCount: 0,
            TotalItemCount: _totalItemCount,
            CandidateBundleCount: _candidateBundleCount,
            SucceededItemCount: 0,
            FailedItemCount: 0);

    public WorkspaceBatchExtractionProgressDto CreateItemProgress(
        IndexedPlannedExtractionWorkItem item,
        bool isMemoryThrottled) =>
        new(
            ResolveProgressKind(item.Kind),
            CompletedItemCount: Volatile.Read(ref _completedItemCount),
            TotalItemCount: _totalItemCount,
            CandidateBundleCount: _candidateBundleCount,
            SucceededItemCount: Volatile.Read(ref _succeededItemCount),
            FailedItemCount: Volatile.Read(ref _failedItemCount),
            BundleDisplayName: item.BundleDisplayName,
            ItemDisplayName: item.ItemDisplayName,
            AssetType: item.AssetType,
            AssetTypeDisplayName: item.AssetTypeDisplayName,
            IsMemoryThrottled: isMemoryThrottled);

    private static WorkspaceBatchExtractionProgressKind ResolveProgressKind(
        BatchExtractionWorkItemKind itemKind) =>
        itemKind == BatchExtractionWorkItemKind.SpineGroup
            ? WorkspaceBatchExtractionProgressKind.SpineGroup
            : WorkspaceBatchExtractionProgressKind.Asset;
}
