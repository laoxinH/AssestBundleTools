using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Extraction.Commands;

/// <summary>
/// 执行工作区级批量提取。
/// </summary>
public sealed class ExtractWorkspaceAssetsHandler : ICommandHandler<ExtractWorkspaceAssetsCommand, Result<WorkspaceBatchExtractionResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly ExtractAssetHandler? _extractAssetHandler;
    private readonly WorkspaceExtractionOperationRecorder? _operationRecorder;

    public ExtractWorkspaceAssetsHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        ExtractAssetHandler? extractAssetHandler = null,
        WorkspaceExtractionOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _extractAssetHandler = extractAssetHandler;
        _operationRecorder = operationRecorder;
    }

    public async Task<Result<WorkspaceBatchExtractionResultDto>> HandleAsync(
        ExtractWorkspaceAssetsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Result<WorkspaceBatchExtractionResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Result<WorkspaceBatchExtractionResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId));
        }

        var plan = WorkspaceBatchExtractionPlanner.BuildPlan(
            _workspaceSessionRegistry,
            command.WorkspaceId,
            command.AssetTypeFilter,
            command.OutputDirectory);

        if (plan.Assets.Count == 0)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Failure(
                ApplicationErrors.NoBatchExtractionCandidates(command.WorkspaceId));
        }

        var affectedBundles = new HashSet<string>(StringComparer.Ordinal);
        var extractedResults = new List<AssetExtractionResultDto>(plan.Assets.Count);
        var failures = new List<AppError>();
        foreach (var asset in plan.Assets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ExtractAssetAsync(command, asset, cancellationToken);
            if (result.IsCancelled)
            {
                return Result<WorkspaceBatchExtractionResultDto>.Cancelled();
            }

            if (!result.IsSuccess || result.Value is null)
            {
                failures.AddRange(result.Errors);
                continue;
            }

            extractedResults.Add(result.Value);
            affectedBundles.Add(asset.BundleId.Value);
        }

        if (extractedResults.Count == 0)
        {
            return failures.Count == 0
                ? Result<WorkspaceBatchExtractionResultDto>.Failure(
                    ApplicationErrors.NoBatchExtractionCandidates(command.WorkspaceId))
                : Result<WorkspaceBatchExtractionResultDto>.Failure(failures);
        }

        var batchResult = new WorkspaceBatchExtractionResultDto(
            WorkspaceId: command.WorkspaceId,
            AssetTypeFilter: command.AssetTypeFilter,
            SuggestedOutputDirectory: plan.SuggestedOutputDirectory,
            ExtractedAssetCount: extractedResults.Count,
            ExtractedBundleCount: affectedBundles.Count,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            FailedAssetCount: failures.Count,
            AssetResults: extractedResults);
        _operationRecorder?.RecordBatchExtraction(command, batchResult);

        return Result<WorkspaceBatchExtractionResultDto>.Success(batchResult);
    }

    private Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        ExtractWorkspaceAssetsCommand command,
        PlannedAssetExtraction asset,
        CancellationToken cancellationToken)
    {
        if (_extractAssetHandler is not null)
        {
            return _extractAssetHandler.HandleAsync(
                new ExtractAssetCommand(
                    command.WorkspaceId,
                    asset.BundleId,
                    asset.AssetId,
                    command.WorkspaceKey,
                    command.WorkspaceDataRootPath,
                    asset.SuggestedOutputPath),
                cancellationToken);
        }

        _workspaceSessionRegistry.TryExtractAsset(
            command.WorkspaceId,
            asset.BundleId,
            asset.AssetId,
            out var extractionResult);

        if (extractionResult is null)
        {
            return Task.FromResult(Result<AssetExtractionResultDto>.Failure(
                ApplicationErrors.AssetExtractionNotSupported(asset.AssetId)));
        }

        return Task.FromResult(Result<AssetExtractionResultDto>.Success(extractionResult!));
    }
}
