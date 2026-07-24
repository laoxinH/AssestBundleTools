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
        var originalOperations = changeSet.Operations ?? Array.Empty<WorkspaceOperationRecordDto>();
        var requestedOperationIds = CreateRequestedOperationIds(command);
        var requestedOperations = requestedOperationIds
            .Select(operationId => originalOperations.FirstOrDefault(operation => string.Equals(
                operation.OperationId,
                operationId,
                StringComparison.Ordinal)))
            .ToArray();
        var missingOperationIndex = Array.FindIndex(requestedOperations, static operation => operation is null);
        if (missingOperationIndex >= 0)
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(
                ApplicationErrors.WorkspaceOperationRecordNotFound(requestedOperationIds[missingOperationIndex])));
        }

        var protectedOperation = requestedOperations.FirstOrDefault(operation =>
            operation is not null && IsProtectedOperationRecord(operation));
        if (protectedOperation is not null)
        {
            return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Failure(
                ApplicationErrors.WorkspaceOperationRecordDeleteNotAllowed(protectedOperation.OperationId)));
        }

        var deletedAtUtc = DateTimeOffset.UtcNow;
        var updatedChangeSet = changeSet;
        foreach (var operationId in requestedOperationIds)
        {
            if (!(updatedChangeSet.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
                .Any(operation => string.Equals(operation.OperationId, operationId, StringComparison.Ordinal)))
            {
                continue;
            }

            updatedChangeSet = _operationLogService.DeleteOperationRecord(
                updatedChangeSet,
                operationId,
                deletedAtUtc);
        }

        var remainingOperationIds = (updatedChangeSet.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Select(static operation => operation.OperationId)
            .ToHashSet(StringComparer.Ordinal);
        var actuallyDeletedOperations = originalOperations
            .Where(operation => !remainingOperationIds.Contains(operation.OperationId))
            .ToArray();
        store.Save(updatedChangeSet);

        if (_workspaceSessionRegistry is IWorkspaceOperationReplaySessionRegistry replaySessionRegistry)
        {
            replaySessionRegistry.TryReplayWorkspaceOperations(command.WorkspaceId, updatedChangeSet);
        }

        var deletedOperation = requestedOperations[0]!;
        var result = new WorkspaceOperationRecordDeletedResultDto(
            command.WorkspaceId,
            deletedOperation.OperationId,
            TryParseBundleId(deletedOperation.Target?.BundleId),
            TryParseAssetId(deletedOperation.Target?.AssetId),
            deletedAtUtc,
            CreateAffectedTargets(actuallyDeletedOperations),
            actuallyDeletedOperations.Select(static operation => operation.OperationId).ToArray());
        return Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Success(result));
    }

    private static IReadOnlyList<string> CreateRequestedOperationIds(DeleteWorkspaceOperationRecordCommand command) =>
        new[] { command.OperationId }
            .Concat(command.AdditionalOperationIds ?? Array.Empty<string>())
            .Where(static operationId => !string.IsNullOrWhiteSpace(operationId))
            .Select(static operationId => operationId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static bool IsProtectedOperationRecord(WorkspaceOperationRecordDto operation) =>
        operation.OperationKind is WorkspaceOperationKind.ExtractAsset or
            WorkspaceOperationKind.ExtractBatch;

    private static IReadOnlyList<WorkspaceOperationTargetDto> CreateAffectedTargets(
        IEnumerable<WorkspaceOperationRecordDto> operations)
    {
        var targets = new List<WorkspaceOperationTargetDto>();
        foreach (var operation in operations)
        {
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
