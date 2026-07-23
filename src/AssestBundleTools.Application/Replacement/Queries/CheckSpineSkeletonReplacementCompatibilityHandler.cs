using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;

namespace AssestBundleTools.Application.Replacement.Queries;

public sealed class CheckSpineSkeletonReplacementCompatibilityHandler :
    IQueryHandler<CheckSpineSkeletonReplacementCompatibilityQuery, Result<SpineSkeletonFormatCompatibilityDto>>
{
    private readonly SpineSkeletonReplacementCompatibilityService _compatibilityService;

    public CheckSpineSkeletonReplacementCompatibilityHandler(
        SpineSkeletonReplacementCompatibilityService compatibilityService)
    {
        _compatibilityService = compatibilityService ?? throw new ArgumentNullException(nameof(compatibilityService));
    }

    public Task<Result<SpineSkeletonFormatCompatibilityDto>> HandleAsync(
        CheckSpineSkeletonReplacementCompatibilityQuery query,
        CancellationToken cancellationToken = default) =>
        _compatibilityService.CheckAsync(
            query.WorkspaceId,
            query.WorkspaceKey,
            query.WorkspaceDataRootPath,
            query.TargetBundleId,
            query.TargetAssetId,
            query.SourceFilePath,
            cancellationToken);
}
