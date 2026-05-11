using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 处理工作区内资源替换，并把结果写入工作区变更队列。
/// </summary>
public sealed class ReplaceAssetFromWorkspaceHandler : ICommandHandler<ReplaceAssetFromWorkspaceCommand, Result<AssetReplacementResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly WorkspaceReplacementOperationRecorder? _operationRecorder;

    public ReplaceAssetFromWorkspaceHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceReplacementOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _operationRecorder = operationRecorder;
    }

    public Task<Result<AssetReplacementResultDto>> HandleAsync(
        ReplaceAssetFromWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetReplacementResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetReplacementResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.TargetBundleId.Value) || string.IsNullOrWhiteSpace(command.SourceBundleId.Value))
        {
            return Task.FromResult(Result<AssetReplacementResultDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.TargetAssetId.Value) || string.IsNullOrWhiteSpace(command.SourceAssetId.Value))
        {
            return Task.FromResult(Result<AssetReplacementResultDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<AssetReplacementResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.TargetBundleId, command.TargetAssetId, out var targetDetails))
        {
            return Task.FromResult(Result<AssetReplacementResultDto>.Failure(ApplicationErrors.AssetNotFound(command.TargetAssetId)));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.SourceBundleId, command.SourceAssetId, out var sourceDetails))
        {
            return Task.FromResult(Result<AssetReplacementResultDto>.Failure(ApplicationErrors.AssetNotFound(command.SourceAssetId)));
        }

        if (targetDetails!.AssetType != sourceDetails!.AssetType)
        {
            return Task.FromResult(Result<AssetReplacementResultDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(sourceDetails.AssetType, targetDetails.AssetType)));
        }

        _workspaceSessionRegistry.TryReplaceAssetFromWorkspace(
            command.WorkspaceId,
            command.TargetBundleId,
            command.TargetAssetId,
            command.SourceBundleId,
            command.SourceAssetId,
            out var replacementResult);

        _operationRecorder?.RecordWorkspaceReplacement(command, targetDetails!, sourceDetails!, replacementResult!);

        return Task.FromResult(Result<AssetReplacementResultDto>.Success(replacementResult!));
    }
}
