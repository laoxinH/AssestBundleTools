using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Bundles.Commands;
using AssestBundleTools.Application.Tests.Bundles.Queries;
using AssestBundleTools.Application.Tests.Assets.Queries;
using AssestBundleTools.Application.Tests.Editing;
using AssestBundleTools.Application.Tests.Replacement;
using AssestBundleTools.Application.Tests.Saving;
using AssestBundleTools.Application.Tests.TestDoubles;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Application.Tests.Workspace.Commands;
using AssestBundleTools.Application.Tests.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Testing;

internal static partial class ApplicationTestSuite
{
    public static async Task<int> RunAsync()
    {
        var failures = new List<string>();

        await RunTestAsync(failures, nameof(Failure_ShouldExposeSingleError), Failure_ShouldExposeSingleError);
        await RunTestAsync(failures, nameof(Failure_ShouldThrow_WhenErrorCollectionIsEmpty), Failure_ShouldThrow_WhenErrorCollectionIsEmpty);
        await RunTestAsync(failures, nameof(GenericSuccess_ShouldExposeValue), GenericSuccess_ShouldExposeValue);
        await RunTestAsync(
            failures,
            nameof(GetResourceProcessorCatalogEntriesHandlerTests.HandleAsync_ShouldReturnDefaultExportProcessorSlots),
            GetResourceProcessorCatalogEntriesHandlerTests.HandleAsync_ShouldReturnDefaultExportProcessorSlots);
        await RunTestAsync(
            failures,
            nameof(GetResourceProcessorCatalogEntriesHandlerTests.HandleAsync_ShouldReturnCancelled_WhenTokenIsCancelled),
            GetResourceProcessorCatalogEntriesHandlerTests.HandleAsync_ShouldReturnCancelled_WhenTokenIsCancelled);
        await RunTestAsync(
            failures,
            nameof(ResourceProcessorCatalogHandlerTests.Upsert_ShouldSaveUserManagedProcessor_WhenDefinitionIsValid),
            ResourceProcessorCatalogHandlerTests.Upsert_ShouldSaveUserManagedProcessor_WhenDefinitionIsValid);
        await RunTestAsync(
            failures,
            nameof(ResourceProcessorCatalogHandlerTests.Upsert_ShouldReturnValidationFailure_WhenResourceKindsAreMissing),
            ResourceProcessorCatalogHandlerTests.Upsert_ShouldReturnValidationFailure_WhenResourceKindsAreMissing);
        await RunTestAsync(
            failures,
            nameof(ResourceProcessorCatalogHandlerTests.Delete_ShouldRejectReadOnlyProcessor),
            ResourceProcessorCatalogHandlerTests.Delete_ShouldRejectReadOnlyProcessor);
        await RunTestAsync(
            failures,
            nameof(GetAssetEditDraftRestorePlanHandlerTests.HandleAsync_ShouldReturnFailure_WhenWorkspaceKeyIsBlank),
            GetAssetEditDraftRestorePlanHandlerTests.HandleAsync_ShouldReturnFailure_WhenWorkspaceKeyIsBlank);
        await RunTestAsync(
            failures,
            nameof(GetAssetEditDraftRestorePlanHandlerTests.HandleAsync_ShouldReturnRestorableCandidate_WhenDraftMatchesReference),
            GetAssetEditDraftRestorePlanHandlerTests.HandleAsync_ShouldReturnRestorableCandidate_WhenDraftMatchesReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetEditDraftRestorePlanHandlerTests.HandleAsync_ShouldClassifyMissingMismatchExpiredAndInvalidReferences),
            GetAssetEditDraftRestorePlanHandlerTests.HandleAsync_ShouldClassifyMissingMismatchExpiredAndInvalidReferences);
        await RunTestAsync(
            failures,
            nameof(GetAssetEditDraftRestorePlanHandlerTests.HandleAsync_ShouldAllowReferenceWithoutSourceFingerprint),
            GetAssetEditDraftRestorePlanHandlerTests.HandleAsync_ShouldAllowReferenceWithoutSourceFingerprint);
        await RunSpineAssetGroupTestsAsync(failures);
        await RunWorkspaceOperationLogTestsAsync(failures);
        await RunExtractionWorkflowTestsAsync(failures);
        await RunTestAsync(
            failures,
            nameof(WorkspaceChangeSetAssetEditDraftStoreTests.Load_ShouldResolveDraftDescriptor_FromWorkspaceChangeSet),
            WorkspaceChangeSetAssetEditDraftStoreTests.Load_ShouldResolveDraftDescriptor_FromWorkspaceChangeSet);
        await RunTestAsync(
            failures,
            nameof(WorkspaceChangeSetAssetEditDraftStoreTests.Load_ShouldReturnNull_WhenChangeSetWorkspaceDoesNotMatchLookup),
            WorkspaceChangeSetAssetEditDraftStoreTests.Load_ShouldReturnNull_WhenChangeSetWorkspaceDoesNotMatchLookup);
        await RunTestAsync(failures, nameof(RegisterWorkspaceCatalogEntry_ShouldReturnFailure_WhenSourcePathIsBlank), RegisterWorkspaceCatalogEntry_ShouldReturnFailure_WhenSourcePathIsBlank);
        await RunTestAsync(failures, nameof(RegisterWorkspaceCatalogEntry_ShouldPreserveExistingPinnedState_AndNormalizeDisplayName), RegisterWorkspaceCatalogEntry_ShouldPreserveExistingPinnedState_AndNormalizeDisplayName);
        await RunTestAsync(failures, nameof(SetWorkspaceCatalogEntryPinnedState_ShouldReturnFailure_WhenWorkspaceKeyIsBlank), SetWorkspaceCatalogEntryPinnedState_ShouldReturnFailure_WhenWorkspaceKeyIsBlank);
        await RunTestAsync(failures, nameof(SetWorkspaceCatalogEntryPinnedState_ShouldMovePinnedEntriesAheadOfUnpinnedEntries), SetWorkspaceCatalogEntryPinnedState_ShouldMovePinnedEntriesAheadOfUnpinnedEntries);
        await RunTestAsync(
            failures,
            nameof(RemoveWorkspaceBundleHandlerTests.HandleAsync_ShouldRemoveBundleArtifacts_AndPersistExcludedPath_WhenBundleRemainsCoveredByDirectoryImport),
            RemoveWorkspaceBundleHandlerTests.HandleAsync_ShouldRemoveBundleArtifacts_AndPersistExcludedPath_WhenBundleRemainsCoveredByDirectoryImport);
        await RunTestAsync(
            failures,
            nameof(RemoveWorkspaceBundleHandlerTests.HandleAsync_ShouldReturnFailure_WhenWorkspaceMetadataDoesNotExist),
            RemoveWorkspaceBundleHandlerTests.HandleAsync_ShouldReturnFailure_WhenWorkspaceMetadataDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(BundleArtifactCacheMaintenanceHandlerTests.GetUsage_ShouldReadUsage_FromRequestedWorkspaceDataRootAndWorkspace),
            BundleArtifactCacheMaintenanceHandlerTests.GetUsage_ShouldReadUsage_FromRequestedWorkspaceDataRootAndWorkspace);
        await RunTestAsync(
            failures,
            nameof(BundleArtifactCacheMaintenanceHandlerTests.Prune_ShouldUseRequestedLimits_AndReturnPruneResult),
            BundleArtifactCacheMaintenanceHandlerTests.Prune_ShouldUseRequestedLimits_AndReturnPruneResult);
        await RunTestAsync(
            failures,
            nameof(BundleArtifactCacheMaintenanceHandlerTests.Prune_ShouldReturnValidationFailure_WhenLimitsAreNegative),
            BundleArtifactCacheMaintenanceHandlerTests.Prune_ShouldReturnValidationFailure_WhenLimitsAreNegative);
        await RunTestAsync(
            failures,
            nameof(BundleArtifactCacheMaintenanceHandlerTests.GetWorkspaceCacheUsage_ShouldAggregateBundleArtifactAndPreviewUsage),
            BundleArtifactCacheMaintenanceHandlerTests.GetWorkspaceCacheUsage_ShouldAggregateBundleArtifactAndPreviewUsage);
        await RunTestAsync(
            failures,
            nameof(BundleArtifactCacheMaintenanceHandlerTests.ClearWorkspaceCache_ShouldClearBundleArtifactsAndPreviewCache),
            BundleArtifactCacheMaintenanceHandlerTests.ClearWorkspaceCache_ShouldClearBundleArtifactsAndPreviewCache);
        await RunTestAsync(
            failures,
            nameof(RegisterWorkspaceImportHandlerTests.HandleAsync_ShouldNormalizeDistinctPaths_UpdateExistingSource_AndClearReimportedExcludedPath),
            RegisterWorkspaceImportHandlerTests.HandleAsync_ShouldNormalizeDistinctPaths_UpdateExistingSource_AndClearReimportedExcludedPath);
        await RunTestAsync(
            failures,
            nameof(RegisterWorkspaceImportHandlerTests.HandleAsync_ShouldReturnFailure_WhenNormalizedPathsAreEmpty),
            RegisterWorkspaceImportHandlerTests.HandleAsync_ShouldReturnFailure_WhenNormalizedPathsAreEmpty);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldDeleteStaleBundleAndPreviewCaches_WhenRegisteredSourcesNoLongerDiscoverFiles),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldDeleteStaleBundleAndPreviewCaches_WhenRegisteredSourcesNoLongerDiscoverFiles);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReuseExistingCache_WhenFingerprintAndVersionsStillMatch),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReuseExistingCache_WhenFingerprintAndVersionsStillMatch);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReportDecryptionFailure_WhenDecryptorExecutionFails),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReportDecryptionFailure_WhenDecryptorExecutionFails);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldPassBundleCacheKeyToDecryptorRequest),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldPassBundleCacheKeyToDecryptorRequest);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldPreserveSourceFileName_WhenReaderReadsNormalizedBundleArtifact),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldPreserveSourceFileName_WhenReaderReadsNormalizedBundleArtifact);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldProcessCandidateBundlesInParallel_WhenExecutionOptionsAllowMultipleWorkers),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldProcessCandidateBundlesInParallel_WhenExecutionOptionsAllowMultipleWorkers);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldRunSynchronousBundleReadsOnMultipleWorkers_WhenConfigured),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldRunSynchronousBundleReadsOnMultipleWorkers_WhenConfigured);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReportBundleProgress_WhenProcessingCandidates),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReportBundleProgress_WhenProcessingCandidates);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldRecordProbeTelemetry_WhenAutoDetectionProbesUnresolvedCandidates),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldRecordProbeTelemetry_WhenAutoDetectionProbesUnresolvedCandidates);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReportReaderRejected_WhenDecryptedBundleCannotBeRead),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReportReaderRejected_WhenDecryptedBundleCannotBeRead);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReportReadFailure_WhenReaderReturnsFailure),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldReportReadFailure_WhenReaderReturnsFailure);
        await RunTestAsync(
            failures,
            nameof(WorkspaceImportExecutionGateTests.ExecuteExclusiveAsync_ShouldSerializeConcurrentOperations_ForSameWorkspaceKey),
            WorkspaceImportExecutionGateTests.ExecuteExclusiveAsync_ShouldSerializeConcurrentOperations_ForSameWorkspaceKey);
        await RunTestAsync(
            failures,
            nameof(WorkspaceImportExecutionGateTests.ExecuteExclusiveAsync_ShouldRemainReusable_AfterConcurrentImportWaves),
            WorkspaceImportExecutionGateTests.ExecuteExclusiveAsync_ShouldRemainReusable_AfterConcurrentImportWaves);
        await RunTestAsync(
            failures,
            nameof(WorkspaceImportExecutionOptionsResolverTests.ResolveBundleWorkerCount_ShouldFollowDocumentedThresholds),
            WorkspaceImportExecutionOptionsResolverTests.ResolveBundleWorkerCount_ShouldFollowDocumentedThresholds);
        await RunTestAsync(
            failures,
            nameof(WorkspaceImportExecutionOptionsResolverTests.ResolveBundleWorkerCount_ShouldUseManualWorkerCount_WhenConfigured),
            WorkspaceImportExecutionOptionsResolverTests.ResolveBundleWorkerCount_ShouldUseManualWorkerCount_WhenConfigured);
        await RunTestAsync(
            failures,
            nameof(WorkspaceContextPersistenceServiceTests.SaveWorkspaceBrowserState_ShouldPersistAssetEditorWindowSnapshots),
            WorkspaceContextPersistenceServiceTests.SaveWorkspaceBrowserState_ShouldPersistAssetEditorWindowSnapshots);
        await RunTestAsync(
            failures,
            nameof(WorkspaceContextPersistenceServiceTests.SaveWorkspaceActivationMetadata_ShouldPreserveExistingAssetEditorWindowSnapshots),
            WorkspaceContextPersistenceServiceTests.SaveWorkspaceActivationMetadata_ShouldPreserveExistingAssetEditorWindowSnapshots);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldReturnFallbackSummary_WhenDecryptionFails),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldReturnFallbackSummary_WhenDecryptionFails);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldReturnCachedPreview_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldReturnCachedPreview_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldUseFileReplacementPreview_ForTexture2D),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldUseFileReplacementPreview_ForTexture2D);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldUsePreviewSourceOverride_ForTextAsset),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldUsePreviewSourceOverride_ForTextAsset);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldUseFileReplacementPreview_ForTextAsset),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldUseFileReplacementPreview_ForTextAsset);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldUseFileReplacementPreview_ForAudioClip),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldUseFileReplacementPreview_ForAudioClip);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldUseWorkspaceReplacementPreview_WithTargetIdentity),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldUseWorkspaceReplacementPreview_WithTargetIdentity);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldUseAssociatedTextureReplacementPreview_ForSprite),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldUseAssociatedTextureReplacementPreview_ForSprite);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldCacheFullText_AndExposeBoundedDisplayText_WhenGeneratedTextIsLong),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldCacheFullText_AndExposeBoundedDisplayText_WhenGeneratedTextIsLong);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldExposeBoundedDisplayText_WhenCachedTextIsLong),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldExposeBoundedDisplayText_WhenCachedTextIsLong);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldLimitGeneratedTextDisplay_ByLineBudget),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldLimitGeneratedTextDisplay_ByLineBudget);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldExposeBoundedDisplayText_ForLongFileReplacementTextAsset),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldExposeBoundedDisplayText_ForLongFileReplacementTextAsset);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldReuseSourceTextReference_ForWorkspaceReplacementTextAsset),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldReuseSourceTextReference_ForWorkspaceReplacementTextAsset);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.LoadPreviewTextChunkHandler_ShouldReadChunk_FromPreviewCacheReference),
            GetAssetPreviewHandlerTests.LoadPreviewTextChunkHandler_ShouldReadChunk_FromPreviewCacheReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.LoadPreviewTextChunkHandler_ShouldReadChunk_FromFileReplacementReference),
            GetAssetPreviewHandlerTests.LoadPreviewTextChunkHandler_ShouldReadChunk_FromFileReplacementReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.LoadPreviewTextChunkHandler_ShouldReadChunk_FromEditDraftReference),
            GetAssetPreviewHandlerTests.LoadPreviewTextChunkHandler_ShouldReadChunk_FromEditDraftReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.LoadPreviewTextChunkHandler_ShouldClampRequestedLength_ToChunkBudget),
            GetAssetPreviewHandlerTests.LoadPreviewTextChunkHandler_ShouldClampRequestedLength_ToChunkBudget);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.LoadPreviewTextLinesHandler_ShouldReadLines_FromPreviewCacheReference),
            GetAssetPreviewHandlerTests.LoadPreviewTextLinesHandler_ShouldReadLines_FromPreviewCacheReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.LoadPreviewTextLinesHandler_ShouldReadLines_FromFileReplacementReference),
            GetAssetPreviewHandlerTests.LoadPreviewTextLinesHandler_ShouldReadLines_FromFileReplacementReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.LoadPreviewTextLinesHandler_ShouldReadLines_FromEditDraftReference),
            GetAssetPreviewHandlerTests.LoadPreviewTextLinesHandler_ShouldReadLines_FromEditDraftReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.SearchPreviewTextHandler_ShouldFindBoundedMatches_FromPreviewCacheReference),
            GetAssetPreviewHandlerTests.SearchPreviewTextHandler_ShouldFindBoundedMatches_FromPreviewCacheReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.SearchPreviewTextHandler_ShouldFindMatches_FromFileReplacementReference),
            GetAssetPreviewHandlerTests.SearchPreviewTextHandler_ShouldFindMatches_FromFileReplacementReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.SearchPreviewTextHandler_ShouldFindMatches_FromEditDraftReference),
            GetAssetPreviewHandlerTests.SearchPreviewTextHandler_ShouldFindMatches_FromEditDraftReference);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldRoundTripCachedImageOverlay_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldRoundTripCachedImageOverlay_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldReuseBundleArtifactManifest_WhenPreviewCacheMisses),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldReuseBundleArtifactManifest_WhenPreviewCacheMisses);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldUpdateBundleArtifactLastAccess_WhenManifestIsReused),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldUpdateBundleArtifactLastAccess_WhenManifestIsReused);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldApplyAutoPrunePolicy_AfterArtifactSave),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldApplyAutoPrunePolicy_AfterArtifactSave);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldApplyPreviewAutoPrunePolicy_AfterPreviewSave),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldApplyPreviewAutoPrunePolicy_AfterPreviewSave);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldSingleFlightBundleArtifactGeneration_WhenConcurrentPreviewCacheMisses),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldSingleFlightBundleArtifactGeneration_WhenConcurrentPreviewCacheMisses);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedMeshGeometry_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedMeshGeometry_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedMaterialPreview_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedMaterialPreview_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedAnimationClipPreview_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedAnimationClipPreview_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedFontPreview_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedFontPreview_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedGameObjectPreview_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedGameObjectPreview_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedMonoBehaviourPreview_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedMonoBehaviourPreview_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedAnimatorControllerPreview_WhenPreviewCacheStillMatches),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldHydrateCachedAnimatorControllerPreview_WhenPreviewCacheStillMatches);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenGeneratorVersionChanges),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenGeneratorVersionChanges);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldRegenerateMeshPreview_WhenGeneratorVersionChanges),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldRegenerateMeshPreview_WhenGeneratorVersionChanges);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenSourceFingerprintChanges),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenSourceFingerprintChanges);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenPreviewIdentityChanges),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenPreviewIdentityChanges);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenAudioTranscoderChanges),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenAudioTranscoderChanges);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenAudioSummaryCacheCanBeUpgradedWithNewTranscoder),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldRegeneratePreview_WhenAudioSummaryCacheCanBeUpgradedWithNewTranscoder);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldReturnFallbackSummary_WhenPreviewGenerationFails),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldReturnFallbackSummary_WhenPreviewGenerationFails);
        await RunTestAsync(
            failures,
            nameof(GetAssetPreviewHandlerTests.HandleAsync_ShouldReturnOutOfMemoryFallbackSummary_WhenPreviewGenerationRunsOutOfMemory),
            GetAssetPreviewHandlerTests.HandleAsync_ShouldReturnOutOfMemoryFallbackSummary_WhenPreviewGenerationRunsOutOfMemory);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetFileReplacementCandidateHandler_ShouldPreferTargetAssetType_WhenSharedExtensionMatches),
            ReplacementWorkflowHandlerTests.GetFileReplacementCandidateHandler_ShouldPreferTargetAssetType_WhenSharedExtensionMatches);
        await RunSpriteEditOperationTestsAsync(failures);
        await RunReplacementOperationRecorderTestsAsync(failures);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetFileReplacementCandidateHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist),
            ReplacementWorkflowHandlerTests.GetFileReplacementCandidateHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetFileReplacementCandidateHandler_ShouldReturnFailure_WhenSourceExtensionIsIncompatible),
            ReplacementWorkflowHandlerTests.GetFileReplacementCandidateHandler_ShouldReturnFailure_WhenSourceExtensionIsIncompatible);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceReplacementCandidatesHandler_ShouldReturnFailure_WhenRegistryProvidesNoCandidates),
            ReplacementWorkflowHandlerTests.GetWorkspaceReplacementCandidatesHandler_ShouldReturnFailure_WhenRegistryProvidesNoCandidates);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceReplacementCandidatesHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist),
            ReplacementWorkflowHandlerTests.GetWorkspaceReplacementCandidatesHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ReplaceAssetFromFileHandler_ShouldApplyReplacement_WhenTargetTypeMatches),
            ReplacementWorkflowHandlerTests.ReplaceAssetFromFileHandler_ShouldApplyReplacement_WhenTargetTypeMatches);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ReplaceAssetFromFileHandler_ShouldReturnFailure_WhenSourceFilePathIsBlank),
            ReplacementWorkflowHandlerTests.ReplaceAssetFromFileHandler_ShouldReturnFailure_WhenSourceFilePathIsBlank);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ReplaceAssetFromFileHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist),
            ReplacementWorkflowHandlerTests.ReplaceAssetFromFileHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ReplaceAssetFromWorkspaceHandler_ShouldApplyReplacement_WhenSourceAndTargetTypesMatch),
            ReplacementWorkflowHandlerTests.ReplaceAssetFromWorkspaceHandler_ShouldApplyReplacement_WhenSourceAndTargetTypesMatch);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ReplaceAssetFromWorkspaceHandler_ShouldReturnFailure_WhenSourceAssetDoesNotExist),
            ReplacementWorkflowHandlerTests.ReplaceAssetFromWorkspaceHandler_ShouldReturnFailure_WhenSourceAssetDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ReplaceAssetFromWorkspaceHandler_ShouldReturnFailure_WhenSourceAndTargetTypesDoNotMatch),
            ReplacementWorkflowHandlerTests.ReplaceAssetFromWorkspaceHandler_ShouldReturnFailure_WhenSourceAndTargetTypesDoNotMatch);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.UndoAssetReplacementHandler_ShouldReturnFailure_WhenNoAppliedReplacementExists),
            ReplacementWorkflowHandlerTests.UndoAssetReplacementHandler_ShouldReturnFailure_WhenNoAppliedReplacementExists);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.UndoAssetReplacementHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist),
            ReplacementWorkflowHandlerTests.UndoAssetReplacementHandler_ShouldReturnFailure_WhenTargetAssetDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.RedoAssetReplacementHandler_ShouldApplyRedo_WhenRedoSnapshotExists),
            ReplacementWorkflowHandlerTests.RedoAssetReplacementHandler_ShouldApplyRedo_WhenRedoSnapshotExists);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.RedoAssetReplacementHandler_ShouldReturnFailure_WhenNoRedoSnapshotExists),
            ReplacementWorkflowHandlerTests.RedoAssetReplacementHandler_ShouldReturnFailure_WhenNoRedoSnapshotExists);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldReturnFailure_WhenNoCandidatesSelected),
            ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldReturnFailure_WhenNoCandidatesSelected);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldApplySelectedCandidates_AndSkipUnsupportedTargets),
            ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldApplySelectedCandidates_AndSkipUnsupportedTargets);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldExecuteDuplicateOrdinaryCandidateOnce),
            ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldExecuteDuplicateOrdinaryCandidateOnce);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldRecordOnlyCurrentSpinePartRelation),
            ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldRecordOnlyCurrentSpinePartRelation);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldApplySpriteImageToAssociatedTexture),
            ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldApplySpriteImageToAssociatedTexture);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldApplyTextureBeforeSpriteOverride_WhenBothTargetSameTexture),
            ReplacementWorkflowHandlerTests.ExecuteWorkspaceBatchReplacementHandler_ShouldApplyTextureBeforeSpriteOverride_WhenBothTargetSameTexture);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseManifestSizeFirst_AndHashWhenSizeMatches),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseManifestSizeFirst_AndHashWhenSizeMatches);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldReturnManifestIssue_WhenWorkspaceKeyDiffers),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldReturnManifestIssue_WhenWorkspaceKeyDiffers);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldReturnCancelled_WhenHeuristicSpineCatalogCancels),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldReturnCancelled_WhenHeuristicSpineCatalogCancels);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldBuildHighConfidenceHeuristicCandidate_FromStandardExportPath),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldBuildHighConfidenceHeuristicCandidate_FromStandardExportPath);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldMarkHeuristicCandidateAmbiguous_WhenTargetsTie),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldMarkHeuristicCandidateAmbiguous_WhenTargetsTie);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseOrdinalSuffix_ForDuplicateOrdinaryAssets),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseOrdinalSuffix_ForDuplicateOrdinaryAssets);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseProjectedOrdinalSuffix_FromStandardExportRoot),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseProjectedOrdinalSuffix_FromStandardExportRoot);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSourceDirectoryName_AsImplicitBundleForHeuristicPlan),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSourceDirectoryName_AsImplicitBundleForHeuristicPlan);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSourceDirectoryTypeAndParentBundle_ForHeuristicPlan),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSourceDirectoryTypeAndParentBundle_ForHeuristicPlan);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineDirectoryAndParentBundle_ForHeuristicPlan),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineDirectoryAndParentBundle_ForHeuristicPlan);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldPreferSpineGroupScope_FromStandardExportRoot),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldPreferSpineGroupScope_FromStandardExportRoot);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldPreserveSpineGroupOrdinalScope_FromSpineDirectory),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldPreserveSpineGroupOrdinalScope_FromSpineDirectory);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldPreserveSpineGroupOrdinalScope_FromStandardExportRoot),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldPreserveSpineGroupOrdinalScope_FromStandardExportRoot);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineGroupCatalog_ForDuplicateHeuristicParts),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineGroupCatalog_ForDuplicateHeuristicParts);
        await RunTestAsync(
            failures,
            nameof(ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineGroupDirectoryAndAncestorBundle_ForHeuristicPlan),
            ReplacementWorkflowHandlerTests.GetWorkspaceBatchReplacementPlanHandler_ShouldUseSpineGroupDirectoryAndAncestorBundle_ForHeuristicPlan);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldReturnFailure_WhenWorkspaceDoesNotExist),
            SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldReturnFailure_WhenWorkspaceDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldReturnEmptyPreview_WhenWorkspaceHasNoChangeSummary),
            SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldReturnEmptyPreview_WhenWorkspaceHasNoChangeSummary);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldGroupBundlePlans_AndResolveDefaultOutputDirectory),
            SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldGroupBundlePlans_AndResolveDefaultOutputDirectory);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldExposeOutputPreflightBlockers),
            SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldExposeOutputPreflightBlockers);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldTreatExistingFilesAsReady_WhenOverwriteIsEnabled),
            SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldTreatExistingFilesAsReady_WhenOverwriteIsEnabled);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldResolveExistingArtifactInputs_FromWorkspaceCache),
            SaveWorkspaceHandlersTests.GetWorkspaceSavePreviewHandler_ShouldResolveExistingArtifactInputs_FromWorkspaceCache);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReturnFailure_WhenWorkspaceDoesNotExist),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReturnFailure_WhenWorkspaceDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReturnFailure_WhenNoBundlePlansExist),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReturnFailure_WhenNoBundlePlansExist);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReturnExecutionSummary_WhenBundlePlansExist),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReturnExecutionSummary_WhenBundlePlansExist);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReportOutputPreflightCounts),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReportOutputPreflightCounts);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReportArtifactInputCounts_WhenContextIsProvided),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldReportArtifactInputCounts_WhenContextIsProvided);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldExportSelectedBundles_AndSaveManifest),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldExportSelectedBundles_AndSaveManifest);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldBlockExistingOutput_WhenOverwriteIsDisabled),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldBlockExistingOutput_WhenOverwriteIsDisabled);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldForwardObjectWriteItems_FromOperationRecords),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldForwardObjectWriteItems_FromOperationRecords);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldSkipBundleWrite_WhenResourceProcessorFails),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldSkipBundleWrite_WhenResourceProcessorFails);
        await RunTestAsync(
            failures,
            nameof(SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldWriteManifest_ForPartialBundleSuccess),
            SaveWorkspaceHandlersTests.SaveWorkspaceAsNewBundleHandler_ShouldWriteManifest_ForPartialBundleSuccess);

        if (failures.Count == 0)
        {
            Console.WriteLine("All application tests passed.");
            return 0;
        }

        Console.Error.WriteLine("Application tests failed:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }

        return 1;
    }

    private static Task Failure_ShouldExposeSingleError()
    {
        var error = ApplicationErrors.WorkspaceCatalogKeyRequired();
        var result = Result.Failure(error);

        TestAssert.True(result.IsFailure, "Result should be failure.");
        TestAssert.False(result.IsSuccess, "Failure result should not be success.");
        TestAssert.False(result.IsCancelled, "Failure result should not be cancelled.");
        TestAssert.Equal(error, TestAssert.Single(result.Errors), "Failure result should expose the original error.");
        return Task.CompletedTask;
    }

    private static Task Failure_ShouldThrow_WhenErrorCollectionIsEmpty()
    {
        var exception = TestAssert.Throws<ArgumentException>(() => Result.Failure(Array.Empty<AppError>()));
        TestAssert.Equal("errors", exception.ParamName, "Empty failure should report the errors parameter.");
        return Task.CompletedTask;
    }

    private static Task GenericSuccess_ShouldExposeValue()
    {
        var result = Result<string>.Success("workspace");

        TestAssert.True(result.IsSuccess, "Success result should be marked successful.");
        TestAssert.False(result.IsFailure, "Success result should not be failure.");
        TestAssert.False(result.IsCancelled, "Success result should not be cancelled.");
        TestAssert.Equal("workspace", result.Value, "Generic success should expose its payload.");
        TestAssert.Empty(result.Errors, "Success result should not carry errors.");
        return Task.CompletedTask;
    }

    private static async Task RegisterWorkspaceCatalogEntry_ShouldReturnFailure_WhenSourcePathIsBlank()
    {
        var store = new TestWorkspaceCatalogStore(WorkspaceCatalogSnapshotDto.Empty);
        var handler = new RegisterWorkspaceCatalogEntryHandler(store);

        var result = await handler.HandleAsync(new RegisterWorkspaceCatalogEntryCommand(
            SourcePath: " ",
            DisplayName: null,
            WorkspaceDataRootPath: "C:\\Data",
            WorkspaceMode: WorkspaceMode.SinglePane));

        TestAssert.True(result.IsFailure, "Blank source path should fail.");
        TestAssert.Equal("Workspace.Catalog.SourcePathRequired", TestAssert.Single(result.Errors).Code, "Failure should use the expected error code.");
        TestAssert.Equal(0, store.SaveCallCount, "Validation failure should not save snapshot.");
    }

    private static async Task RegisterWorkspaceCatalogEntry_ShouldPreserveExistingPinnedState_AndNormalizeDisplayName()
    {
        const string sourcePath = "C:\\Games\\ExampleWorkspace";
        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(sourcePath);
        var existingEntry = new WorkspaceCatalogEntryDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Old Name",
            SourcePath: sourcePath,
            WorkspaceDataRootPath: "C:\\Data\\Old",
            WorkspaceMode: WorkspaceMode.SinglePane,
            LastActivatedAtUtc: new DateTimeOffset(2026, 4, 27, 12, 0, 0, TimeSpan.Zero),
            IsPinned: true,
            LastKnownState: "Loaded");
        var store = new TestWorkspaceCatalogStore(new WorkspaceCatalogSnapshotDto(
            Version: 0,
            LastActiveWorkspaceKey: null,
            RecentWorkspaces: [existingEntry]));
        var handler = new RegisterWorkspaceCatalogEntryHandler(store);

        var result = await handler.HandleAsync(new RegisterWorkspaceCatalogEntryCommand(
            SourcePath: $"  {sourcePath}  ",
            DisplayName: null,
            WorkspaceDataRootPath: "  C:\\Data\\New  ",
            WorkspaceMode: WorkspaceMode.SinglePane,
            IsPinned: false,
            LastKnownState: "Ready"));

        TestAssert.True(result.IsSuccess, "Valid registration should succeed.");
        var entry = result.Value ?? throw new InvalidOperationException("Expected result value.");
        TestAssert.Equal(workspaceKey, entry.WorkspaceKey, "Workspace key should be stable.");
        TestAssert.Equal("ExampleWorkspace", entry.DisplayName, "Display name should fall back to directory name.");
        TestAssert.Equal(sourcePath, entry.SourcePath, "Source path should be trimmed but preserved.");
        TestAssert.Equal("C:\\Data\\New", entry.WorkspaceDataRootPath, "Data root path should be trimmed.");
        TestAssert.True(entry.IsPinned, "Existing pinned state should win over incoming false value.");
        TestAssert.Equal("Ready", entry.LastKnownState, "Latest known state should be updated.");
        TestAssert.Equal(1, store.SaveCallCount, "Successful registration should save snapshot once.");
        TestAssert.Equal(workspaceKey, store.Snapshot.LastActiveWorkspaceKey, "Saved snapshot should update last active workspace.");
        TestAssert.Equal(1, store.Snapshot.Version, "Version should normalize to 1 when prior snapshot version is invalid.");
    }

    private static async Task SetWorkspaceCatalogEntryPinnedState_ShouldReturnFailure_WhenWorkspaceKeyIsBlank()
    {
        var store = new TestWorkspaceCatalogStore(WorkspaceCatalogSnapshotDto.Empty);
        var handler = new SetWorkspaceCatalogEntryPinnedStateHandler(store);

        var result = await handler.HandleAsync(new SetWorkspaceCatalogEntryPinnedStateCommand(
            WorkspaceKey: " ",
            IsPinned: true));

        TestAssert.True(result.IsFailure, "Blank workspace key should fail.");
        TestAssert.Equal("Workspace.Catalog.WorkspaceKeyRequired", TestAssert.Single(result.Errors).Code, "Failure should use the expected error code.");
        TestAssert.Equal(0, store.SaveCallCount, "Validation failure should not save snapshot.");
    }

    private static async Task SetWorkspaceCatalogEntryPinnedState_ShouldMovePinnedEntriesAheadOfUnpinnedEntries()
    {
        const string firstWorkspaceKey = "workspace-a";
        const string secondWorkspaceKey = "workspace-b";
        var olderTime = new DateTimeOffset(2026, 4, 26, 8, 0, 0, TimeSpan.Zero);
        var newerTime = new DateTimeOffset(2026, 4, 27, 8, 0, 0, TimeSpan.Zero);
        var store = new TestWorkspaceCatalogStore(new WorkspaceCatalogSnapshotDto(
            Version: 0,
            LastActiveWorkspaceKey: secondWorkspaceKey,
            RecentWorkspaces:
            [
                new WorkspaceCatalogEntryDto(
                    WorkspaceKey: firstWorkspaceKey,
                    DisplayName: "Workspace A",
                    SourcePath: "C:\\A",
                    WorkspaceDataRootPath: "C:\\A\\Data",
                    WorkspaceMode: WorkspaceMode.SinglePane,
                    LastActivatedAtUtc: olderTime,
                    IsPinned: false,
                    LastKnownState: null),
                new WorkspaceCatalogEntryDto(
                    WorkspaceKey: secondWorkspaceKey,
                    DisplayName: "Workspace B",
                    SourcePath: "C:\\B",
                    WorkspaceDataRootPath: "C:\\B\\Data",
                    WorkspaceMode: WorkspaceMode.SinglePane,
                    LastActivatedAtUtc: newerTime,
                    IsPinned: false,
                    LastKnownState: null)
            ]));
        var handler = new SetWorkspaceCatalogEntryPinnedStateHandler(store);

        var result = await handler.HandleAsync(new SetWorkspaceCatalogEntryPinnedStateCommand(
            WorkspaceKey: firstWorkspaceKey,
            IsPinned: true));

        TestAssert.True(result.IsSuccess, "Pinning an entry should succeed.");
        var snapshot = result.Value ?? throw new InvalidOperationException("Expected result value.");
        TestAssert.Equal(1, snapshot.Version, "Version should normalize to 1 when prior snapshot version is invalid.");
        TestAssert.Equal(firstWorkspaceKey, snapshot.RecentWorkspaces[0].WorkspaceKey, "Pinned entry should move ahead of unpinned entries.");
        TestAssert.True(snapshot.RecentWorkspaces[0].IsPinned, "Pinned entry should be marked pinned.");
        TestAssert.Equal(secondWorkspaceKey, snapshot.RecentWorkspaces[1].WorkspaceKey, "Unpinned entry should remain after pinned entry.");
        TestAssert.Equal(1, store.SaveCallCount, "Successful pin update should save snapshot once.");
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
