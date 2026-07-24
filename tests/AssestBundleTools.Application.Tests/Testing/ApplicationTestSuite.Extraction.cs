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
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldUseSkelExtension_ForDetectedSkeletonByDefault),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldUseSkelExtension_ForDetectedSkeletonByDefault);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldUseJsonExtension_ForDetectedJsonSkeletonWhenEnabled),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldUseJsonExtension_ForDetectedJsonSkeletonWhenEnabled);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldKeepTxtExtension_ForOrdinaryTextAssetDuringSkeletonDetection),
            ExtractionWorkflowHandlerTests.ExtractAssetHandler_ShouldKeepTxtExtension_ForOrdinaryTextAssetDuringSkeletonDetection);
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
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldWriteExportManifest_ForBatchReplacementReadback),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldWriteExportManifest_ForBatchReplacementReadback);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldLimitExportManifest_ToSelectedAssets),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldLimitExportManifest_ToSelectedAssets);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldReportWorkspaceBatchProgress),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldReportWorkspaceBatchProgress);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.GetWorkspaceBatchExtractionPreviewHandler_ShouldRespectSelectedBundlesAndCategories),
            ExtractionWorkflowHandlerTests.GetWorkspaceBatchExtractionPreviewHandler_ShouldRespectSelectedBundlesAndCategories);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.GetWorkspaceBatchExtractionPreviewHandler_ShouldExposeUnknownTypeDisplayNames),
            ExtractionWorkflowHandlerTests.GetWorkspaceBatchExtractionPreviewHandler_ShouldExposeUnknownTypeDisplayNames);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.GetWorkspaceBatchExtractionPreviewHandler_ShouldIncludeSpineGroups_WhenAllCategoryIsSelected),
            ExtractionWorkflowHandlerTests.GetWorkspaceBatchExtractionPreviewHandler_ShouldIncludeSpineGroups_WhenAllCategoryIsSelected);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.GetWorkspaceBatchExtractionPreviewHandler_ShouldPassWorkspaceDataRootPath_ToSpinePlanner),
            ExtractionWorkflowHandlerTests.GetWorkspaceBatchExtractionPreviewHandler_ShouldPassWorkspaceDataRootPath_ToSpinePlanner);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldRespectSelectedBundlesAndCategories),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldRespectSelectedBundlesAndCategories);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldFilterUnknownTypeByDisplayName),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldFilterUnknownTypeByDisplayName);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.LoadAssetListHandler_ShouldDisambiguateDuplicateAssetNames),
            ExtractionWorkflowHandlerTests.LoadAssetListHandler_ShouldDisambiguateDuplicateAssetNames);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldDisambiguateDuplicateAssetOutputPaths),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldDisambiguateDuplicateAssetOutputPaths);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldExtractSpineGroups_WhenAllCategoryIsSelected),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldExtractSpineGroups_WhenAllCategoryIsSelected);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldLimitExportManifest_ToSelectedSpineGroups),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldLimitExportManifest_ToSelectedSpineGroups);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldPassWorkspaceDataRootPath_ToSpinePlanner),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldPassWorkspaceDataRootPath_ToSpinePlanner);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldExtractBundlesInParallel_AndKeepSameBundleSequential),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldExtractBundlesInParallel_AndKeepSameBundleSequential);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldThrottleCrossBundleParallelism_WhenMemoryPressureIsHigh),
            ExtractionWorkflowHandlerTests.ExtractWorkspaceAssetsHandler_ShouldThrottleCrossBundleParallelism_WhenMemoryPressureIsHigh);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldExtractSkeletonAtlasTextureAndAlphaPages),
            ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldExtractSkeletonAtlasTextureAndAlphaPages);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldUseJsonExtension_ForJsonSkeleton_WhenPreferenceEnabled),
            ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldUseJsonExtension_ForJsonSkeleton_WhenPreferenceEnabled);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldUseExtractionPlanDetailsMode),
            ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldUseExtractionPlanDetailsMode);
        await RunTestAsync(
            failures,
            nameof(ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldPreferCatalogPlan_WhenAvailable),
            ExtractionWorkflowHandlerTests.ExtractSpineAssetGroupHandler_ShouldPreferCatalogPlan_WhenAvailable);
    }
}
