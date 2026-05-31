using AssestBundleTools.Application.Tests.Assets.Queries;

namespace AssestBundleTools.Application.Tests.Testing;

internal static partial class ApplicationTestSuite
{
    private static async Task RunSpineAssetGroupTestsAsync(List<string> failures)
    {
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldGroupSkeletonAtlasAndWorkspaceTexturePages),
            LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldGroupSkeletonAtlasAndWorkspaceTexturePages);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldReportMissingAtlasAndTextureDiagnostics),
            LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldReportMissingAtlasAndTextureDiagnostics);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldExposePartExtractionState),
            LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldExposePartExtractionState);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldMatchExtensionlessJsonSkeleton_WithSameNameAtlas),
            LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldMatchExtensionlessJsonSkeleton_WithSameNameAtlas);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldClassifyExtensionlessDuplicateSpineParts_UsingDependencyLinks),
            LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldClassifyExtensionlessDuplicateSpineParts_UsingDependencyLinks);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldSplitDuplicateSpineResourcesUsingDependencyLinks),
            LoadSpineAssetGroupsHandlerTests.HandleAsync_ShouldSplitDuplicateSpineResourcesUsingDependencyLinks);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldParseAtlasPagesAndRegions_FromPreviewPipeline),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldParseAtlasPagesAndRegions_FromPreviewPipeline);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldUsePreviewSourceOverride_ForDraftAtlasText),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldUsePreviewSourceOverride_ForDraftAtlasText);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldParseAtlasFromFullTextContent_WhenDisplayTextIsBounded),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldParseAtlasFromFullTextContent_WhenDisplayTextIsBounded);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldParseJsonSkeletonSummary_FromPreviewPipeline),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldParseJsonSkeletonSummary_FromPreviewPipeline);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldBuildJsonSpineAnimationPreview_FromPreviewPipeline),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldBuildJsonSpineAnimationPreview_FromPreviewPipeline);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldApplyJsonDrawOrderAndSlotColorTimelines),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldApplyJsonDrawOrderAndSlotColorTimelines);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldPreferRuntimeAdapterPreview_WhenAdapterHandlesSkeleton),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldPreferRuntimeAdapterPreview_WhenAdapterHandlesSkeleton);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldPassBinarySkeletonPreview_ToRuntimeAdapter),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldPassBinarySkeletonPreview_ToRuntimeAdapter);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldTreatReadableSkelPayload_AsJsonSkeletonPreview),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldTreatReadableSkelPayload_AsJsonSkeletonPreview);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportUnsupportedRuntimeVersion_WhenNoAdapterMatches),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportUnsupportedRuntimeVersion_WhenNoAdapterMatches);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldResolveSpineRuntimeMatrix_ByMajorMinorVersion),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldResolveSpineRuntimeMatrix_ByMajorMinorVersion);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportJsonSpineAnimationUnsupportedFeatures),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportJsonSpineAnimationUnsupportedFeatures);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportAtlasPreviewUnavailable_WhenAtlasPreviewIsNotText),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportAtlasPreviewUnavailable_WhenAtlasPreviewIsNotText);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportAtlasPageTextureUnmatched_WhenPageHasNoTextureAsset),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportAtlasPageTextureUnmatched_WhenPageHasNoTextureAsset);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportTexturePreviewUnavailable_WhenMatchedTextureHasNoImagePreview),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportTexturePreviewUnavailable_WhenMatchedTextureHasNoImagePreview);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportAtlasPageTextureSizeMismatch_WhenImageDimensionsDiffer),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportAtlasPageTextureSizeMismatch_WhenImageDimensionsDiffer);
    }
}
