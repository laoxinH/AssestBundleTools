using System.Text;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static class AssetsToolsTextAssetPreviewClassifierTests
{
    public static Task TryDecodeReadableTextPayload_ShouldAcceptJsonSkelText()
    {
        var payload = Encoding.UTF8.GetBytes("""
            {"skeleton":{"spine":"3.8.99"},"bones":[{"name":"root"}]}
            """);

        var decoded = AssetsToolsTextAssetPreviewClassifier.TryDecodeReadableTextPayload(
            payload,
            out var textContent);

        TestAssert.True(decoded, "Readable JSON skeleton payloads should remain text previews.");
        TestAssert.True(textContent.Contains("\"skeleton\"", StringComparison.Ordinal), "Decoded text should preserve skeleton JSON.");
        return Task.CompletedTask;
    }

    public static Task TryDecodeJsonTextPayload_ShouldAcceptJsonSkelText()
    {
        var payload = Encoding.UTF8.GetBytes("""
            {"skeleton":{"spine":"3.8.99"},"bones":[{"name":"root"}]}
            """);

        var decoded = AssetsToolsTextAssetPreviewClassifier.TryDecodeJsonTextPayload(
            payload,
            out var textContent);

        TestAssert.True(decoded, "JSON skeleton payloads should remain text previews.");
        TestAssert.True(textContent.Contains("\"skeleton\"", StringComparison.Ordinal), "Decoded JSON should preserve skeleton metadata.");
        return Task.CompletedTask;
    }

    public static Task TryDecodeReadableTextPayload_ShouldRejectBinarySkelPayload()
    {
        var payload = new byte[] { 0x00, 0x01, 0x7F, 0x02, 0x03, 0x04 };

        var decoded = AssetsToolsTextAssetPreviewClassifier.TryDecodeReadableTextPayload(
            payload,
            out var textContent);

        TestAssert.False(decoded, "Binary skeleton payloads should not be displayed as text.");
        TestAssert.Equal(string.Empty, textContent, "Rejected binary payloads should not expose decoded text.");
        return Task.CompletedTask;
    }

    public static Task TryDecodeReadableTextPayload_ShouldRejectUtf8ControlPayload()
    {
        var payload = Encoding.UTF8.GetBytes("spine\0binary\u0001payload");

        var decoded = AssetsToolsTextAssetPreviewClassifier.TryDecodeReadableTextPayload(
            payload,
            out var textContent);

        TestAssert.False(decoded, "UTF-8 decodable skeleton payloads with binary control bytes should remain binary previews.");
        TestAssert.Equal(string.Empty, textContent, "Rejected binary-like text should not be projected as text.");
        return Task.CompletedTask;
    }

    public static Task TryDecodeJsonTextPayload_ShouldRejectReadableNonJsonPayload()
    {
        var payload = Encoding.UTF8.GetBytes("spine 3.8 root hip body atlas page names");

        var decoded = AssetsToolsTextAssetPreviewClassifier.TryDecodeJsonTextPayload(
            payload,
            out var textContent);

        TestAssert.False(decoded, "Readable but non-JSON skeleton payloads should remain binary previews.");
        TestAssert.Equal(string.Empty, textContent, "Rejected non-JSON payloads should keep their raw bytes for binary runtime parsing.");
        return Task.CompletedTask;
    }

    public static Task TryDecodeJsonTextPayload_ShouldRejectInvalidJsonPayload()
    {
        var payload = Encoding.UTF8.GetBytes("{ skeleton: binary-ish }");

        var decoded = AssetsToolsTextAssetPreviewClassifier.TryDecodeJsonTextPayload(
            payload,
            out var textContent);

        TestAssert.False(decoded, "Malformed JSON-looking skeleton payloads should remain binary previews.");
        TestAssert.Equal(string.Empty, textContent, "Malformed JSON-looking payloads should not be exposed as text.");
        return Task.CompletedTask;
    }

    public static Task IsSpineSkeletonPath_ShouldMatchSkelAndSkelBytes()
    {
        TestAssert.True(
            AssetsToolsTextAssetPreviewClassifier.IsSpineSkeletonPath("Assets/Hero/Hero.skel", "Hero"),
            ".skel TextAssets should use the skeleton text/binary classifier.");
        TestAssert.True(
            AssetsToolsTextAssetPreviewClassifier.IsSpineSkeletonPath("Assets/Hero/Hero.skel.bytes", "Hero"),
            ".skel.bytes TextAssets should use the skeleton text/binary classifier.");
        TestAssert.False(
            AssetsToolsTextAssetPreviewClassifier.IsSpineSkeletonPath("Assets/Hero/Hero.atlas", "Hero"),
            "Atlas text should stay on the normal TextAsset preview path.");
        return Task.CompletedTask;
    }
}
