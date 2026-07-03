using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 为批处理操作网关补充工作区读模型失效通知。
/// </summary>
internal sealed class NotifyingWorkspaceBatchOperationsGateway : IWorkspaceBatchOperationsGateway
{
    private readonly IWorkspaceBatchOperationsGateway _inner;
    private readonly IWorkspaceOperationChangeNotifier _operationChangeNotifier;
    private readonly IWorkspaceAssociatedAssetImpactRegistry? _associatedAssetImpactRegistry;

    public NotifyingWorkspaceBatchOperationsGateway(
        IWorkspaceBatchOperationsGateway inner,
        IWorkspaceOperationChangeNotifier operationChangeNotifier,
        IWorkspaceAssociatedAssetImpactRegistry? associatedAssetImpactRegistry = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _operationChangeNotifier = operationChangeNotifier ?? throw new ArgumentNullException(nameof(operationChangeNotifier));
        _associatedAssetImpactRegistry = associatedAssetImpactRegistry;
    }

    public Task<Result<WorkspaceChangeSummaryDto>> GetWorkspaceChangeSummaryAsync(
        GetWorkspaceChangeSummaryQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceChangeSummaryAsync(query, cancellationToken);

    public Task<Result<WorkspaceSavePreviewDto>> GetWorkspaceSavePreviewAsync(
        GetWorkspaceSavePreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceSavePreviewAsync(query, cancellationToken);

    // 保存检查点尚未落地前，导出结果只由导出窗口消费，避免主工作台误刷新旧资产标签。
    public Task<Result<WorkspaceSaveExecutionResultDto>> SaveWorkspaceAsNewBundleAsync(
        SaveWorkspaceAsNewBundleCommand command,
        CancellationToken cancellationToken = default) =>
        _inner.SaveWorkspaceAsNewBundleAsync(command, cancellationToken);

    public Task<Result<WorkspaceBatchExtractionPreviewDto>> GetWorkspaceBatchExtractionPreviewAsync(
        GetWorkspaceBatchExtractionPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceBatchExtractionPreviewAsync(query, cancellationToken);

    public async Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExtractWorkspaceAssetsAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Extraction,
            CreateAffectedAssets(result.Value),
            WorkspaceOperationChangeReason.BatchExtractionRecorded);
        return result;
    }

    public Task<Result<WorkspaceBatchReplacementPlanDto>> GetWorkspaceBatchReplacementPlanAsync(
        GetWorkspaceBatchReplacementPlanQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceBatchReplacementPlanAsync(query, cancellationToken);

    public async Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
        ExecuteWorkspaceBatchReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExecuteWorkspaceBatchReplacementAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Replacement,
            ExpandAffectedAssets(command.WorkspaceId, CreateAffectedAssets(result.Value)),
            reason: WorkspaceOperationChangeReason.BatchReplacementApplied);
        return result;
    }

    private void NotifyIfSucceeded<T>(
        Result<T> result,
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind,
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets = null,
        WorkspaceOperationChangeReason reason = WorkspaceOperationChangeReason.Unspecified)
    {
        if (result.IsSuccess)
        {
            _operationChangeNotifier.NotifyWorkspaceOperationsChanged(
                workspaceId,
                changeKind,
                affectedAssets,
                reason);
        }
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> CreateAffectedAssets(
        WorkspaceBatchExtractionResultDto? result)
    {
        if (result is null)
        {
            return Array.Empty<WorkspaceOperationAffectedAsset>();
        }

        return (result.AssetResults ?? Array.Empty<AssetExtractionResultDto>())
            .Concat((result.SpineGroupResults ?? Array.Empty<SpineAssetGroupExtractionResultDto>())
                .SelectMany(static groupResult => groupResult.AssetResults))
            .Select(static assetResult => new WorkspaceOperationAffectedAsset(
                assetResult.BundleId,
                assetResult.AssetId))
            .ToArray();
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> CreateAffectedAssets(
        WorkspaceBatchReplacementResultDto? result)
    {
        if (result is null)
        {
            return Array.Empty<WorkspaceOperationAffectedAsset>();
        }

        return (result.ItemResults ?? Array.Empty<WorkspaceBatchReplacementItemResultDto>())
            .Where(static item => item.Status == WorkspaceBatchReplacementItemStatus.Replaced)
            .Where(static item => item.BundleId is not null && item.AssetId is not null)
            .Select(static item => new WorkspaceOperationAffectedAsset(
                item.BundleId!.Value,
                item.AssetId!.Value,
                WorkspaceOperationAffectedAssetRole.PrimaryTarget))
            .ToArray();
    }

    private IReadOnlyList<WorkspaceOperationAffectedAsset>? ExpandAffectedAssets(
        WorkspaceId workspaceId,
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets)
    {
        if (_associatedAssetImpactRegistry is null ||
            affectedAssets is null ||
            affectedAssets.Count == 0)
        {
            return affectedAssets;
        }

        var expandedAssets = new List<WorkspaceOperationAffectedAsset>(affectedAssets);
        foreach (var affectedAsset in affectedAssets)
        {
            if (!_associatedAssetImpactRegistry.TryGetAssociatedAssetImpacts(
                    workspaceId,
                    affectedAsset.BundleId,
                    affectedAsset.AssetId,
                    out var impacts))
            {
                continue;
            }

            expandedAssets.AddRange(impacts.Select(static impact =>
                new WorkspaceOperationAffectedAsset(
                    impact.BundleId,
                    impact.AssetId,
                    WorkspaceOperationAffectedAssetRole.ImplicitAffectedTarget)));
        }

        return NormalizeAffectedAssets(expandedAssets);
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> NormalizeAffectedAssets(
        IEnumerable<WorkspaceOperationAffectedAsset> affectedAssets) =>
        affectedAssets
            .Where(static item => item is not null)
            .GroupBy(static item => string.Concat(item.BundleId.Value, "\n", item.AssetId.Value), StringComparer.Ordinal)
            .Select(static group => group
                .OrderBy(static item => GetRolePriority(item.Role))
                .First())
            .ToArray();

    private static int GetRolePriority(WorkspaceOperationAffectedAssetRole role) =>
        role switch
        {
            WorkspaceOperationAffectedAssetRole.PrimaryTarget => 0,
            WorkspaceOperationAffectedAssetRole.RelatedTarget => 1,
            WorkspaceOperationAffectedAssetRole.ImplicitAffectedTarget => 2,
            _ => 3
        };
}
