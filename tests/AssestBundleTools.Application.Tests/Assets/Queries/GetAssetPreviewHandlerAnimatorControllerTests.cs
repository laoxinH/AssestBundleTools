using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldHydrateCachedAnimatorControllerPreview_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.animator-controller.json";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var animatorControllerAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.Unknown.ToString(),
            AssetTypeDisplayName = "AnimatorController",
            SerializedFileName = "animations.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [animatorControllerAssetEntry]
        };
        var animatorController = CreateAnimatorControllerPreview();
        var previewCachePayload = CreateAnimatorControllerPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-1",
            animatorController);
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

        TestAssert.True(result.IsSuccess, "Matching AnimatorController preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.AnimatorController, preview.Kind, "Cached AnimatorController preview kind should round-trip.");
        TestAssert.Equal(null, preview.BinaryContent, "AnimatorController preview should not expose binary payload.");
        TestAssert.Equal(null, preview.TextContent, "AnimatorController preview JSON should be hydrated into structured object data.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached AnimatorController preview should expose resolved payload path.");
        var restoredAnimatorController = preview.AnimatorController ?? throw new InvalidOperationException("Expected AnimatorController preview.");
        TestAssert.Equal("HeroAnimator", restoredAnimatorController.Name, "AnimatorController name should round-trip.");
        TestAssert.Equal(1, restoredAnimatorController.LayerCount, "AnimatorController layer count should round-trip.");
        TestAssert.Equal(2, restoredAnimatorController.ParameterCount, "AnimatorController parameter count should round-trip.");
        TestAssert.Equal(3, restoredAnimatorController.StateCount, "AnimatorController state count should round-trip.");
        TestAssert.Equal(4, restoredAnimatorController.TransitionCount, "AnimatorController transition count should round-trip.");
        TestAssert.Equal(2, restoredAnimatorController.ReferenceCount, "AnimatorController reference count should round-trip.");
        var layer = TestAssert.Single(restoredAnimatorController.Layers);
        TestAssert.Equal("Base Layer", layer.Name, "Layer name should round-trip.");
        TestAssert.Equal(600L, layer.StateMachinePathId, "Layer state machine path id should round-trip.");
        var speedParameter = restoredAnimatorController.Parameters.Single(static parameter => parameter.Name == "Speed");
        TestAssert.Equal("Float", speedParameter.TypeName, "Parameter type name should round-trip.");
        TestAssert.Equal(1d, speedParameter.DefaultFloat, "Parameter default float should round-trip.");
        var animationClipReference = restoredAnimatorController.References.Single(static reference => reference.TypeName == "AnimationClip");
        TestAssert.Equal(700L, animationClipReference.PathId, "AnimationClip reference path id should round-trip.");
        TestAssert.Equal("HeroIdle", animationClipReference.TargetName, "AnimationClip reference target name should round-trip.");
        TestAssert.Equal(2, restoredAnimatorController.Containers?.Count ?? 0, "AnimatorController container summaries should round-trip.");
        TestAssert.True(
            restoredAnimatorController.Containers?.Any(static container =>
                container.Kind == AssetPreviewAnimatorControllerContainerKind.State &&
                container.ChildCount == 3) == true,
            "AnimatorController state container summary should round-trip.");
        TestAssert.Equal(3, preview.PreviewInfoItems?.Count ?? 0, "AnimatorController preview info items should be preserved.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cache hit should not invoke decryptor.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cache hit should not invoke preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Cache hit should not rewrite preview cache.");
    }
}
