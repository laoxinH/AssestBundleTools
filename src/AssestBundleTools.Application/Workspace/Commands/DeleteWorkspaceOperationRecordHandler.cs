using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 删除工作区操作记录，并让当前会话按更新后的事实流重放。
/// </summary>
public sealed class DeleteWorkspaceOperationRecordHandler : ICommandHandler<DeleteWorkspaceOperationRecordCommand, Result<WorkspaceOperationRecordDeletedResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceOperationLogService _operationLogService;

    public DeleteWorkspaceOperationRecordHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceOperationLogService operationLogService)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _operationLogService = operationLogService ?? throw new ArgumentNullException(nameof(operationLogService));
    }

    public Task<Result<WorkspaceOperationRecordDeletedResultDto>> HandleAsync(
        DeleteWorkspaceOperationRecordCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.OperationId))
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(
                ApplicationErrors.WorkspaceOperationRecordNotFound(string.Empty)));
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(
                ApplicationErrors.WorkspaceImportWorkspaceKeyRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(
                ApplicationErrors.WorkspaceImportDataRootPathRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(
                ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        var workspaceKey = command.WorkspaceKey.Trim();
        var store = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(command.WorkspaceDataRootPath.Trim());
        var changeSet = store.Load(workspaceKey) ?? WorkspaceChangeSetDto.Empty(workspaceKey);
        var deletedOperation = (changeSet.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .FirstOrDefault(operation => string.Equals(
                operation.OperationId,
                command.OperationId.Trim(),
                StringComparison.Ordinal));
        if (deletedOperation is null)
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(
                ApplicationErrors.WorkspaceOperationRecordNotFound(command.OperationId)));
        }

        if (IsProtectedOperationRecord(deletedOperation))
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(
                ApplicationErrors.WorkspaceOperationRecordDeleteNotAllowed(deletedOperation.OperationId)));
        }

        var deletedAtUtc = DateTimeOffset.UtcNow;
        var updatedChangeSet = _operationLogService.DeleteOperationRecord(
            changeSet,
            deletedOperation.OperationId,
            deletedAtUtc);
        store.Save(updatedChangeSet);

        if (_workspaceSessionRegistry is IWorkspaceOperationReplaySessionRegistry replaySessionRegistry)
        {
            replaySessionRegistry.TryReplayWorkspaceOperations(command.WorkspaceId, updatedChangeSet);
        }

        var result = new WorkspaceOperationRecordDeletedResultDto(
            command.WorkspaceId,
            deletedOperation.OperationId,
            TryParseBundleId(deletedOperation.Target?.BundleId),
            TryParseAssetId(deletedOperation.Target?.AssetId),
            deletedAtUtc,
            CreateAffectedTargets(deletedOperation));
        return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Success(result));
    }

    private static bool IsProtectedOperationRecord(WorkspaceOperationRecordDto operation) =>
        operation.OperationKind is WorkspaceOperationKind.ExtractAsset or
            WorkspaceOperationKind.ExtractBatch;

    private static IReadOnlyList<WorkspaceOperationTargetDto> CreateAffectedTargets(
        WorkspaceOperationRecordDto operation)
    {
        var targets = new List<WorkspaceOperationTargetDto>();
        if (operation.Target is not null)
        {
            targets.Add(operation.Target);
        }

        foreach (var relation in operation.Relations ?? Array.Empty<WorkspaceOperationRelationDto>())
        {
            if (relation.RelatedTarget is not null)
            {
                targets.Add(relation.RelatedTarget);
            }
        }

        return targets
            .Where(static target => !string.IsNullOrWhiteSpace(target.BundleId) && !string.IsNullOrWhiteSpace(target.AssetId))
            .GroupBy(static target => string.Concat(target.BundleId, "\n", target.AssetId), StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static BundleId? TryParseBundleId(string? value) =>
        BundleId.TryParse(value, out var bundleId)
            ? bundleId
            : null;

    private static AssetId? TryParseAssetId(string? value) =>
        AssetId.TryParse(value, out var assetId)
            ? assetId
            : null;
}
