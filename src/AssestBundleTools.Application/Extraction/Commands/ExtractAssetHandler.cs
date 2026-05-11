using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 处理单资源提取请求，并把结果记录到工作区会话中。
/// </summary>
public sealed class ExtractAssetHandler : ICommandHandler<ExtractAssetCommand, Result<AssetExtractionResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly WorkspaceAssetExtractionExporter? _extractionExporter;
    private readonly WorkspaceExtractionOperationRecorder? _operationRecorder;

    public ExtractAssetHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceAssetExtractionExporter? extractionExporter = null,
        WorkspaceExtractionOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _extractionExporter = extractionExporter;
        _operationRecorder = operationRecorder;
    }

    public async Task<Result<AssetExtractionResultDto>> HandleAsync(
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetExtractionResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (string.IsNullOrWhiteSpace(command.BundleId.Value))
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.BundleIdRequired());
        }

        if (string.IsNullOrWhiteSpace(command.AssetId.Value))
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.AssetIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(command.WorkspaceId, command.BundleId))
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.BundleNotFound(command.BundleId));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(command.WorkspaceId, command.BundleId, command.AssetId))
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                out var targetDetails) ||
            targetDetails is null)
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId));
        }

        if (!targetDetails.CanExtract)
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.AssetExtractionNotSupported(command.AssetId));
        }

        var suggestedOutputPath = ResolveSuggestedOutputPath(command, targetDetails);
        var extractionResult = new AssetExtractionResultDto(
            WorkspaceId: command.WorkspaceId,
            BundleId: command.BundleId,
            AssetId: command.AssetId,
            AssetName: targetDetails.Name,
            SuggestedOutputPath: suggestedOutputPath,
            ExtractedAtUtc: DateTimeOffset.UtcNow);
        var effectiveCommand = command with { SuggestedOutputPath = suggestedOutputPath };

        if (_extractionExporter is not null)
        {
            var exportResult = await _extractionExporter.ExportAsync(
                effectiveCommand,
                targetDetails,
                extractionResult,
                cancellationToken);
            if (exportResult.IsCancelled)
            {
                return Result<AssetExtractionResultDto>.Cancelled();
            }

            if (!exportResult.IsSuccess || exportResult.Value is null)
            {
                return Result<AssetExtractionResultDto>.Failure(exportResult.Errors);
            }

            extractionResult = exportResult.Value;
        }

        if (!_workspaceSessionRegistry.TryExtractAsset(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            out var committedExtractionResult) ||
            committedExtractionResult is null)
        {
            return Result<AssetExtractionResultDto>.Failure(ApplicationErrors.AssetExtractionNotSupported(command.AssetId));
        }

        extractionResult = extractionResult with
        {
            ExtractedAtUtc = committedExtractionResult.ExtractedAtUtc
        };

        _operationRecorder?.RecordAssetExtraction(
            effectiveCommand,
            targetDetails,
            extractionResult);

        return Result<AssetExtractionResultDto>.Success(extractionResult);
    }

    private string ResolveSuggestedOutputPath(ExtractAssetCommand command, AssetDetailsDto targetDetails)
    {
        if (!string.IsNullOrWhiteSpace(command.SuggestedOutputPath))
        {
            return command.SuggestedOutputPath.Trim();
        }

        if (_workspaceSessionRegistry.TryGetAssetList(
                command.WorkspaceId,
                command.BundleId,
                searchKeyword: null,
                assetTypeFilter: null,
                assetTypeDisplayNameFilter: null,
                out var assetListResult) &&
            assetListResult is not null)
        {
            var assetListItem = assetListResult.Items.FirstOrDefault(item =>
                item.AssetId.Equals(command.AssetId));
            if (assetListItem is not null)
            {
                return WorkspaceBatchExtractionPlanner.BuildSuggestedOutputPath(
                    WorkspaceBatchExtractionPlanner.ResolveOutputDirectory(null, command.WorkspaceId, null),
                    assetListResult.BundleDisplayName,
                    assetListItem.Name,
                    assetListItem.AssetType);
            }
        }

        return WorkspaceBatchExtractionPlanner.BuildSuggestedOutputPath(
            WorkspaceBatchExtractionPlanner.ResolveOutputDirectory(null, command.WorkspaceId, null),
            command.BundleId.Value,
            targetDetails.Name,
            targetDetails.AssetType);
    }
}
