using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Reading;

internal static class AssetsToolsAudioClipSizeTests
{
    private const string PreviousParserVersion = "assetstools.net/3.0.4-dependency-graph.8";
    private const string CurrentParserVersion = "assetstools.net/3.0.4-dependency-graph.9";
    private const string RealSamplePath = @"C:\Users\thixi\Downloads\ABT-Guide-Recording\01-import\Audio-Sample.bundle";

    public static Task CacheParserVersion_ShouldInvalidateIndexesWithoutSourcePayloadSizes()
    {
        var reader = new AssetsToolsAssetBundleReader();

        TestAssert.Equal(CurrentParserVersion, reader.CacheParserVersion, "Reader should publish the source-size metadata parser version.");
        TestAssert.False(
            reader.SupportsCacheParserVersion(PreviousParserVersion),
            "Indexes created before source payload sizes were added must be rebuilt.");
        return Task.CompletedTask;
    }

    public static async Task ReadAsync_ShouldExposeAudioPayloadSize_FromRealSampleWhenAvailable()
    {
        if (!File.Exists(RealSamplePath))
        {
            Console.WriteLine($"[SKIP] AudioClip sample bundle not found: {RealSamplePath}");
            return;
        }

        var reader = new AssetsToolsAssetBundleReader();
        var result = await reader.ReadAsync(new AssetBundleReadRequestDto(
            BundlePath: RealSamplePath,
            LogicalPath: Path.GetFileName(RealSamplePath),
            WorkspaceKey: "audio-size-real-sample",
            WorkspaceDataRootPath: Path.GetTempPath()));

        TestAssert.True(result.IsSuccess, "AudioClip sample bundle should be readable.");
        var bundle = TestAssert.NotNull(result.Value, "Reader should return the AudioClip sample index.");
        var asset = TestAssert.NotNull(
            bundle.Assets.SingleOrDefault(static candidate =>
                string.Equals(candidate.AssetType, AssetType.AudioClip.ToString(), StringComparison.Ordinal) &&
                string.Equals(candidate.AssetName, "00_FOLEY_walk_dirt_3", StringComparison.Ordinal)),
            "AudioClip sample should contain 00_FOLEY_walk_dirt_3.");

        TestAssert.Equal(172L, asset.SizeInBytes, "Serialized AudioClip record size should remain available.");
        TestAssert.Equal<long?>(17_376L, asset.PreferredDisplaySizeInBytes, "List source size should use m_Resource.m_Size.");
    }
}
