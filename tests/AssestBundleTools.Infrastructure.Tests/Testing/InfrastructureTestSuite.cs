using AssestBundleTools.Infrastructure.Tests.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Tests.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;
using AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;
using AssestBundleTools.Infrastructure.Tests.Workspace.Runtime;

namespace AssestBundleTools.Infrastructure.Tests.Testing;

internal static class InfrastructureTestSuite
{
    public static async Task<int> RunAsync()
    {
        var failures = new List<string>();

        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceCatalogStoreTests.Load_ShouldReturnEmptySnapshot_WhenCatalogFileDoesNotExist),
            JsonWorkspaceCatalogStoreTests.Load_ShouldReturnEmptySnapshot_WhenCatalogFileDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceCatalogStoreTests.Save_ShouldRoundTripCatalogSnapshot),
            JsonWorkspaceCatalogStoreTests.Save_ShouldRoundTripCatalogSnapshot);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceMetadataStoreTests.Save_ShouldRoundTripWorkspaceMetadata),
            JsonWorkspaceMetadataStoreTests.Save_ShouldRoundTripWorkspaceMetadata);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceMetadataStoreTests.Delete_ShouldRemoveSavedMetadataFile),
            JsonWorkspaceMetadataStoreTests.Delete_ShouldRemoveSavedMetadataFile);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceChangeStoreTests.Save_ShouldRoundTripEditDraftDescriptors),
            JsonWorkspaceChangeStoreTests.Save_ShouldRoundTripEditDraftDescriptors);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Save_ShouldRoundTripDescriptorAndPayload),
            JsonPreviewCacheStoreTests.Save_ShouldRoundTripDescriptorAndPayload);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Delete_ShouldRemoveDescriptorPayload_AndPruneEmptyPayloadDirectory),
            JsonPreviewCacheStoreTests.Delete_ShouldRemoveDescriptorPayload_AndPruneEmptyPayloadDirectory);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Load_ShouldReturnNull_WhenDescriptorExistsButPayloadFileIsMissing),
            JsonPreviewCacheStoreTests.Load_ShouldReturnNull_WhenDescriptorExistsButPayloadFileIsMissing);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Load_ShouldReturnNull_WhenStoredDescriptorPayloadPathIsInvalid),
            JsonPreviewCacheStoreTests.Load_ShouldReturnNull_WhenStoredDescriptorPayloadPathIsInvalid);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.ResolvePayloadFilePath_ShouldReturnNull_WhenDescriptorFieldsAreInvalid),
            JsonPreviewCacheStoreTests.ResolvePayloadFilePath_ShouldReturnNull_WhenDescriptorFieldsAreInvalid);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Delete_ShouldRemoveDescriptor_WhenStoredDescriptorPayloadPathIsInvalid),
            JsonPreviewCacheStoreTests.Delete_ShouldRemoveDescriptor_WhenStoredDescriptorPayloadPathIsInvalid);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryTests.TryGetAssetDetails_ShouldResolveMeshTextureAssociations_FromRendererPath),
            CachedWorkspaceSessionRegistryTests.TryGetAssetDetails_ShouldResolveMeshTextureAssociations_FromRendererPath);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryTests.TryGetAssetDetails_ShouldFallbackToMeshDirectTexture_WhenRendererPathHasNoTexture),
            CachedWorkspaceSessionRegistryTests.TryGetAssetDetails_ShouldFallbackToMeshDirectTexture_WhenRendererPathHasNoTexture);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryTests.TryGetAssetDetails_ShouldResolveMeshTextureAssociations_FromExternalMaterialTexture),
            CachedWorkspaceSessionRegistryTests.TryGetAssetDetails_ShouldResolveMeshTextureAssociations_FromExternalMaterialTexture);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryTests.TryGetAssetList_ShouldExposeDynamicTypeFacets_AndFilterUnknownByDisplayName),
            CachedWorkspaceSessionRegistryTests.TryGetAssetList_ShouldExposeDynamicTypeFacets_AndFilterUnknownByDisplayName);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsRealSampleDependencyTests.Cone002Mesh_ShouldExposeRendererMaterialPath_ButNoTextureAssociation_FromRealDependencyGraph),
            AssetsToolsRealSampleDependencyTests.Cone002Mesh_ShouldExposeRendererMaterialPath_ButNoTextureAssociation_FromRealDependencyGraph);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsRealSampleDependencyTests.Cone002Mesh_ShouldResolveTextureAssociation_WhenReadableSampleWorkspaceContainsMatchingTextureBundles),
            AssetsToolsRealSampleDependencyTests.Cone002Mesh_ShouldResolveTextureAssociation_WhenReadableSampleWorkspaceContainsMatchingTextureBundles);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsRealSampleDependencyTests.GameObject_ShouldResolveComponentMaterialPath_FromRealDependencyGraph),
            AssetsToolsRealSampleDependencyTests.GameObject_ShouldResolveComponentMaterialPath_FromRealDependencyGraph);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsRealSampleDependencyTests.MonoBehaviourPreview_ShouldGenerateStructuredSummary_FromReadableRealSamples),
            AssetsToolsRealSampleDependencyTests.MonoBehaviourPreview_ShouldGenerateStructuredSummary_FromReadableRealSamples);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsRealSampleDependencyTests.AnimatorControllerPreview_ShouldGenerateStructuredSummary_FromReadableRealSamples),
            AssetsToolsRealSampleDependencyTests.AnimatorControllerPreview_ShouldGenerateStructuredSummary_FromReadableRealSamples);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsAssetPreviewGeneratorTests.GenerateAsync_ShouldReturnGenerationFailureDetail_WhenUnexpectedExceptionOccurs),
            AssetsToolsAssetPreviewGeneratorTests.GenerateAsync_ShouldReturnGenerationFailureDetail_WhenUnexpectedExceptionOccurs);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV42RuntimeDrawCommands_FromJsonSkeleton),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV42RuntimeDrawCommands_FromJsonSkeleton);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV38RuntimeDrawCommands_FromJsonSkeleton),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV38RuntimeDrawCommands_FromJsonSkeleton);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV38SkinPreviewVariants_FromJsonSkeleton),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV38SkinPreviewVariants_FromJsonSkeleton);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV38MeshDrawCommands_FromJsonSkeleton),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV38MeshDrawCommands_FromJsonSkeleton);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV42MeshDrawCommands_FromJsonSkeleton),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldBuildV42MeshDrawCommands_FromJsonSkeleton);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38DrawOrderAndSlotColorTimelines),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38DrawOrderAndSlotColorTimelines);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38UnweightedMeshDeformTimeline),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38UnweightedMeshDeformTimeline);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38WeightedMeshDeformTimeline),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38WeightedMeshDeformTimeline);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38TransformConstraintTimeline),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38TransformConstraintTimeline);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38IkConstraintTimeline),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV38IkConstraintTimeline);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV42MeshDeformTimeline),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV42MeshDeformTimeline);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV42IkConstraintTimeline),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldApplyV42IkConstraintTimeline);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldTryRegisteredAdapters_ForBinarySkeletonWithUnknownRuntime),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldTryRegisteredAdapters_ForBinarySkeletonWithUnknownRuntime);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldReturnNotHandled_WhenRuntimeMetadataIsMissing),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldReturnNotHandled_WhenRuntimeMetadataIsMissing);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldReturnNotHandled_WhenV42ParserReturnsNullSkeletonData),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldReturnNotHandled_WhenV42ParserReturnsNullSkeletonData);
        await RunTestAsync(
            failures,
            nameof(SpineRuntimeSamplingPolicyTests.CreateSampleTimes_ShouldKeepThirtyFpsBudget_ForLargeOfficialRuntimeMesh),
            SpineRuntimeSamplingPolicyTests.CreateSampleTimes_ShouldKeepThirtyFpsBudget_ForLargeOfficialRuntimeMesh);
        await RunTestAsync(
            failures,
            nameof(SpineRuntimeSamplingPolicyTests.CreateSampleTimes_ShouldStayBounded_ForExtremeOfficialRuntimeMesh),
            SpineRuntimeSamplingPolicyTests.CreateSampleTimes_ShouldStayBounded_ForExtremeOfficialRuntimeMesh);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsMeshTopologyBuilderTests.BuildTriangles_ShouldReadTriangleAndQuadTopology),
            AssetsToolsMeshTopologyBuilderTests.BuildTriangles_ShouldReadTriangleAndQuadTopology);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsMeshTopologyBuilderTests.BuildTriangles_ShouldSkipIndicesOutsidePreviewVertexRange),
            AssetsToolsMeshTopologyBuilderTests.BuildTriangles_ShouldSkipIndicesOutsidePreviewVertexRange);
        await RunTestAsync(
            failures,
            nameof(ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldUseBundleScopedWorkspaceStagingPath_WhenWorkspaceDataRootIsProvided),
            ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldUseBundleScopedWorkspaceStagingPath_WhenWorkspaceDataRootIsProvided);
        await RunTestAsync(
            failures,
            nameof(ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldAppendUniqueSegments_WhenCustomWorkingDirectoryIsProvided),
            ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldAppendUniqueSegments_WhenCustomWorkingDirectoryIsProvided);

        if (failures.Count == 0)
        {
            Console.WriteLine("All infrastructure tests passed.");
            return 0;
        }

        Console.Error.WriteLine("Infrastructure tests failed:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }

        return 1;
    }

    private static Task RunTestAsync(
        ICollection<string> failures,
        string testName,
        Action test)
    {
        try
        {
            test();
            Console.WriteLine($"[PASS] {testName}");
        }
        catch (Exception exception)
        {
            failures.Add($"{testName}: {exception.Message}");
        }

        return Task.CompletedTask;
    }

    private static async Task RunTestAsync(
        ICollection<string> failures,
        string testName,
        Func<Task> test)
    {
        try
        {
            await test();
            Console.WriteLine($"[PASS] {testName}");
        }
        catch (Exception exception)
        {
            failures.Add($"{testName}: {exception.Message}");
        }
    }
}
