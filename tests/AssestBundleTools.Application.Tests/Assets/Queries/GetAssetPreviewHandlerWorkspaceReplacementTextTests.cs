using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldReuseSourceTextReference_ForWorkspaceReplacementTextAsset()
    {
        const string resolvedSourcePayloadFilePath = @"D:\WorkspaceData\preview-cache\source-atlas.txt";
        const string sourceText = "line-0\nsource target line\nline-2";
        var sourceAssetId = new AssetId("asset.source-text");

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var targetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.TextAsset.ToString(),
            AssetTypeDisplayName = AssetType.TextAsset.ToString(),
            AssetPath = "Assets/Hero/Target.atlas",
            AssetName = "Target.atlas"
        };
        var sourceEntry = targetEntry with
        {
            AssetId = sourceAssetId.Value,
            AssetPath = "Assets/Hero/Source.atlas",
            AssetName = "Source.atlas",
            PathId = 54321
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [targetEntry, sourceEntry]
        };
        var sourcePreviewPayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            sourceAssetId.Value,
            sourceText,
            generatorVersion: "generator-1");
        var previewCacheStore = new TestPreviewCacheStore(
            sourcePreviewPayload,
            resolvedSourcePayloadFilePath);
        var dataStoreFactory = new TestWorkspaceDataStoreFactory(
            new TestBundleIndexCacheStore([bundleCacheEntry]),
            previewCacheStore);
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    name: "Target.atlas",
                    assetType: AssetType.TextAsset,
                    workspaceReplacementSourceBundleId: query.BundleId,
                    workspaceReplacementSourceAssetId: sourceAssetId,
                    workspaceReplacementSourceBundleDisplayName: "characters",
                    workspaceReplacementSourceAssetName: "Source.atlas")
            ]),
            dataStoreFactory,
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Workspace TextAsset replacement should resolve the source text preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        var reference = preview.FullContentReference ?? throw new InvalidOperationException("Expected full content reference.");
        TestAssert.Equal(query.BundleId, preview.BundleId, "Workspace replacement preview should keep the target bundle identity.");
        TestAssert.Equal(query.AssetId, preview.AssetId, "Workspace replacement preview should keep the target asset identity.");
        TestAssert.Equal(AssetPreviewKind.Text, preview.Kind, "Workspace replacement text preview should expose text content.");
        TestAssert.Equal(sourceText, preview.FullTextContent, "Workspace replacement text should expose the source full text.");
        TestAssert.Equal(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            reference.Kind,
            "Workspace replacement text should reuse the source preview-cache reference.");
        TestAssert.Equal(query.WorkspaceKey, reference.WorkspaceKey, "Reference should remain scoped to the current workspace key.");
        TestAssert.Equal(
            sourcePreviewPayload.Descriptor.PreviewCacheKey,
            reference.PreviewCacheKey,
            "Reference should point to the source asset preview payload.");
        TestAssert.Equal(
            Encoding.UTF8.GetByteCount(sourceText),
            reference.ContentLength,
            "Reference should expose the source text payload length.");
        TestAssert.Equal(
            resolvedSourcePayloadFilePath,
            preview.PreviewPayloadFilePath,
            "Workspace replacement preview should keep the source payload file path as an opaque diagnostic reference.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cached source replacement text should not decrypt the original bundle.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cached source replacement text should not regenerate source content.");

        await AssertWorkspaceReplacementReferenceCanReadChunkLineAndSearchAsync(
            dataStoreFactory,
            query.WorkspaceDataRootPath,
            reference);
    }

    private static async Task AssertWorkspaceReplacementReferenceCanReadChunkLineAndSearchAsync(
        TestWorkspaceDataStoreFactory dataStoreFactory,
        string workspaceDataRootPath,
        AssetPreviewFullContentReferenceDto reference)
    {
        var chunkResult = await new LoadPreviewTextChunkHandler(dataStoreFactory).HandleAsync(
            new LoadPreviewTextChunkQuery(
                workspaceDataRootPath,
                reference,
                Offset: 7,
                Length: 6));
        TestAssert.True(chunkResult.IsSuccess, "Workspace replacement reference should support chunk loading.");
        TestAssert.Equal("source", chunkResult.Value?.Text, "Chunk loading should read from the source text payload.");

        var lineResult = await new LoadPreviewTextLinesHandler(dataStoreFactory).HandleAsync(
            new LoadPreviewTextLinesQuery(
                workspaceDataRootPath,
                reference,
                StartLineIndex: 1,
                LineCount: 1));
        TestAssert.True(lineResult.IsSuccess, "Workspace replacement reference should support line loading.");
        TestAssert.Equal("source target line", lineResult.Value?.Lines.SingleOrDefault(), "Line loading should read source text lines.");

        var searchResult = await new SearchPreviewTextHandler(dataStoreFactory).HandleAsync(
            new SearchPreviewTextQuery(
                workspaceDataRootPath,
                reference,
                SearchText: "target",
                StartLineIndex: 0,
                MaxMatchCount: 4,
                MatchCase: false));
        TestAssert.True(searchResult.IsSuccess, "Workspace replacement reference should support text search.");
        TestAssert.Equal(1, searchResult.Value?.Matches.Count, "Search should find the source text match.");
        TestAssert.Equal(1, searchResult.Value?.Matches[0].LineIndex, "Search should preserve the source line index.");
    }
}
