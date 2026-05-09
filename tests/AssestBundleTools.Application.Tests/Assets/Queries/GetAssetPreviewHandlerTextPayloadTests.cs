using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldCacheFullText_AndExposeBoundedDisplayText_WhenGeneratedTextIsLong()
    {
        const string resolvedBundlePath = @"C:\Temp\long-text.bundle";
        const int maxDisplayCharacters = 24000;

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    Decryptor: decryptorDescriptor,
                    ResolvedBundlePath: resolvedBundlePath,
                    ProducedDerivedFile: false,
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var fullTextContent = new string('a', maxDisplayCharacters + 10) + "tail";
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain; charset=utf-8",
                    Content: Encoding.UTF8.GetBytes(fullTextContent))))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Long text preview generation should succeed.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(maxDisplayCharacters, preview.TextContent?.Length ?? 0, "Display text should stay bounded for Presentation consumption.");
        TestAssert.Equal(fullTextContent[..maxDisplayCharacters], preview.TextContent, "Display text should be the leading bounded slice.");
        TestAssert.Equal(fullTextContent, preview.FullTextContent, "Application consumers should receive the full structured text.");
        TestAssert.Equal(fullTextContent, previewCacheStore.GetSavedTextContent(), "Preview cache should persist full text payloads.");
        TestAssert.Equal(1, previewCacheStore.SaveCallCount, "Generated long text should be written to preview cache once.");
    }
}
