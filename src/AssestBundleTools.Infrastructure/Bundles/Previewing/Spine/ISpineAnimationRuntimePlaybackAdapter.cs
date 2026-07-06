using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal interface ISpineAnimationRuntimePlaybackAdapter
{
    string AdapterKey { get; }

    SpineAnimationRuntimePlaybackSessionResultDto TryCreatePlaybackSession(
        SpineAnimationRuntimePlaybackSourceDto source,
        IAssetPreviewTextSourceReader textSourceReader,
        CancellationToken cancellationToken);
}
