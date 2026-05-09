using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal interface ISpineAnimationRuntimeAdapter
{
    string AdapterKey { get; }

    SpineAnimationRuntimePreviewResultDto TryBuildPreview(
        SpineAnimationRuntimePreviewRequestDto request,
        CancellationToken cancellationToken);
}
