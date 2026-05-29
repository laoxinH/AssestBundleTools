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
    private readonly IWorkspaceAssociatedAssetImpactRegistry? _associatedAssetImpactRegistry;

    public RedoAssetReplacementHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceReplacementOperationRecorder? operationRecorder = null,
        IWorkspaceAssociatedAssetImpactRegistry? associatedAssetImpactRegistry = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _operationRecorder = operationRecorder;
        _associatedAssetImpactRegistry = associatedAssetImpactRegistry;
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
            return Task.FromResult(RedoSpecificReplacement(command));
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
        RedoAssetReplacementCommand command)
    {
        if (_operationRecorder is null ||
            !_operationRecorder.TryFindReplacementOperation(command, out var replacementOperation) ||
            replacementOperation?.Source is null ||
            !WorkspaceReplacementOperationContextMatcher.CanUseFromContext(
                replacementOperation,
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                _associatedAssetImpactRegistry) ||
            !TryResolveReplacementTarget(replacementOperation, out var targetBundleId, out var targetAssetId) ||
            !_workspaceSessionRegistry.TryGetAssetDetails(
                command.WorkspaceId,
                targetBundleId,
                targetAssetId,
                out var targetDetails) ||
            targetDetails is null)
        {
            return Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.NoRedoReplacement(command.AssetId));
        }

        var targetCommand = command with
        {
            BundleId = targetBundleId,
            AssetId = targetAssetId
        };
        var redoResult = replacementOperation.OperationKind switch
        {
            WorkspaceOperationKind.ReplaceFromFile => RedoFileReplacement(targetCommand, targetDetails, replacementOperation),
            WorkspaceOperationKind.ReplaceFromWorkspace => RedoWorkspaceReplacement(targetCommand, replacementOperation),
            _ => null
        };
        if (redoResult is null)
        {
            return Result<AssetReplacementRedoResultDto>.Failure(ApplicationErrors.NoRedoReplacement(command.AssetId));
        }

        _operationRecorder.RecordRedoReplacement(targetCommand, targetDetails, redoResult, replacementOperation);
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

    private static bool TryResolveReplacementTarget(
        WorkspaceOperationRecordDto? operation,
        out BundleId bundleId,
        out AssetId assetId)
    {
        if (BundleId.TryParse(operation?.Target?.BundleId, out bundleId) &&
            AssetId.TryParse(operation?.Target?.AssetId, out assetId))
        {
            return true;
        }

        bundleId = default;
        assetId = default;
        return false;
    }
}
