using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 处理重做工作区替换命令。
/// </summary>
public sealed class RedoAssetReplacementHandler : ICommandHandler<RedoAssetReplacementCommand, Result<AssetReplacementRedoResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly WorkspaceReplacementOperationRecorder? _operationRecorder;

    public RedoAssetReplacementHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceReplacementOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _operationRecorder = operationRecorder;
    }

    public Task<Result<AssetReplacementRedoResultDto>> HandleAsync(
        RedoAssetReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetReplacementRedoResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.BundleId.Value))
        {
            return Task.FromResult(Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.AssetId.Value))
        {
            return Task.FromResult(Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(command.WorkspaceId, command.BundleId, command.AssetId))
        {
            return Task.FromResult(Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                out var targetDetails))
        {
            return Task.FromResult(Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        if (_workspaceSessionRegistry is not IWorkspaceReplacementRedoSessionRegistry redoSessionRegistry ||
            !redoSessionRegistry.TryRedoAssetReplacement(
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                out var redoResult))
        {
            return Task.FromResult(Result<AssetReplacementRedoResultDto>.Failure(
                ApplicationErrors.NoRedoReplacement(command.AssetId)));
        }

        _operationRecorder?.RecordRedoReplacement(command, targetDetails!, redoResult!);

        return Task.FromResult(Result<AssetReplacementRedoResultDto>.Success(redoResult!));
    }
}
