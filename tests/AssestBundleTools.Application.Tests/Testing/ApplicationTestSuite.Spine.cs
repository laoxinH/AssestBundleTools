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
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldParseAtlasPagesAndRegions_FromPreviewPipeline),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldParseAtlasPagesAndRegions_FromPreviewPipeline);
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
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportUnsupportedRuntimeVersion_WhenNoAdapterMatches),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldReportUnsupportedRuntimeVersion_WhenNoAdapterMatches);
        await RunTestAsync(
            failures,
            nameof(LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldResolveSpineViewerRuntimeMatrix_ByMajorMinorVersion),
            LoadSpineAssetGroupDetailsHandlerTests.HandleAsync_ShouldResolveSpineViewerRuntimeMatrix_ByMajorMinorVersion);
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
