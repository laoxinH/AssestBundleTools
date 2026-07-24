using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 通过现有预览管线比较目标与外部来源的 Spine skeleton 载荷格式。
/// </summary>
public sealed class SpineSkeletonReplacementCompatibilityService
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> _assetPreviewHandler;
    private readonly IReplacementPreviewSourceProvider _replacementPreviewSourceProvider;

    public SpineSkeletonReplacementCompatibilityService(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewHandler,
        IReplacementPreviewSourceProvider replacementPreviewSourceProvider)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _assetPreviewHandler = assetPreviewHandler ?? throw new ArgumentNullException(nameof(assetPreviewHandler));
        _replacementPreviewSourceProvider = replacementPreviewSourceProvider ?? throw new ArgumentNullException(nameof(replacementPreviewSourceProvider));
    }

    public async Task<Result<SpineSkeletonFormatCompatibilityDto>> CheckAsync(
        WorkspaceId workspaceId,
        string workspaceKey,
        string workspaceDataRootPath,
        BundleId targetBundleId,
        AssetId targetAssetId,
        string sourceFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!_workspaceSessionRegistry.TryGetAssetDetails(
                workspaceId,
                targetBundleId,
                targetAssetId,
                out var targetDetails) ||
            targetDetails is null)
        {
            return Result<SpineSkeletonFormatCompatibilityDto>.Failure(
                Common.Errors.ApplicationErrors.AssetNotFound(targetAssetId));
        }

        var targetResult = await _assetPreviewHandler.HandleAsync(
            new GetAssetPreviewQuery(
                workspaceId,
                workspaceKey,
                workspaceDataRootPath,
                targetBundleId,
                targetAssetId,
                ContentScope: AssetPreviewContentScope.Original),
            cancellationToken);
        if (targetResult.IsCancelled)
        {
            return Result<SpineSkeletonFormatCompatibilityDto>.Cancelled();
        }

        if (!targetResult.IsSuccess || targetResult.Value is null)
        {
            return Result<SpineSkeletonFormatCompatibilityDto>.Failure(targetResult.Errors);
        }

        var sourceResult = await _replacementPreviewSourceProvider.LoadFilePreviewAsync(
            targetDetails,
            sourceFilePath,
            cancellationToken);
        if (sourceResult.IsCancelled)
        {
            return Result<SpineSkeletonFormatCompatibilityDto>.Cancelled();
        }

        if (!sourceResult.IsSuccess || sourceResult.Value is null)
        {
            return Result<SpineSkeletonFormatCompatibilityDto>.Failure(sourceResult.Errors);
        }

        var targetFormat = SpineSkeletonPayloadFormatClassifier.Classify(targetResult.Value);
        var sourceFormat = SpineSkeletonPayloadFormatClassifier.Classify(sourceResult.Value);
        return Result<SpineSkeletonFormatCompatibilityDto>.Success(
            new SpineSkeletonFormatCompatibilityDto(
                targetFormat,
                sourceFormat,
                targetFormat != SpineSkeletonAssetFormat.Unknown && targetFormat == sourceFormat));
    }
}
