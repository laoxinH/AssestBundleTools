using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldCacheFullText_AndExposeBoundedDisplayText_WhenGeneratedTextIsLong()
    {
        const string resolvedBundlePath = @"C:\Temp\long-text.bundle";
        const int maxDisplayCharacters = AssetPreviewTextDisplayPolicy.MaxDisplayCharacters;

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
        TestAssert.True(preview.TextDisplay?.IsTruncated == true, "Text display metadata should mark generated long text as truncated.");
        TestAssert.Equal(fullTextContent.Length, preview.TextDisplay?.FullCharacterCount, "Text display metadata should preserve the full character count.");
        TestAssert.Equal(maxDisplayCharacters, preview.TextDisplay?.MaxDisplayCharacters, "Text display metadata should expose the active character budget.");
        TestAssert.Equal(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            preview.FullContentReference?.Kind,
            "Generated text preview should expose a preview-cache full content reference.");
        TestAssert.Equal(
            previewCacheStore.LastSavedPayload?.Descriptor.PreviewCacheKey,
            preview.FullContentReference?.PreviewCacheKey,
            "Full content reference should identify the saved preview cache payload.");
        TestAssert.Equal(
            Encoding.UTF8.GetByteCount(fullTextContent),
            preview.FullContentReference?.ContentLength,
            "Full content reference should expose raw payload length.");
        TestAssert.Equal(
            maxDisplayCharacters,
            ReadInt64PreviewInfo(preview, AssetPreviewInfoKey.TextDisplayCharacterCount),
            "Preview info should expose the bounded text display character count.");
        TestAssert.Equal(
            fullTextContent.Length,
            ReadInt64PreviewInfo(preview, AssetPreviewInfoKey.TextFullCharacterCount),
            "Preview info should expose the full text character count.");
        TestAssert.True(
            ReadBooleanPreviewInfo(preview, AssetPreviewInfoKey.TextDisplayTruncated) == true,
            "Preview info should expose text display truncation.");
        TestAssert.Equal(fullTextContent, preview.FullTextContent, "Application consumers should receive the full structured text.");
        TestAssert.Equal(fullTextContent, previewCacheStore.GetSavedTextContent(), "Preview cache should persist full text payloads.");
        TestAssert.Equal(1, previewCacheStore.SaveCallCount, "Generated long text should be written to preview cache once.");
    }

    public static async Task HandleAsync_ShouldExposeBoundedDisplayText_WhenCachedTextIsLong()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.txt";
        const int maxDisplayCharacters = AssetPreviewTextDisplayPolicy.MaxDisplayCharacters;

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var fullTextContent = new string('c', maxDisplayCharacters + 64);
        var previewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            fullTextContent,
            generatorVersion: "generator-1");
        var previewCacheStore = new TestPreviewCacheStore(previewCachePayload, resolvedPayloadFilePath);
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor())),
            new TestAssetPreviewGenerator(),
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Cached long text preview should hydrate successfully.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.True(preview.IsFromCache, "Preview should be served from cache.");
        TestAssert.Equal(maxDisplayCharacters, preview.TextContent?.Length ?? 0, "Cached display text should stay bounded for Presentation.");
        TestAssert.Equal(fullTextContent[..maxDisplayCharacters], preview.TextContent, "Cached display text should use the same leading bounded slice.");
        TestAssert.Equal(fullTextContent, preview.FullTextContent, "Application consumers should still receive full cached text.");
        TestAssert.True(preview.TextDisplay?.IsTruncated == true, "Cached long text metadata should mark the display as truncated.");
        TestAssert.Equal(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            preview.FullContentReference?.Kind,
            "Cached text preview should expose a preview-cache full content reference.");
        TestAssert.Equal(
            previewCachePayload.Descriptor.PreviewCacheKey,
            preview.FullContentReference?.PreviewCacheKey,
            "Cached full content reference should identify the cached payload.");
        TestAssert.Equal(
            maxDisplayCharacters,
            ReadInt64PreviewInfo(preview, AssetPreviewInfoKey.TextDisplayCharacterCount),
            "Cached preview info should expose the bounded text display character count.");
        TestAssert.Equal(
            fullTextContent.Length,
            ReadInt64PreviewInfo(preview, AssetPreviewInfoKey.TextFullCharacterCount),
            "Cached preview info should expose the full text character count.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached preview should keep the resolved payload reference path.");
    }

    public static async Task HandleAsync_ShouldLimitGeneratedTextDisplay_ByLineBudget()
    {
        const string resolvedBundlePath = @"C:\Temp\many-lines.bundle";
        const int maxDisplayLineCount = AssetPreviewTextDisplayPolicy.MaxDisplayLineCount;

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var lines = Enumerable.Range(0, maxDisplayLineCount + 5)
            .Select(static index => $"l{index:D4}")
            .ToArray();
        var fullTextContent = string.Join('\n', lines);
        var expectedDisplayText = string.Join('\n', lines.Take(maxDisplayLineCount));
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
                new TestPreviewCacheStore()),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Many-line text preview generation should succeed.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(expectedDisplayText, preview.TextContent, "Display text should stop at the configured line budget.");
        TestAssert.Equal(maxDisplayLineCount, preview.TextDisplay?.DisplayLineCount, "Text display metadata should report the displayed line count.");
        TestAssert.Equal(lines.Length, preview.TextDisplay?.EstimatedFullLineCount, "Text display metadata should report the estimated full line count.");
        TestAssert.True(preview.TextDisplay?.IsTruncated == true, "Line-budget truncation should be reported.");
        TestAssert.Equal(
            maxDisplayLineCount,
            ReadInt64PreviewInfo(preview, AssetPreviewInfoKey.TextDisplayLineCount),
            "Preview info should expose the displayed line count.");
        TestAssert.Equal(
            lines.Length,
            ReadInt64PreviewInfo(preview, AssetPreviewInfoKey.TextEstimatedFullLineCount),
            "Preview info should expose the estimated full line count.");
        TestAssert.Equal(fullTextContent, preview.FullTextContent, "Full text should remain available for Application consumers.");
    }

    public static async Task HandleAsync_ShouldExposeBoundedDisplayText_ForLongFileReplacementTextAsset()
    {
        const string replacementFilePath = @"C:\Imports\large-atlas.json";
        const int maxDisplayCharacters = AssetPreviewTextDisplayPolicy.MaxDisplayCharacters;

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets =
            [
                bundleCacheEntry.Assets[0] with
                {
                    AssetType = AssetType.TextAsset.ToString(),
                    AssetTypeDisplayName = AssetType.TextAsset.ToString(),
                    AssetPath = "Assets/Hero/Hero.atlas",
                    AssetName = "Hero.atlas"
                }
            ]
        };
        var longReplacementText = "{" + new string('j', maxDisplayCharacters + 128) + "}";
        var replacementPreviewSourceProvider = new TestReplacementPreviewSourceProvider(
            Result<AssetPreviewContentDto>.Success(new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Text,
                ContentType: "text/plain; charset=utf-8",
                Content: Encoding.UTF8.GetBytes(longReplacementText))));
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    assetType: AssetType.TextAsset,
                    fileReplacementSourceFileName: "large-atlas.json",
                    fileReplacementSourcePath: replacementFilePath)
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                new TestPreviewCacheStore()),
            new TestAssetBundleDecryptorRegistry(new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor())),
            new TestAssetPreviewGenerator(),
            new TestAudioPayloadTranscodingService(),
            replacementPreviewSourceProvider: replacementPreviewSourceProvider);

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Long TextAsset file replacements should still preview successfully.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(maxDisplayCharacters, preview.TextContent?.Length ?? 0, "Replacement display text should stay bounded for Presentation.");
        TestAssert.Equal(longReplacementText[..maxDisplayCharacters], preview.TextContent, "Replacement display text should use the leading bounded slice.");
        TestAssert.True(preview.TextDisplay?.IsTruncated == true, "Replacement text metadata should mark the display as truncated.");
        TestAssert.Equal(longReplacementText.Length, preview.TextDisplay?.FullCharacterCount, "Replacement text metadata should preserve the full character count.");
        TestAssert.Equal(
            maxDisplayCharacters,
            ReadInt64PreviewInfo(preview, AssetPreviewInfoKey.TextDisplayCharacterCount),
            "Replacement preview info should expose the bounded text display character count.");
        TestAssert.Equal(
            longReplacementText.Length,
            ReadInt64PreviewInfo(preview, AssetPreviewInfoKey.TextFullCharacterCount),
            "Replacement preview info should expose the full text character count.");
        TestAssert.Equal(longReplacementText, preview.FullTextContent, "Structured consumers should still receive the full replacement text.");
    }

    public static async Task LoadPreviewTextChunkHandler_ShouldReadChunk_FromPreviewCacheReference()
    {
        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        const string fullTextContent = "0123456789abcdef";
        var previewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            fullTextContent,
            generatorVersion: "generator-1");
        var previewCacheStore = new TestPreviewCacheStore(previewCachePayload);
        var handler = new LoadPreviewTextChunkHandler(new TestWorkspaceDataStoreFactory(
            new TestBundleIndexCacheStore([bundleCacheEntry]),
            previewCacheStore));
        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            "text/plain; charset=utf-8",
            ContentLength: 0,
            query.WorkspaceKey,
            previewCachePayload.Descriptor.PreviewCacheKey);

        var result = await handler.HandleAsync(new LoadPreviewTextChunkQuery(
            query.WorkspaceDataRootPath,
            reference,
            Offset: 4,
            Length: 6));

        TestAssert.True(result.IsSuccess, "Text chunk loading should succeed for a cached text preview reference.");
        var chunk = result.Value ?? throw new InvalidOperationException("Expected text chunk result.");
        TestAssert.Equal("456789", chunk.Text, "Chunk text should match the requested range.");
        TestAssert.Equal(4, chunk.Offset, "Chunk should report the effective offset.");
        TestAssert.Equal(6, chunk.Length, "Chunk should report the returned character count.");
        TestAssert.Equal(fullTextContent.Length, chunk.TotalLength, "Chunk should report the full normalized text length.");
        TestAssert.True(chunk.HasMore, "Chunk should report that more text remains.");
        TestAssert.Equal(
            Encoding.UTF8.GetByteCount(fullTextContent),
            chunk.Reference.ContentLength,
            "Chunk reference should refresh the raw payload length.");
    }

    public static async Task LoadPreviewTextChunkHandler_ShouldClampRequestedLength_ToChunkBudget()
    {
        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var fullTextContent = new string('x', LoadPreviewTextChunkHandler.MaxChunkLength + 128);
        var previewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            fullTextContent,
            generatorVersion: "generator-1");
        var previewCacheStore = new TestPreviewCacheStore(previewCachePayload);
        var handler = new LoadPreviewTextChunkHandler(new TestWorkspaceDataStoreFactory(
            new TestBundleIndexCacheStore([bundleCacheEntry]),
            previewCacheStore));
        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            "text/plain; charset=utf-8",
            ContentLength: 0,
            query.WorkspaceKey,
            previewCachePayload.Descriptor.PreviewCacheKey);

        var result = await handler.HandleAsync(new LoadPreviewTextChunkQuery(
            query.WorkspaceDataRootPath,
            reference,
            Offset: 0,
            Length: LoadPreviewTextChunkHandler.MaxChunkLength + 128));

        TestAssert.True(result.IsSuccess, "Oversized text chunk requests should be clamped rather than failing.");
        var chunk = result.Value ?? throw new InvalidOperationException("Expected text chunk result.");
        TestAssert.Equal(LoadPreviewTextChunkHandler.MaxChunkLength, chunk.Length, "Chunk length should be clamped to the handler budget.");
        TestAssert.Equal(LoadPreviewTextChunkHandler.MaxChunkLength, chunk.Text.Length, "Returned text should match the chunk budget.");
        TestAssert.True(chunk.HasMore, "Clamped chunk should report remaining text.");
    }

    private static long? ReadInt64PreviewInfo(AssetPreviewDto preview, AssetPreviewInfoKey key)
    {
        return preview.PreviewInfoItems?
            .FirstOrDefault(item => item.Key == key)?
            .Int64Value;
    }

    private static bool? ReadBooleanPreviewInfo(AssetPreviewDto preview, AssetPreviewInfoKey key)
    {
        return preview.PreviewInfoItems?
            .FirstOrDefault(item => item.Key == key)?
            .BooleanValue;
    }
}
