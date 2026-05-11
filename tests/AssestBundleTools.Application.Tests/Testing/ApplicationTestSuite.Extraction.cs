using AssestBundleTools.Application.Tests.Extraction;

namespace AssestBundleTools.Application.Tests.Testing;

internal static partial class ApplicationTestSuite
{
    private static async Task RunExtractionWorkflowTestsAsync(List<string> failures)
    {
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteFullTextPayload_AndRecordOperation),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteFullTextPayload_AndRecordOperation);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldPreserveReadableSkelTextPayload),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldPreserveReadableSkelTextPayload);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteImagePayload_WithPngOutput),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteImagePayload_WithPngOutput);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldPreserveUserSelectedOutputFileName),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldPreserveUserSelectedOutputFileName);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldExtractBatch_AndAppendBatchOperation),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldExtractBatch_AndAppendBatchOperation);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldExtractSkeletonAtlasTextureAndAlphaPages),
            ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldExtractSkeletonAtlasTextureAndAlphaPages);
    }
}
