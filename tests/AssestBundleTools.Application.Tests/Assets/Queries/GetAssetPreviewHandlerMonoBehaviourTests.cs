using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldHydrateCachedMonoBehaviourPreview_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.mono-behaviour.json";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var monoBehaviourAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.Unknown.ToString(),
            AssetTypeDisplayName = "MonoBehaviour",
            SerializedFileName = "scene.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [monoBehaviourAssetEntry]
        };
        var monoBehaviour = CreateMonoBehaviourPreview();
        var previewCachePayload = CreateMonoBehaviourPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-1",
            monoBehaviour);
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

        TestAssert.True(result.IsSuccess, "Matching MonoBehaviour preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.MonoBehaviour, preview.Kind, "Cached MonoBehaviour preview kind should round-trip.");
        TestAssert.Equal(null, preview.BinaryContent, "MonoBehaviour preview should not expose binary payload.");
        TestAssert.Equal(null, preview.TextContent, "MonoBehaviour preview JSON should be hydrated into structured object data.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached MonoBehaviour preview should expose resolved payload path.");
        var restoredMonoBehaviour = preview.MonoBehaviour ?? throw new InvalidOperationException("Expected MonoBehaviour preview.");
        TestAssert.Equal("HeroLogic", restoredMonoBehaviour.Name, "MonoBehaviour name should round-trip.");
        TestAssert.True(restoredMonoBehaviour.IsEnabled == true, "MonoBehaviour enabled flag should round-trip.");
        TestAssert.Equal(8, restoredMonoBehaviour.FieldCount, "MonoBehaviour visited field count should round-trip.");
        TestAssert.Equal(2, restoredMonoBehaviour.ScalarFieldCount, "MonoBehaviour scalar field count should round-trip.");
        TestAssert.Equal(1, restoredMonoBehaviour.ArrayFieldCount, "MonoBehaviour array field count should round-trip.");
        TestAssert.Equal(2, restoredMonoBehaviour.ReferenceCount, "MonoBehaviour reference count should round-trip.");
        var script = restoredMonoBehaviour.Script ?? throw new InvalidOperationException("Expected script reference.");
        TestAssert.Equal(400L, script.PathId, "Script path id should round-trip.");
        TestAssert.Equal("HeroController", script.ClassName, "Script class name should round-trip.");
        TestAssert.Equal("Assembly-CSharp.dll", script.AssemblyName, "Script assembly should round-trip.");
        var healthField = restoredMonoBehaviour.Fields.Single(static field => field.Path == "m_Health");
        TestAssert.Equal(100L, healthField.Int64Value, "Integer field should round-trip.");
        var waypointField = restoredMonoBehaviour.Fields.Single(static field => field.Path == "m_Waypoints");
        TestAssert.Equal(2, waypointField.ChildCount, "Array child count should round-trip.");
        TestAssert.Equal(2, restoredMonoBehaviour.FieldGroups?.Count ?? 0, "Field groups should round-trip.");
        TestAssert.True(
            restoredMonoBehaviour.FieldGroups?.Any(static group =>
                group.Name == "m_Waypoints" &&
                group.ArrayFieldCount == 1) == true,
            "Field group array summary should round-trip.");
        TestAssert.Equal(1, restoredMonoBehaviour.Containers?.Count ?? 0, "Container summaries should round-trip.");
        TestAssert.Equal(2, restoredMonoBehaviour.Containers?[0].ChildCount, "Container child count should round-trip.");
        var materialReference = restoredMonoBehaviour.References.Single(static reference => reference.Path == "m_Material");
        TestAssert.Equal(200L, materialReference.PathId, "Reference path id should round-trip.");
        TestAssert.Equal("Material", materialReference.TypeName, "Reference type should round-trip.");
        TestAssert.Equal("HeroMaterial", materialReference.TargetName, "Reference target name should round-trip.");
        TestAssert.Equal(2, preview.PreviewInfoItems?.Count ?? 0, "MonoBehaviour preview info items should be preserved.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cache hit should not invoke decryptor.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cache hit should not invoke preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Cache hit should not rewrite preview cache.");
    }
}
