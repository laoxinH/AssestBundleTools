using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldHydrateCachedFontPreview_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.font.json";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var fontAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.Unknown.ToString(),
            AssetTypeDisplayName = "Font",
            SerializedFileName = "fonts.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [fontAssetEntry]
        };
        var font = CreateFontPreview();
        var previewCachePayload = CreateFontPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-1",
            font);
        var previewCacheStore = new TestPreviewCacheStore(previewCachePayload, resolvedPayloadFilePath);
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Matching font preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Font, preview.Kind, "Cached font preview kind should round-trip.");
        TestAssert.Equal(null, preview.BinaryContent, "Font preview should not expose binary payload.");
        TestAssert.Equal(null, preview.TextContent, "Font preview JSON should be hydrated into structured font data.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached font preview should expose resolved payload path.");
        var restoredFont = preview.Font ?? throw new InvalidOperationException("Expected font preview.");
        TestAssert.Equal("LiberationSans SDF", restoredFont.FontName, "Font name should round-trip.");
        TestAssert.Equal(72, restoredFont.CharacterCount, "Font character count should round-trip.");
        TestAssert.Equal(16, restoredFont.DefaultSize, "Font default size should round-trip.");
        TestAssert.Equal(21.5d, restoredFont.LineSpacing, "Font line spacing should round-trip.");
        TestAssert.Equal(4096L, restoredFont.FontDataSizeInBytes, "Font data size should round-trip.");
        TestAssert.True(restoredFont.IsCharacterListTruncated, "Font truncation marker should round-trip.");
        TestAssert.Equal("LiberationSans Atlas", restoredFont.Texture?.Name, "Font texture reference name should round-trip.");
        TestAssert.True(restoredFont.Texture?.IsExternal == true, "Font external texture reference should round-trip.");
        TestAssert.Equal("LiberationSans Material", restoredFont.DefaultMaterial?.Name, "Font default material reference should round-trip.");
        TestAssert.Equal(2, restoredFont.Characters.Count, "Font characters should be hydrated from cached JSON.");
        TestAssert.Equal(65L, restoredFont.Characters[0].CodePoint, "Font character code point should round-trip.");
        TestAssert.Equal("A", restoredFont.Characters[0].DisplayText, "Font character glyph should round-trip.");
        TestAssert.Equal(8.5d, restoredFont.Characters[0].Advance, "Font character advance should round-trip.");
        TestAssert.True(restoredFont.Characters[1].IsFlipped == true, "Font character flipped flag should round-trip.");
        TestAssert.Equal(3, preview.PreviewInfoItems?.Count ?? 0, "Font preview info items should be preserved.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cache hit should not invoke decryptor.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cache hit should not invoke preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Cache hit should not rewrite preview cache.");
    }
}
