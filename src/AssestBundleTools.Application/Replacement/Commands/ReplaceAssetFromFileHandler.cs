using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 处理外部文件替换命令，并把结果写入工作区变更队列。
/// </summary>
public sealed class ReplaceAssetFromFileHandler : ICommandHandler<ReplaceAssetFromFileCommand, Result<AssetFileReplacementResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly WorkspaceReplacementOperationRecorder? _operationRecorder;

    public ReplaceAssetFromFileHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceReplacementOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _operationRecorder = operationRecorder;
    }

    public Task<Result<AssetFileReplacementResultDto>> HandleAsync(
        ReplaceAssetFromFileCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.TargetBundleId.Value))
        {
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.TargetAssetId.Value))
        {
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.SourceFilePath))
        {
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.SourceFilePathRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.TargetBundleId, command.TargetAssetId, out var targetDetails))
        {
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.AssetNotFound(command.TargetAssetId)));
        }

        if (targetDetails!.AssetType != command.SourceAssetType)
        {
            return Task.FromResult(Result<AssetFileReplacementResultDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(command.SourceAssetType, targetDetails.AssetType)));
        }

        _workspaceSessionRegistry.TryReplaceAssetFromFile(
            command.WorkspaceId,
            command.TargetBundleId,
            command.TargetAssetId,
            command.SourceFilePath,
            command.SourceAssetType,
            out var replacementResult);

        _operationRecorder?.RecordFileReplacement(command, targetDetails!, replacementResult!);

        return Task.FromResult(Result<AssetFileReplacementResultDto>.Success(replacementResult!));
    }
}
