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
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMeshPayload_AsObjOutput),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMeshPayload_AsObjOutput);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMeshPayload_AsFbx_WhenSelectedOutputPathUsesFbx),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMeshPayload_AsFbx_WhenSelectedOutputPathUsesFbx);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteStructuredSummaryPayload_AsJsonOutput),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteStructuredSummaryPayload_AsJsonOutput);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsTypeTreeJson_ByDefault),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsTypeTreeJson_ByDefault);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsYaml_WhenSelectedOutputPathUsesYaml),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsYaml_WhenSelectedOutputPathUsesYaml);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsFieldText_WhenSelectedOutputPathUsesTxt),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldWriteMonoBehaviourPayload_AsFieldText_WhenSelectedOutputPathUsesTxt);
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
