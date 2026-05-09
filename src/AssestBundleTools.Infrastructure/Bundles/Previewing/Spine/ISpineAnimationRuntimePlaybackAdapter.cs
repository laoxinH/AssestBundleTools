using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal interface ISpineAnimationRuntimePlaybackAdapter
{
    string AdapterKey { get; }

    SpineAnimationRuntimePlaybackSessionResultDto TryCreatePlaybackSession(
        SpineAnimationRuntimePlaybackSourceDto source,
        CancellationToken cancellationToken);
}
