using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal interface ISpineAnimationRuntimeAdapter
{
    string AdapterKey { get; }

    SpineAnimationRuntimePreviewResultDto TryBuildPreview(
        SpineAnimationRuntimePreviewRequestDto request,
        IAssetPreviewTextSourceReader textSourceReader,
        CancellationToken cancellationToken);
}
