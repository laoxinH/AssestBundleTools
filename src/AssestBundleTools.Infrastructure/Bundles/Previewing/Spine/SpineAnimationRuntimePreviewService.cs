using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

/// <summary>
/// Spine runtime adapter 组合点；真实官方 runtime 按版本在此层封装。
/// </summary>
public sealed class SpineAnimationRuntimePreviewService : ISpineAnimationRuntimePreviewService
{
    public Task<SpineAnimationRuntimePreviewResultDto> TryBuildPreviewAsync(
        SpineAnimationRuntimePreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(cancellationToken.IsCancellationRequested
            ? SpineAnimationRuntimePreviewResultDto.Cancelled
            : SpineAnimationRuntimePreviewResultDto.NotHandled);
    }
}
