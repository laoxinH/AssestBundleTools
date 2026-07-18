using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Extensions;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static class AudioPreviewOnlineComponentMapperTests
{
    public static void AttachRequiredComponent_ShouldRequestVgmstream_WhenBackendIsUnavailable()
    {
        var message = new AssetPreviewMessageDto(
            AssetPreviewMessageKey.AudioTranscodeUnavailable,
            ["FSB5"]);
        var transcodeResult = new AudioPayloadTranscodeResultDto(
            AudioPayloadTranscodeStatusKind.Unavailable,
            AudioPayloadTranscodeIssueKind.BackendUnavailable,
            Transcoder: null,
            ContentType: null,
            Content: null);

        var mappedMessage = AudioPreviewOnlineComponentMapper.AttachRequiredComponent(
            message,
            transcodeResult);

        TestAssert.Equal(AssetPreviewMessageKey.AudioTranscodeUnavailable, mappedMessage.Key, "Unavailable transcoder should keep the stable preview message key.");
        TestAssert.Equal(OnlineComponentIds.VgmstreamCli, mappedMessage.RequiredComponentId, "Unavailable FSB5 transcode should request the managed vgmstream component.");
    }
}
