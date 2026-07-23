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
            CreateAffectedAsset(command.BundleId, command.AssetId),
            WorkspaceOperationChangeReason.ExtractionRecorded);
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
            CreateAffectedAssets(result.Value?.AssetResults),
            WorkspaceOperationChangeReason.ExtractionRecorded);
        return result;
    }

    public Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
        GetFileReplacementCandidateQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.GetFileReplacementCandidateAsync(query, cancellationToken);

    public Task<Result<IReadOnlyList<SpineReplacementSourceMatchDto>>> MatchSpineReplacementSourcesAsync(
        MatchSpineReplacementSourcesQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.MatchSpineReplacementSourcesAsync(query, cancellationToken);

    public Task<Result<SpineSkeletonFormatCompatibilityDto>> CheckSpineSkeletonReplacementCompatibilityAsync(
        CheckSpineSkeletonReplacementCompatibilityQuery query,
        CancellationToken cancellationToken = default) =>
        _inner.CheckSpineSkeletonReplacementCompatibilityAsync(query, cancellationToken);

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
                CreateAffectedAsset(command.TargetBundleId, command.TargetAssetId)),
            WorkspaceOperationChangeReason.ReplacementApplied);
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
                CreateAffectedAsset(command.TargetBundleId, command.TargetAssetId)),
            WorkspaceOperationChangeReason.ReplacementApplied);
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
                    result.Value?.TargetAssetId)),
            WorkspaceOperationChangeReason.ReplacementUndone,
            CreateOperationIds(command.ReplacementOperationId));
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
                    result.Value?.TargetAssetId)),
            WorkspaceOperationChangeReason.ReplacementRedone,
            CreateOperationIds(command.ReplacementOperationId));
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
                CreateAffectedAsset(command.BundleId, command.AssetId)),
            WorkspaceOperationChangeReason.EditApplied);
        return result;
    }

    public async Task<Result<AssetTextEditDraftDto>> CreateTextAssetEditDraftAsync(
        CreateTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.CreateTextAssetEditDraftAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Edit,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAsset(command.BundleId, command.AssetId)),
            WorkspaceOperationChangeReason.EditApplied);
        return result;
    }

    public Task<Result<AssetTextEditDraftDto>> OpenTextAssetEditDraftAsync(
        OpenTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default) =>
        _inner.OpenTextAssetEditDraftAsync(command, cancellationToken);

    public async Task<Result<AssetTextEditDraftUpdatedResultDto>> UpdateTextAssetEditDraftAsync(
        UpdateTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.UpdateTextAssetEditDraftAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Edit,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAsset(command.BundleId, command.AssetId)),
            WorkspaceOperationChangeReason.EditApplied);
        return result;
    }

    public async Task<Result<AssetTextEditAppliedResultDto>> ApplyTextAssetEditDraftAsync(
        ApplyTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ApplyTextAssetEditDraftAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Edit,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAsset(command.BundleId, command.AssetId)),
            WorkspaceOperationChangeReason.EditApplied);
        return result;
    }

    public async Task<Result<AssetTextEditDraftDiscardedResultDto>> DiscardTextAssetEditDraftAsync(
        DiscardTextAssetEditDraftCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.DiscardTextAssetEditDraftAsync(command, cancellationToken);
        NotifyIfSucceeded(
            result,
            command.WorkspaceId,
            WorkspaceOperationChangeKind.Edit,
            ExpandAffectedAssets(
                command.WorkspaceId,
                CreateAffectedAsset(command.BundleId, command.AssetId)),
            WorkspaceOperationChangeReason.EditApplied);
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
                CreateAffectedAssets(result.Value)),
            WorkspaceOperationChangeReason.RecordDeleted,
            CreateOperationIds(result.Value?.OperationId ?? command.OperationId));
        return result;
    }

    private void NotifyIfSucceeded<T>(
        Result<T> result,
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind,
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets = null,
        WorkspaceOperationChangeReason reason = WorkspaceOperationChangeReason.Unspecified,
        IReadOnlyList<string>? operationIds = null)
    {
        if (result.IsSuccess)
        {
            _operationChangeNotifier.NotifyWorkspaceOperationsChanged(
                workspaceId,
                changeKind,
                affectedAssets,
                reason,
                operationIds);
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
            var primaryAsset = new WorkspaceOperationAffectedAsset(
                affectedAsset.BundleId,
                affectedAsset.AssetId,
                WorkspaceOperationAffectedAssetRole.PrimaryTarget);
            if (!_associatedAssetImpactRegistry.TryGetAssociatedAssetImpacts(
                    workspaceId,
                    primaryAsset.BundleId,
                    primaryAsset.AssetId,
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

    private static IReadOnlyList<WorkspaceOperationAffectedAsset> CreateAffectedAssets(
        BundleId requestedBundleId,
        AssetId requestedAssetId,
        BundleId? actualTargetBundleId,
        AssetId? actualTargetAssetId)
    {
        if (actualTargetBundleId.HasValue && actualTargetAssetId.HasValue)
        {
            var affectedAssets = new List<WorkspaceOperationAffectedAsset>
            {
                new(
                    actualTargetBundleId.Value,
                    actualTargetAssetId.Value,
                    WorkspaceOperationAffectedAssetRole.PrimaryTarget)
            };
            if (!actualTargetBundleId.Value.Equals(requestedBundleId) ||
                !actualTargetAssetId.Value.Equals(requestedAssetId))
            {
                affectedAssets.Add(new WorkspaceOperationAffectedAsset(
                    requestedBundleId,
                    requestedAssetId,
                    WorkspaceOperationAffectedAssetRole.RelatedTarget));
            }

            return NormalizeAffectedAssets(affectedAssets);
        }

        return CreateAffectedAsset(requestedBundleId, requestedAssetId);
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

    private static IReadOnlyList<string>? CreateOperationIds(string? operationId) =>
        string.IsNullOrWhiteSpace(operationId)
            ? null
            : [operationId.Trim()];
}
