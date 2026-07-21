using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Internal;

internal static class AssetsToolsAssetDisplayMetadataResolverTests
{
    public static Task SelectPreferredAudioSizeInBytes_ShouldPreferStreamedResourceSize()
    {
        var result = AssetsToolsAssetDisplayMetadataResolver.SelectPreferredAudioSizeInBytes(
            resourceSize: 17_888,
            inlineDataSize: 63_276);

        TestAssert.Equal<long?>(17_888, result, "Streamed AudioClip payload size should be the preferred display size.");
        return Task.CompletedTask;
    }

    public static Task SelectPreferredAudioSizeInBytes_ShouldUseInlinePayloadAsFallback()
    {
        var result = AssetsToolsAssetDisplayMetadataResolver.SelectPreferredAudioSizeInBytes(
            resourceSize: 0,
            inlineDataSize: 17_888);

        TestAssert.Equal<long?>(17_888, result, "Inline AudioClip payload size should be used when no streamed resource exists.");
        return Task.CompletedTask;
    }

    public static Task SelectPreferredAudioSizeInBytes_ShouldIgnoreMissingOrInvalidSizes()
    {
        var result = AssetsToolsAssetDisplayMetadataResolver.SelectPreferredAudioSizeInBytes(
            resourceSize: -1,
            inlineDataSize: null);

        TestAssert.Null(result, "Invalid AudioClip payload metadata should fall back to the serialized record size upstream.");
        return Task.CompletedTask;
    }
}
