using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

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

        if (!string.IsNullOrWhiteSpace(command.ReplacementOperationId))
        {
            return Task.FromResult(RedoSpecificReplacement(command, targetDetails!));
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

    private Result<AssetReplacementRedoResultDto> RedoSpecificReplacement(
        RedoAssetReplacementCommand command,
        AssetDetailsDto targetDetails)
    {
        if (_operationRecorder is null ||
            !_operationRecorder.TryFindReplacementOperation(command, out var replacementOperation) ||
            replacementOperation?.Source is null)
        {
            return Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.NoRedoReplacement(command.AssetId));
        }

        var redoResult = replacementOperation.OperationKind switch
        {
            WorkspaceOperationKind.ReplaceFromFile => RedoFileReplacement(command, targetDetails, replacementOperation),
            WorkspaceOperationKind.ReplaceFromWorkspace => RedoWorkspaceReplacement(command, replacementOperation),
            _ => null
        };
        if (redoResult is null)
        {
            return Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.NoRedoReplacement(command.AssetId));
        }

        _operationRecorder.RecordRedoReplacement(command, targetDetails, redoResult, replacementOperation);
        return Result<AssetReplacementRedoResultDto>.Success(redoResult);
    }

    private AssetReplacementRedoResultDto? RedoFileReplacement(
        RedoAssetReplacementCommand command,
        AssetDetailsDto targetDetails,
        WorkspaceOperationRecordDto replacementOperation)
    {
        if (string.IsNullOrWhiteSpace(replacementOperation.Source?.SourceFilePath))
        {
            return null;
        }

        var sourceAssetType = ParseAssetType(
            replacementOperation.Source.SourceAssetType,
            targetDetails.AssetType);
        if (sourceAssetType != targetDetails.AssetType ||
            !_workspaceSessionRegistry.TryReplaceAssetFromFile(
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                replacementOperation.Source.SourceFilePath,
                sourceAssetType,
                out var fileReplacementResult) ||
            fileReplacementResult is null)
        {
            return null;
        }

        return new AssetReplacementRedoResultDto(
            fileReplacementResult.WorkspaceId,
            fileReplacementResult.TargetBundleId,
            fileReplacementResult.TargetAssetId,
            fileReplacementResult.TargetAssetName,
            fileReplacementResult.ReplacedAtUtc);
    }

    private AssetReplacementRedoResultDto? RedoWorkspaceReplacement(
        RedoAssetReplacementCommand command,
        WorkspaceOperationRecordDto replacementOperation)
    {
        if (!BundleId.TryParse(replacementOperation.Source?.SourceBundleId, out var sourceBundleId) ||
            !AssetId.TryParse(replacementOperation.Source?.SourceAssetId, out var sourceAssetId) ||
            !_workspaceSessionRegistry.TryReplaceAssetFromWorkspace(
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                sourceBundleId,
                sourceAssetId,
                out var workspaceReplacementResult) ||
            workspaceReplacementResult is null)
        {
            return null;
        }

        return new AssetReplacementRedoResultDto(
            workspaceReplacementResult.WorkspaceId,
            workspaceReplacementResult.TargetBundleId,
            workspaceReplacementResult.TargetAssetId,
            workspaceReplacementResult.TargetAssetName,
            workspaceReplacementResult.ReplacedAtUtc);
    }

    private static AssetType ParseAssetType(string? value, AssetType fallback) =>
        Enum.TryParse<AssetType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
}
