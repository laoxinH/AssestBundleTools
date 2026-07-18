using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Extensions;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;

internal static class AudioPreviewOnlineComponentMapper
{
    public static AssetPreviewMessageDto AttachRequiredComponent(
        AssetPreviewMessageDto message,
        AudioPayloadTranscodeResultDto transcodeResult)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(transcodeResult);

        return transcodeResult.Status == AudioPayloadTranscodeStatusKind.Unavailable
            ? message with { RequiredComponentId = OnlineComponentIds.VgmstreamCli }
            : message;
    }
}
