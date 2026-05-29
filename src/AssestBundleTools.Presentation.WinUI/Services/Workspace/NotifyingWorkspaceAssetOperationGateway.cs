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

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 为资产操作网关补充工作区读模型失效通知，避免页面手动互相刷新。
/// </summary>
internal sealed class NotifyingWorkspaceAssetOperationGateway : IWorkspaceAssetOperationGateway
{
    private readonly IWorkspaceAssetOperationGateway _inner;
    private readonly IWorkspaceOperationChangeNotifier _operationChangeNotifier;
    private readonly IWorkspaceAssociatedAssetImpactRegistry? _associatedAssetImpactRegistry;

    public NotifyingWorkspaceAssetOperationGateway(
        IWorkspaceAssetOperationGateway inner,
        IWorkspaceOperationChangeNotifier operationChangeNotifier,
        IWorkspaceAssociatedAssetImpactRegistry? associatedAssetImpactRegistry = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _operationChangeNotifier = operationChangeNotifier ?? throw new ArgumentNullException(nameof(operationChangeNotifier));
        _associatedAssetImpactRegistry = associatedAssetImpactRegistry;
    }

    public async Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExtractAssetAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Extraction,
            CreateAffectedAsset(command.BundleId, command.AssetId));
        return result;
    }

    public async Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineAssetGroupAsync(
        ExtractSpineAssetGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExtractSpineAssetGroupAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Extraction,
            CreateAffectedAssets(result.Value?.AssetResults));
        return result;
    }

    public Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
        GetFileReplacementCandidateQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetFileReplacementCandidateAsync(query, cancellationToken);

    public Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
        GetWorkspaceReplacementCandidatesQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetWorkspaceReplacementCandidatesAsync(query, cancellationToken);

    public async Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
        ReplaceAssetFromFileCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ReplaceAssetFromFileAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Extraction | WorkspaceOperationChangeKind.Replacement,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAsset(command.TargetBundleId, command.TargetAssetId)));
        return result;
    }

    public async Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
        ReplaceAssetFromWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ReplaceAssetFromWorkspaceAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Replacement,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAsset(command.TargetBundleId, command.TargetAssetId)));
        return result;
    }

    public async Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
        UndoAssetReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.UndoAssetReplacementAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Replacement,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAssets(
                    command.BundleId,
                    command.AssetId,
                    result.Value?.TargetBundleId,
                    result.Value?.TargetAssetId)));
        return result;
    }

    public async Task<Result<AssetReplacementRedoResultDto>> RedoAssetReplacementAsync(
        RedoAssetReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.RedoAssetReplacementAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Replacement,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAssets(
                    command.BundleId,
                    command.AssetId,
                    result.Value?.TargetBundleId,
                    result.Value?.TargetAssetId)));
        return result;
    }

    public async Task<Result<AssetSpriteEditAppliedResultDto>> ApplySpriteEditAsync(
        ApplySpriteEditCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ApplySpriteEditAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Edit,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAsset(command.BundleId, command.AssetId)));
        return result;
    }

    public async Task<Result<WorkspaceOperationRecordDeletedResultDto>> DeleteWorkspaceOperationRecordAsync(
        DeleteWorkspaceOperationRecordCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.DeleteWorkspaceOperationRecordAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Extraction | WorkspaceOperationChangeKind.Replacement | WorkspaceOperationChangeKind.Edit,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAssets(result.Value)));
        return result;
    }

    private void NotifyIfSucceeded<T>(
        Result<T> result,
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind,
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets = null)
    {
        if (result.IsSuccess)
        {
            _operationChangeNotifier.NotifyWorkspaceOperationsChanged(workspaceId, changeKind, affectedAssets);
        }
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> CreateAffectedAsset(
        BundleId bundleId,
        AssetId assetId) =>
        [new WorkspaceOperationAffectedAsset(bundleId, assetId)];

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
                new WorkspaceOperationAffectedAsset(impact.BundleId, impact.AssetId)));
        }

        return NormalizeAffectedAssets(expandedAssets);
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> CreateAffectedAssets(
        BundleId primaryBundleId,
        AssetId primaryAssetId,
        BundleId? secondaryBundleId,
        AssetId? secondaryAssetId)
    {
        var affectedAssets = new List<WorkspaceOperationAffectedAsset>
        {
            new(primaryBundleId, primaryAssetId)
        };
        if (secondaryBundleId.HasValue && secondaryAssetId.HasValue)
        {
            affectedAssets.Add(new WorkspaceOperationAffectedAsset(secondaryBundleId.Value, secondaryAssetId.Value));
        }

        return NormalizeAffectedAssets(affectedAssets);
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset>? CreateAffectedAssets(
        WorkspaceOperationRecordDeletedResultDto? result)
    {
        if (result is null)
        {
            return null;
        }

        var affectedTargets = result.AffectedTargets ?? Array.Empty<WorkspaceOperationTargetDto>();
        var affectedAssets = affectedTargets
            .Select(static target => BundleId.TryParse(target.BundleId, out var bundleId) &&
                                     AssetId.TryParse(target.AssetId, out var assetId)
                ? new WorkspaceOperationAffectedAsset(bundleId, assetId)
                : null)
            .Where(static item => item is not null)
            .Select(static item => item!)
            .ToArray();
        if (affectedAssets.Length > 0)
        {
            return affectedAssets;
        }

        return result.BundleId is { } bundleId && result.AssetId is { } assetId
            ? CreateAffectedAsset(bundleId, assetId)
            : null;
    }

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> CreateAffectedAssets(
        IReadOnlyList<AssetExtractionResultDto>? assetResults) =>
        assetResults?
            .Select(static result => new WorkspaceOperationAffectedAsset(result.BundleId, result.AssetId))
            .ToArray() ??
        Array.Empty<WorkspaceOperationAffectedAsset>();

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> NormalizeAffectedAssets(
        IEnumerable<WorkspaceOperationAffectedAsset> affectedAssets) =>
        affectedAssets
            .Where(static item => item is not null)
            .GroupBy(static item => string.Concat(item.BundleId.Value, "\n", item.AssetId.Value), StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
}
