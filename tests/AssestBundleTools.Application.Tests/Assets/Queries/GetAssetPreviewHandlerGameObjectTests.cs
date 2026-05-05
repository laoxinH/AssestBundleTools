using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldHydrateCachedGameObjectPreview_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.game-object.json";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var gameObjectAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.Unknown.ToString(),
            AssetTypeDisplayName = "GameObject",
            SerializedFileName = "scene.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [gameObjectAssetEntry]
        };
        var gameObject = CreateGameObjectPreview();
        var previewCachePayload = CreateGameObjectPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-1",
            gameObject);
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

        TestAssert.True(result.IsSuccess, "Matching GameObject preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.GameObject, preview.Kind, "Cached GameObject preview kind should round-trip.");
        TestAssert.Equal(null, preview.BinaryContent, "GameObject preview should not expose binary payload.");
        TestAssert.Equal(null, preview.TextContent, "GameObject preview JSON should be hydrated into structured object data.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached GameObject preview should expose resolved payload path.");
        var restoredGameObject = preview.GameObject ?? throw new InvalidOperationException("Expected GameObject preview.");
        TestAssert.Equal("HeroRoot", restoredGameObject.Name, "GameObject name should round-trip.");
        TestAssert.Equal(5, restoredGameObject.Layer, "GameObject layer should round-trip.");
        TestAssert.Equal("Player", restoredGameObject.Tag, "GameObject tag should round-trip.");
        TestAssert.True(restoredGameObject.IsActive == true, "GameObject active flag should round-trip.");
        TestAssert.Equal(3, restoredGameObject.ComponentCount, "GameObject component count should round-trip.");
        TestAssert.Equal(3, restoredGameObject.Components.Count, "GameObject component rows should be hydrated from cached JSON.");
        TestAssert.Equal("Transform", restoredGameObject.Components[0].TypeName, "Transform component type should round-trip.");
        TestAssert.Equal("HeroController", restoredGameObject.Components[2].ScriptName, "MonoBehaviour script name should round-trip.");
        TestAssert.Equal(400L, restoredGameObject.Components[2].ScriptPathId, "MonoBehaviour script path id should round-trip.");
        var materialReference = TestAssert.Single(restoredGameObject.Components[1].References ?? Array.Empty<AssetPreviewGameObjectReferenceDto>());
        TestAssert.Equal("material", materialReference.Role, "Renderer material reference role should round-trip.");
        TestAssert.Equal(200L, materialReference.PathId, "Renderer material reference path id should round-trip.");
        TestAssert.Equal("HeroMaterial", materialReference.Name, "Renderer material reference name should round-trip.");
        var rendererSummaries = restoredGameObject.Components[1].Summaries ?? Array.Empty<AssetPreviewGameObjectComponentSummaryDto>();
        TestAssert.True(
            rendererSummaries.Any(static summary =>
                summary.Key == AssetPreviewGameObjectComponentSummaryKey.MaterialCount &&
                summary.Int64Value == 1),
            "Renderer material count summary should round-trip.");
        TestAssert.True(
            rendererSummaries.Any(static summary =>
                summary.Key == AssetPreviewGameObjectComponentSummaryKey.ReceiveShadows &&
                summary.BooleanValue == true),
            "Renderer receive shadows summary should round-trip.");
        var scriptReference = TestAssert.Single(restoredGameObject.Components[2].References ?? Array.Empty<AssetPreviewGameObjectReferenceDto>());
        TestAssert.Equal("script", scriptReference.Role, "MonoBehaviour script reference role should round-trip.");
        TestAssert.Equal(400L, scriptReference.PathId, "MonoBehaviour script reference path id should round-trip.");
        var scriptSummary = (restoredGameObject.Components[2].Summaries ?? Array.Empty<AssetPreviewGameObjectComponentSummaryDto>())
            .Single(static summary => summary.Key == AssetPreviewGameObjectComponentSummaryKey.Script);
        TestAssert.Equal("HeroController", scriptSummary.StringValue, "MonoBehaviour script summary should round-trip.");
        var transform = restoredGameObject.Transform ?? throw new InvalidOperationException("Expected GameObject transform summary.");
        TestAssert.Equal(100L, transform.PathId, "Transform path id should round-trip.");
        TestAssert.Equal(2, transform.ChildCount, "Transform child count should round-trip.");
        TestAssert.Equal(1d, transform.LocalPositionX, "Transform local position x should round-trip.");
        TestAssert.Equal(3d, transform.LocalPositionZ, "Transform local position z should round-trip.");
        TestAssert.Equal(1d, transform.LocalScaleX, "Transform local scale should round-trip.");
        TestAssert.Equal(3, preview.PreviewInfoItems?.Count ?? 0, "GameObject preview info items should be preserved.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cache hit should not invoke decryptor.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cache hit should not invoke preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Cache hit should not rewrite preview cache.");
    }
}
