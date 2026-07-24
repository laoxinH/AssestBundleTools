using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldUseOriginalPreview_WhenEffectiveFileReplacementExists()
    {
        const string replacementFilePath = @"C:\Imports\replacement.json";
        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(
            query.WorkspaceKey,
            query.BundleId.Value,
            query.AssetId.Value);
        var originalCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            "original binary skeleton",
            generatorVersion: "generator-1");
        var previewCacheStore = new TestPreviewCacheStore(originalCachePayload);
        var replacementPreviewSourceProvider = new TestReplacementPreviewSourceProvider(
            Result<AssetPreviewContentDto>.Success(new AssetPreviewContentDto(
                AssetPreviewKind.Text,
                "application/json",
                Encoding.UTF8.GetBytes("{\"bones\":[]}"))));
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    fileReplacementSourceFileName: "replacement.json",
                    fileReplacementSourcePath: replacementFilePath)
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(
                new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor())),
            new TestAssetPreviewGenerator(),
            new TestAudioPayloadTranscodingService(),
            replacementPreviewSourceProvider: replacementPreviewSourceProvider);

        var result = await handler.HandleAsync(query with
        {
            ContentScope = AssetPreviewContentScope.Original
        });

        TestAssert.True(result.IsSuccess, "Original preview lookup should succeed.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected original preview result.");
        TestAssert.Equal("original binary skeleton", preview.TextContent, "Original scope should use the immutable AB preview cache.");
        TestAssert.Equal(0, replacementPreviewSourceProvider.LoadFilePreviewCallCount, "Original scope must not read the effective replacement source.");
    }
}
