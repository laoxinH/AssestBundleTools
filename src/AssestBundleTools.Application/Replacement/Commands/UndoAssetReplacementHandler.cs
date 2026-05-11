using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 处理撤销工作区替换命令。
/// </summary>
public sealed class UndoAssetReplacementHandler : ICommandHandler<UndoAssetReplacementCommand, Result<AssetReplacementUndoResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly WorkspaceReplacementOperationRecorder? _operationRecorder;

    public UndoAssetReplacementHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceReplacementOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _operationRecorder = operationRecorder;
    }

    public Task<Result<AssetReplacementUndoResultDto>> HandleAsync(
        UndoAssetReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetReplacementUndoResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetReplacementUndoResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.BundleId.Value))
        {
            return Task.FromResult(Result<AssetReplacementUndoResultDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.AssetId.Value))
        {
            return Task.FromResult(Result<AssetReplacementUndoResultDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<AssetReplacementUndoResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(command.WorkspaceId, command.BundleId, command.AssetId))
        {
            return Task.FromResult(Result<AssetReplacementUndoResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        _workspaceSessionRegistry.TryGetAssetDetails(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            out var targetDetails);

        if (!_workspaceSessionRegistry.TryUndoAssetReplacement(
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                out var undoResult))
        {
            return Task.FromResult(Result<AssetReplacementUndoResultDto>.Failure(
                ApplicationErrors.NoAppliedReplacement(command.AssetId)));
        }

        _operationRecorder?.RecordUndoReplacement(command, targetDetails!, undoResult!);

        return Task.FromResult(Result<AssetReplacementUndoResultDto>.Success(undoResult!));
    }
}
