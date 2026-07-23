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
    private readonly SpineSkeletonReplacementCompatibilityService? _spineSkeletonCompatibilityService;

    public ReplaceAssetFromFileHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceReplacementOperationRecorder? operationRecorder = null,
        SpineSkeletonReplacementCompatibilityService? spineSkeletonCompatibilityService = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _operationRecorder = operationRecorder;
        _spineSkeletonCompatibilityService = spineSkeletonCompatibilityService;
    }

    public async Task<Result<AssetFileReplacementResultDto>> HandleAsync(
        ReplaceAssetFromFileCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetFileReplacementResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (string.IsNullOrWhiteSpace(command.TargetBundleId.Value))
        {
            return Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.BundleIdRequired());
        }

        if (string.IsNullOrWhiteSpace(command.TargetAssetId.Value))
        {
            return Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.AssetIdRequired());
        }

        if (string.IsNullOrWhiteSpace(command.SourceFilePath))
        {
            return Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.SourceFilePathRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.TargetBundleId, command.TargetAssetId, out var targetDetails))
        {
            return Result<AssetFileReplacementResultDto>.Failure(ApplicationErrors.AssetNotFound(command.TargetAssetId));
        }

        if (targetDetails!.AssetType != command.SourceAssetType)
        {
            return Result<AssetFileReplacementResultDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(command.SourceAssetType, targetDetails.AssetType));
        }

        if (command.RequireSpineSkeletonFormatMatch)
        {
            if (_spineSkeletonCompatibilityService is null)
            {
                return Result<AssetFileReplacementResultDto>.Failure(
                    ApplicationErrors.SpineSkeletonFormatMismatch("Unknown", "Unknown"));
            }

            var compatibilityResult = await _spineSkeletonCompatibilityService.CheckAsync(
                command.WorkspaceId,
                command.WorkspaceKey ?? string.Empty,
                command.WorkspaceDataRootPath ?? string.Empty,
                command.TargetBundleId,
                command.TargetAssetId,
                command.SourceFilePath,
                cancellationToken);
            if (compatibilityResult.IsCancelled)
            {
                return Result<AssetFileReplacementResultDto>.Cancelled();
            }

            if (!compatibilityResult.IsSuccess || compatibilityResult.Value is null)
            {
                return Result<AssetFileReplacementResultDto>.Failure(compatibilityResult.Errors);
            }

            if (!compatibilityResult.Value.IsCompatible)
            {
                return Result<AssetFileReplacementResultDto>.Failure(
                    ApplicationErrors.SpineSkeletonFormatMismatch(
                        compatibilityResult.Value.TargetFormat.ToString(),
                        compatibilityResult.Value.SourceFormat.ToString()));
            }
        }

        _workspaceSessionRegistry.TryReplaceAssetFromFile(
            command.WorkspaceId,
            command.TargetBundleId,
            command.TargetAssetId,
            command.SourceFilePath,
            command.SourceAssetType,
            out var replacementResult);

        _operationRecorder?.RecordFileReplacement(command, targetDetails!, replacementResult!);

        return Result<AssetFileReplacementResultDto>.Success(replacementResult!);
    }
}
