using AssestBundleTools.Infrastructure.Tests.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Tests.Bundles.Internal;
using AssestBundleTools.Infrastructure.Tests.Bundles.Processing;
using AssestBundleTools.Infrastructure.Tests.Bundles.PostExport;
using AssestBundleTools.Infrastructure.Tests.Bundles.Reading;
using AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;
using AssestBundleTools.Infrastructure.Tests.Extensions.Installation;
using AssestBundleTools.Infrastructure.Tests.Settings.Persistence;
using AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;
using AssestBundleTools.Infrastructure.Tests.Workspace.Diagnostics;
using AssestBundleTools.Infrastructure.Tests.Workspace.Extraction;
using AssestBundleTools.Infrastructure.Tests.Workspace.Runtime;
using AssestBundleTools.Infrastructure.Tests.Workspace.Saving;
using AssestBundleTools.Infrastructure.Tests.Updates;

namespace AssestBundleTools.Infrastructure.Tests.Testing;

internal static class InfrastructureTestSuite
{
    public static async Task<int> RunAsync()
    {
        var failures = new List<string>();

        await RunTestAsync(
            failures,
            nameof(VelopackApplicationUpdateGatewayTests.RuntimeInfo_ShouldReportUnmanaged_WhenRunningOutsideVelopackPackage),
            VelopackApplicationUpdateGatewayTests.RuntimeInfo_ShouldReportUnmanaged_WhenRunningOutsideVelopackPackage);

        await RunTestAsync(
            failures,
            nameof(JsonAppPreferencesStoreTests.Save_ShouldRoundTripDisplayLanguage),
            JsonAppPreferencesStoreTests.Save_ShouldRoundTripDisplayLanguage);
        await RunTestAsync(
            failures,
            nameof(JsonAppPreferencesStoreTests.Load_ShouldUseSystemLanguage_WhenLegacySnapshotOmitsDisplayLanguage),
            JsonAppPreferencesStoreTests.Load_ShouldUseSystemLanguage_WhenLegacySnapshotOmitsDisplayLanguage);
        await RunTestAsync(
            failures,
            nameof(JsonAppPreferencesStoreTests.Save_ShouldRoundTripElevatedDragDropCompatibility),
            JsonAppPreferencesStoreTests.Save_ShouldRoundTripElevatedDragDropCompatibility);
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
            nameof(JsonWorkspaceMetadataStoreTests.Load_ShouldDefaultUseSourceCodec_ForLegacyExportSettings),
            JsonWorkspaceMetadataStoreTests.Load_ShouldDefaultUseSourceCodec_ForLegacyExportSettings);
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
            nameof(FileSystemWorkspaceDiagnosticLogStoreTests.Append_ShouldWriteTimestampedSingleLineEntries_UnderWorkspaceRoot),
            FileSystemWorkspaceDiagnosticLogStoreTests.Append_ShouldWriteTimestampedSingleLineEntries_UnderWorkspaceRoot);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceChangeStoreTests.Save_ShouldRoundTripEditDraftDescriptors),
            JsonWorkspaceChangeStoreTests.Save_ShouldRoundTripEditDraftDescriptors);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceChangeStoreTests.Save_ShouldRoundTripOperationRecords_AsStringEnums),
            JsonWorkspaceChangeStoreTests.Save_ShouldRoundTripOperationRecords_AsStringEnums);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceChangeStoreTests.Load_ShouldReadLegacyChangeSetWithoutOperationFields),
            JsonWorkspaceChangeStoreTests.Load_ShouldReadLegacyChangeSetWithoutOperationFields);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceChangeStoreTests.Save_ShouldReplaceChangeSet_WhenExistingFileHasSharedReadLease),
            JsonWorkspaceChangeStoreTests.Save_ShouldReplaceChangeSet_WhenExistingFileHasSharedReadLease);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.SaveTextPayload_ShouldReadChunkLinesAndSearch_FromDraftPayload),
            JsonAssetEditDraftTextStoreTests.SaveTextPayload_ShouldReadChunkLinesAndSearch_FromDraftPayload);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.Delete_ShouldRemoveDraftPayload),
            JsonAssetEditDraftTextStoreTests.Delete_ShouldRemoveDraftPayload);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldUpdatePayloadDescriptorAndLineIndex),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldUpdatePayloadDescriptorAndLineIndex);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldAppendPatchLog_WithoutRewritingPayloadFile),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldAppendPatchLog_WithoutRewritingPayloadFile);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldCreatePatchBackedDraft_FromDeferredSourceReference),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldCreatePatchBackedDraft_FromDeferredSourceReference);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldMaterializeFullReplacement_WhenDeferredSourceCacheWasPruned),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldMaterializeFullReplacement_WhenDeferredSourceCacheWasPruned);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldCreatePatchBackedDraft_FromDeferredFileReplacementSourceReference),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldCreatePatchBackedDraft_FromDeferredFileReplacementSourceReference);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldApplySubsequentPatchLogEntries),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldApplySubsequentPatchLogEntries);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldReadOffsetChunk_FromPatchBackedDraft),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldReadOffsetChunk_FromPatchBackedDraft);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldBranchFromRequestedRevision_WhenHeadIsNewer),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldBranchFromRequestedRevision_WhenHeadIsNewer);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldUseNormalizedOffsets_ForDeferredCrlfSource),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldUseNormalizedOffsets_ForDeferredCrlfSource);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldRejectStaleBaseline_WithoutChangingPayload),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldRejectStaleBaseline_WithoutChangingPayload);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldRejectInvalidRange_WithoutChangingPayload),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldRejectInvalidRange_WithoutChangingPayload);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldRejectLengthExceeded_WithoutChangingPayload),
            JsonAssetEditDraftTextStoreTests.ApplyTextPatch_ShouldRejectLengthExceeded_WithoutChangingPayload);
        await RunTestAsync(
            failures,
            nameof(JsonAssetEditDraftTextStoreTests.SaveTextPayload_ShouldRejectInvalidDraftPathSegments),
            JsonAssetEditDraftTextStoreTests.SaveTextPayload_ShouldRejectInvalidDraftPathSegments);
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
            nameof(JsonPreviewCacheStoreTests.Delete_ShouldRemoveTextLineIndexSidecar),
            JsonPreviewCacheStoreTests.Delete_ShouldRemoveTextLineIndexSidecar);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.LoadTextChunk_ShouldReadNormalizedTextRange_FromPayloadFile),
            JsonPreviewCacheStoreTests.LoadTextChunk_ShouldReadNormalizedTextRange_FromPayloadFile);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.LoadTextLines_ShouldReadNormalizedLineRange_FromPayloadFile),
            JsonPreviewCacheStoreTests.LoadTextLines_ShouldReadNormalizedLineRange_FromPayloadFile);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Save_ShouldWriteTextLineIndexSidecar_ForTextPreviewPayload),
            JsonPreviewCacheStoreTests.Save_ShouldWriteTextLineIndexSidecar_ForTextPreviewPayload);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.LoadTextChunk_ShouldReadFromCharacterIndexSidecar_ForLargeOffset),
            JsonPreviewCacheStoreTests.LoadTextChunk_ShouldReadFromCharacterIndexSidecar_ForLargeOffset);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.LoadTextLines_ShouldFallback_WhenTextLineIndexSidecarIsMissing),
            JsonPreviewCacheStoreTests.LoadTextLines_ShouldFallback_WhenTextLineIndexSidecarIsMissing);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Save_ShouldKeepTextLineIndexSidecars_IsolatedByPayloadFile),
            JsonPreviewCacheStoreTests.Save_ShouldKeepTextLineIndexSidecars_IsolatedByPayloadFile);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.SearchText_ShouldFindBoundedMatches_FromPayloadFile),
            JsonPreviewCacheStoreTests.SearchText_ShouldFindBoundedMatches_FromPayloadFile);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.SearchText_ShouldReturnTruncatedLineSnippet_ForVeryLongLine),
            JsonPreviewCacheStoreTests.SearchText_ShouldReturnTruncatedLineSnippet_ForVeryLongLine);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.GetUsage_ShouldReturnPreviewEntriesAndTotalSize),
            JsonPreviewCacheStoreTests.GetUsage_ShouldReturnPreviewEntriesAndTotalSize);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.GetUsage_ShouldIncludeTextLineIndexSidecarSize),
            JsonPreviewCacheStoreTests.GetUsage_ShouldIncludeTextLineIndexSidecarSize);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Prune_ShouldDeleteOldestPreviewEntries_WhenCountLimitIsExceeded),
            JsonPreviewCacheStoreTests.Prune_ShouldDeleteOldestPreviewEntries_WhenCountLimitIsExceeded);
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
            nameof(JsonSpineGroupCatalogCacheStoreTests.Save_ShouldRoundTripCatalogEntry_AndDeleteWorkspace),
            JsonSpineGroupCatalogCacheStoreTests.Save_ShouldRoundTripCatalogEntry_AndDeleteWorkspace);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceImportRecognitionCacheStoreTests.Save_ShouldRoundTripReplaceDeleteAndDeleteWorkspace),
            JsonWorkspaceImportRecognitionCacheStoreTests.Save_ShouldRoundTripReplaceDeleteAndDeleteWorkspace);
        await RunTestAsync(
            failures,
            nameof(JsonBundleArtifactStoreTests.Save_ShouldMaterializeDerivedBundle_AndRoundTripManifest),
            JsonBundleArtifactStoreTests.Save_ShouldMaterializeDerivedBundle_AndRoundTripManifest);
        await RunTestAsync(
            failures,
            nameof(JsonBundleArtifactStoreTests.Save_ShouldMaterializePassThroughBundle_AndRoundTripManifest),
            JsonBundleArtifactStoreTests.Save_ShouldMaterializePassThroughBundle_AndRoundTripManifest);
        await RunTestAsync(
            failures,
            nameof(JsonBundleArtifactStoreTests.ResolveBundlePath_ShouldRejectLegacyManifestOnlyPassThroughArtifact),
            JsonBundleArtifactStoreTests.ResolveBundlePath_ShouldRejectLegacyManifestOnlyPassThroughArtifact);
        await RunTestAsync(
            failures,
            nameof(JsonBundleArtifactStoreTests.UpdateLastAccessed_ShouldPersistManifestAccessTime),
            JsonBundleArtifactStoreTests.UpdateLastAccessed_ShouldPersistManifestAccessTime);
        await RunTestAsync(
            failures,
            nameof(JsonBundleArtifactStoreTests.GetUsage_ShouldReturnArtifactEntriesAndTotalSize),
            JsonBundleArtifactStoreTests.GetUsage_ShouldReturnArtifactEntriesAndTotalSize);
        await RunTestAsync(
            failures,
            nameof(JsonBundleArtifactStoreTests.Prune_ShouldDeleteLeastRecentlyAccessedArtifacts_WhenSizeLimitIsExceeded),
            JsonBundleArtifactStoreTests.Prune_ShouldDeleteLeastRecentlyAccessedArtifacts_WhenSizeLimitIsExceeded);
        await RunTestAsync(
            failures,
            nameof(JsonBundleArtifactStoreTests.Prune_ShouldPreserveProtectedArtifact_WhenSizeLimitIsExceeded),
            JsonBundleArtifactStoreTests.Prune_ShouldPreserveProtectedArtifact_WhenSizeLimitIsExceeded);
        await RunTestAsync(
            failures,
            nameof(JsonBundleArtifactStoreTests.DeleteWorkspace_ShouldRemoveBundleArtifactDirectory),
            JsonBundleArtifactStoreTests.DeleteWorkspace_ShouldRemoveBundleArtifactDirectory);
        await RunTestAsync(
            failures,
            nameof(FileSystemAssetExtractionFileStoreTests.WriteAsync_ShouldWriteContent_UnderWorkspaceDataRoot_ForRelativeOutputPath),
            FileSystemAssetExtractionFileStoreTests.WriteAsync_ShouldWriteContent_UnderWorkspaceDataRoot_ForRelativeOutputPath);
        await RunTestAsync(
            failures,
            nameof(FileSystemAssetExtractionFileStoreTests.WriteAsync_ShouldCopySourcePayload_WhenContentIsNotProvided),
            FileSystemAssetExtractionFileStoreTests.WriteAsync_ShouldCopySourcePayload_WhenContentIsNotProvided);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceExportManifestStoreTests.SaveAsync_ShouldWriteReadableManifest_WithSizeAndMd5),
            FileSystemWorkspaceExportManifestStoreTests.SaveAsync_ShouldWriteReadableManifest_WithSizeAndMd5);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldFail_WhenNoObjectWriteItemsAreProvided),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldFail_WhenNoObjectWriteItemsAreProvided);
        await RunTestAsync(
            failures,
            nameof(BundleExportWriteDiagnosticsTests.ResolveTopLevelDetail_ShouldIncludeStageProcessSourceSizeAndException),
            BundleExportWriteDiagnosticsTests.ResolveTopLevelDetail_ShouldIncludeStageProcessSourceSizeAndException);
        await RunTestAsync(
            failures,
            nameof(SpriteEditGeometryWriterProjectionTests.Create_ShouldConvertTopOriginGeometry_ToUnitySpriteFields),
            SpriteEditGeometryWriterProjectionTests.Create_ShouldConvertTopOriginGeometry_ToUnitySpriteFields);
        await RunTestAsync(
            failures,
            nameof(SpriteEditGeometryWriterProjectionTests.Create_ShouldNormalizePivot_WhenTargetSpriteUsesNormalizedPivot),
            SpriteEditGeometryWriterProjectionTests.Create_ShouldNormalizePivot_WhenTargetSpriteUsesNormalizedPivot);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRewriteTextAssetObject_WhenRealSampleBundleIsAvailable),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRewriteTextAssetObject_WhenRealSampleBundleIsAvailable);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldApplySourceCodec_AfterStandardObjectWrite),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldApplySourceCodec_AfterStandardObjectWrite);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldReplaceExistingOutput_WhenOverwriteIsEnabled),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldReplaceExistingOutput_WhenOverwriteIsEnabled);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldFailWhenOutputExists_AndOverwriteIsDisabled),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldFailWhenOutputExists_AndOverwriteIsDisabled);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRewriteTexture2DObject_WhenRealSampleBundleIsAvailable),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRewriteTexture2DObject_WhenRealSampleBundleIsAvailable);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRewriteSpriteGeometry_WhenRealSampleBundleIsAvailable),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRewriteSpriteGeometry_WhenRealSampleBundleIsAvailable);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldFallbackTexture2DToRgba32_WhenTargetFormatCannotBeEncoded),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldFallbackTexture2DToRgba32_WhenTargetFormatCannotBeEncoded);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldPreserveOriginalCompression_WhenRequested),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldPreserveOriginalCompression_WhenRequested);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldWriteExplicitLz4Compression_WhenRequested),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldWriteExplicitLz4Compression_WhenRequested);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExportManifestStoreTests.SaveAsync_ShouldWriteReadableBundleExportManifest_WithHashesAndProcessors),
            FileSystemWorkspaceBundleExportManifestStoreTests.SaveAsync_ShouldWriteReadableBundleExportManifest_WithHashesAndProcessors);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExportManifestStoreTests.SaveAsync_ShouldOmitWrittenAssets_ForFailedBundle),
            FileSystemWorkspaceBundleExportManifestStoreTests.SaveAsync_ShouldOmitWrittenAssets_ForFailedBundle);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceSaveOutputProbeTests.Probe_ShouldReportReady_WhenTargetDoesNotExist),
            FileSystemWorkspaceSaveOutputProbeTests.Probe_ShouldReportReady_WhenTargetDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceSaveOutputProbeTests.Probe_ShouldReportExistingFile_AsWriteBlocker),
            FileSystemWorkspaceSaveOutputProbeTests.Probe_ShouldReportExistingFile_AsWriteBlocker);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceSaveOutputProbeTests.Probe_ShouldReportDirectorySegmentBlockedByFile),
            FileSystemWorkspaceSaveOutputProbeTests.Probe_ShouldReportDirectorySegmentBlockedByFile);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryTests.OpenWorkspace_ShouldRepairLegacyNormalizedBundleDisplayName_FromSourcePath),
            CachedWorkspaceSessionRegistryTests.OpenWorkspace_ShouldRepairLegacyNormalizedBundleDisplayName_FromSourcePath);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryDuplicateAssetIdTests.OpenWorkspace_ShouldKeepDuplicateLegacyAssetIdsScopedByBundle),
            CachedWorkspaceSessionRegistryDuplicateAssetIdTests.OpenWorkspace_ShouldKeepDuplicateLegacyAssetIdsScopedByBundle);
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
            nameof(CachedWorkspaceSessionRegistryTests.TryGetWorkspaceChangeSummary_ShouldIncludeHiddenExtractedAssets),
            CachedWorkspaceSessionRegistryTests.TryGetWorkspaceChangeSummary_ShouldIncludeHiddenExtractedAssets);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryTests.TryGetAssociatedAssetImpacts_ShouldReturnVisibleDependents_WhenTextureChanges),
            CachedWorkspaceSessionRegistryTests.TryGetAssociatedAssetImpacts_ShouldReturnVisibleDependents_WhenTextureChanges);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryTests.TryGetAssociatedAssetImpacts_ShouldReturnDirectDependents_WhenTextAssetChanges),
            CachedWorkspaceSessionRegistryTests.TryGetAssociatedAssetImpacts_ShouldReturnDirectDependents_WhenTextAssetChanges);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMaterializePersistedExtractionOperations_FromChangeSet),
            CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMaterializePersistedExtractionOperations_FromChangeSet);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMaterializePersistedReplacementOperations_FromChangeSet),
            CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMaterializePersistedReplacementOperations_FromChangeSet);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMaterializePersistedRedoReplacementOperation_FromChangeSet),
            CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMaterializePersistedRedoReplacementOperation_FromChangeSet);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMaterializePersistedSpriteEditOperation_FromChangeSet),
            CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMaterializePersistedSpriteEditOperation_FromChangeSet);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMarkOnlyLatestTextAssetContentOperationEffective),
            CachedWorkspaceSessionRegistryOperationRestoreTests.OpenWorkspace_ShouldMarkOnlyLatestTextAssetContentOperationEffective);
        await RunTestAsync(
            failures,
            nameof(CachedWorkspaceSessionRegistryOperationRestoreTests.TryGetAssetDetails_ShouldProjectDependencyReplacement_AsAssociatedChangeRecord),
            CachedWorkspaceSessionRegistryOperationRestoreTests.TryGetAssetDetails_ShouldProjectDependencyReplacement_AsAssociatedChangeRecord);
        await RunTestAsync(
            failures,
            nameof(UnityAssetBundleFileInspectorTests.CanTreatAsBundle_ShouldRejectPngContent_WithNormalizedBundleExtension),
            UnityAssetBundleFileInspectorTests.CanTreatAsBundle_ShouldRejectPngContent_WithNormalizedBundleExtension);
        await RunTestAsync(
            failures,
            nameof(UnityAssetBundleFileInspectorTests.CanTreatAsBundle_ShouldAcceptKnownUnityHeader_WithoutKnownExtension),
            UnityAssetBundleFileInspectorTests.CanTreatAsBundle_ShouldAcceptKnownUnityHeader_WithoutKnownExtension);
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
            nameof(AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldShareInFlightLoad_ForSameBundlePath),
            AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldShareInFlightLoad_ForSameBundlePath);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldShareInFlightLoad_ForSameExplicitCacheKey),
            AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldShareInFlightLoad_ForSameExplicitCacheKey);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldNotShareInFlightLoad_WhenExplicitCacheKeysDiffer),
            AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldNotShareInFlightLoad_WhenExplicitCacheKeysDiffer);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldRemoveFailedLoadEntry),
            AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldRemoveFailedLoadEntry);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldRetainInFlightLoad_WhenWaitingAcquireIsCancelled),
            AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldRetainInFlightLoad_WhenWaitingAcquireIsCancelled);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldEvictLeastRecentlyUsedIdleSession_WhenCapacityIsExceeded),
            AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldEvictLeastRecentlyUsedIdleSession_WhenCapacityIsExceeded);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldRetreatToPressureLimit_WhenMemoryPressureIsReported),
            AssetsToolsBundleLoadedSessionCacheTests.AcquireAsync_ShouldRetreatToPressureLimit_WhenMemoryPressureIsReported);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsBundleLoadedSessionCacheTests.InvalidateBundleArtifact_ShouldRemoveMatchingArtifactIdentity),
            AssetsToolsBundleLoadedSessionCacheTests.InvalidateBundleArtifact_ShouldRemoveMatchingArtifactIdentity);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsBundleLoadedSessionCacheTests.InvalidateWorkspace_ShouldRemoveMatchingWorkspaceArtifactIdentities),
            AssetsToolsBundleLoadedSessionCacheTests.InvalidateWorkspace_ShouldRemoveMatchingWorkspaceArtifactIdentities);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsTextAssetPreviewClassifierTests.TryDecodeReadableTextPayload_ShouldAcceptJsonSkelText),
            AssetsToolsTextAssetPreviewClassifierTests.TryDecodeReadableTextPayload_ShouldAcceptJsonSkelText);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsTextAssetPreviewClassifierTests.TryDecodeJsonTextPayload_ShouldAcceptJsonSkelText),
            AssetsToolsTextAssetPreviewClassifierTests.TryDecodeJsonTextPayload_ShouldAcceptJsonSkelText);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsTextAssetPreviewClassifierTests.TryDecodeReadableTextPayload_ShouldRejectBinarySkelPayload),
            AssetsToolsTextAssetPreviewClassifierTests.TryDecodeReadableTextPayload_ShouldRejectBinarySkelPayload);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsTextAssetPreviewClassifierTests.TryDecodeReadableTextPayload_ShouldRejectUtf8ControlPayload),
            AssetsToolsTextAssetPreviewClassifierTests.TryDecodeReadableTextPayload_ShouldRejectUtf8ControlPayload);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsTextAssetPreviewClassifierTests.TryDecodeJsonTextPayload_ShouldRejectReadableNonJsonPayload),
            AssetsToolsTextAssetPreviewClassifierTests.TryDecodeJsonTextPayload_ShouldRejectReadableNonJsonPayload);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsTextAssetPreviewClassifierTests.TryDecodeJsonTextPayload_ShouldRejectInvalidJsonPayload),
            AssetsToolsTextAssetPreviewClassifierTests.TryDecodeJsonTextPayload_ShouldRejectInvalidJsonPayload);
        await RunTestAsync(
            failures,
            nameof(AssetsToolsTextAssetPreviewClassifierTests.IsSpineSkeletonPath_ShouldMatchSkelAndSkelBytes),
            AssetsToolsTextAssetPreviewClassifierTests.IsSpineSkeletonPath_ShouldMatchSkelAndSkelBytes);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.LoadFilePreviewAsync_ShouldReturnNormalizedText_ForTextAssetAtlas),
            FileSystemReplacementPreviewSourceProviderTests.LoadFilePreviewAsync_ShouldReturnNormalizedText_ForTextAssetAtlas);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.LoadFilePreviewAsync_ShouldReturnBinary_ForBinarySkelTextAsset),
            FileSystemReplacementPreviewSourceProviderTests.LoadFilePreviewAsync_ShouldReturnBinary_ForBinarySkelTextAsset);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.LoadFileTextChunkAsync_ShouldReadNormalizedTextRange_ForTextAssetFile),
            FileSystemReplacementPreviewSourceProviderTests.LoadFileTextChunkAsync_ShouldReadNormalizedTextRange_ForTextAssetFile);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.LoadFileTextLinesAsync_ShouldReadNormalizedLineRange_ForTextAssetFile),
            FileSystemReplacementPreviewSourceProviderTests.LoadFileTextLinesAsync_ShouldReadNormalizedLineRange_ForTextAssetFile);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.SearchFileTextAsync_ShouldFindBoundedMatches_ForTextAssetFile),
            FileSystemReplacementPreviewSourceProviderTests.SearchFileTextAsync_ShouldFindBoundedMatches_ForTextAssetFile);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.LoadFileTextChunkAsync_ShouldRejectBinarySkelTextAssetFile),
            FileSystemReplacementPreviewSourceProviderTests.LoadFileTextChunkAsync_ShouldRejectBinarySkelTextAssetFile);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.SearchFileTextAsync_ShouldRejectBinarySkelTextAssetFile),
            FileSystemReplacementPreviewSourceProviderTests.SearchFileTextAsync_ShouldRejectBinarySkelTextAssetFile);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.LoadFilePreviewAsync_ShouldReturnImagePayload_ForTextureReplacement),
            FileSystemReplacementPreviewSourceProviderTests.LoadFilePreviewAsync_ShouldReturnImagePayload_ForTextureReplacement);
        await RunTestAsync(
            failures,
            nameof(FileSystemReplacementPreviewSourceProviderTests.LoadFilePreviewAsync_ShouldReturnAudioPayload_ForAudioClipReplacement),
            FileSystemReplacementPreviewSourceProviderTests.LoadFilePreviewAsync_ShouldReturnAudioPayload_ForAudioClipReplacement);
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
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldClipV38RegionDrawCommands_FromJsonSkeleton),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldClipV38RegionDrawCommands_FromJsonSkeleton);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryCreateSessionAsync_ShouldSeekV38RuntimeFrameWithoutSampledCache),
            SpineAnimationRuntimePreviewServiceTests.TryCreateSessionAsync_ShouldSeekV38RuntimeFrameWithoutSampledCache);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryCreateSessionAsync_ShouldSeekV42RuntimeFrameWithoutSampledCache),
            SpineAnimationRuntimePreviewServiceTests.TryCreateSessionAsync_ShouldSeekV42RuntimeFrameWithoutSampledCache);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.SampleFixedFramesAsync_ShouldSampleDeterministicV38RuntimeFrames),
            SpineAnimationRuntimePreviewServiceTests.SampleFixedFramesAsync_ShouldSampleDeterministicV38RuntimeFrames);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.SampleFixedFramesAsync_ShouldUseSampledFallbackSession_WhenRuntimeAdapterIsMissing),
            SpineAnimationRuntimePreviewServiceTests.SampleFixedFramesAsync_ShouldUseSampledFallbackSession_WhenRuntimeAdapterIsMissing);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.PlanFrameSequenceExportAsync_ShouldCreateStableCanvasAndFrameNames_ForV38RuntimeFrames),
            SpineAnimationRuntimePreviewServiceTests.PlanFrameSequenceExportAsync_ShouldCreateStableCanvasAndFrameNames_ForV38RuntimeFrames);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryCreateSessionAsync_ShouldUseSampledFallbackSession_WhenOfficialPlaybackAdapterIsMissing),
            SpineAnimationRuntimePreviewServiceTests.TryCreateSessionAsync_ShouldUseSampledFallbackSession_WhenOfficialPlaybackAdapterIsMissing);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryCreateSessionAsync_ShouldReturnCancelledRuntimeFrame_WhenFrameTokenIsAlreadyCancelled),
            SpineAnimationRuntimePreviewServiceTests.TryCreateSessionAsync_ShouldReturnCancelledRuntimeFrame_WhenFrameTokenIsAlreadyCancelled);
        await RunTestAsync(
            failures,
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldSkipSampledFrames_WhenPlaybackSessionWillDrivePreview),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldSkipSampledFrames_WhenPlaybackSessionWillDrivePreview);
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
            nameof(SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldRejectOversizedJsonAtlasBeforeRuntimeParse),
            SpineAnimationRuntimePreviewServiceTests.TryBuildPreviewAsync_ShouldRejectOversizedJsonAtlasBeforeRuntimeParse);
        await RunTestAsync(
            failures,
            nameof(SpineRuntimeSamplingPolicyTests.CreateSampleTimes_ShouldKeepHalfSpeedPlaybackDense_ForLargeOfficialRuntimeMesh),
            SpineRuntimeSamplingPolicyTests.CreateSampleTimes_ShouldKeepHalfSpeedPlaybackDense_ForLargeOfficialRuntimeMesh);
        await RunTestAsync(
            failures,
            nameof(SpineRuntimeSamplingPolicyTests.CreateSampleTimes_ShouldKeepLongHalfSpeedAnimationDense_ForLargeOfficialRuntimeMesh),
            SpineRuntimeSamplingPolicyTests.CreateSampleTimes_ShouldKeepLongHalfSpeedAnimationDense_ForLargeOfficialRuntimeMesh);
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
            nameof(ExternalProcessAssetBundleDecryptorTests.RegistryAndCatalog_ShouldExposeBuiltInArknightsCodec_AsReadOnlyEncryptor),
            ExternalProcessAssetBundleDecryptorTests.RegistryAndCatalog_ShouldExposeBuiltInArknightsCodec_AsReadOnlyEncryptor);
        await RunTestAsync(
            failures,
            nameof(ExternalProcessAssetBundleDecryptorTests.RegistryAndCatalog_ShouldExposeBuiltInCrossCoreCodec_AsReadOnlyEncryptor),
            ExternalProcessAssetBundleDecryptorTests.RegistryAndCatalog_ShouldExposeBuiltInCrossCoreCodec_AsReadOnlyEncryptor);
        await RunTestAsync(
            failures,
            nameof(ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldUseBundleScopedWorkspaceStagingPath_WhenWorkspaceDataRootIsProvided),
            ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldUseBundleScopedWorkspaceStagingPath_WhenWorkspaceDataRootIsProvided);
        await RunTestAsync(
            failures,
            nameof(ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldAppendUniqueSegments_WhenCustomWorkingDirectoryIsProvided),
            ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldAppendUniqueSegments_WhenCustomWorkingDirectoryIsProvided);
        await RunTestAsync(
            failures,
            nameof(ExternalProcessAssetBundleDecryptorTests.EncryptAsync_ShouldRunOptionalEncryptCommand_AndReturnRequestedOutput),
            ExternalProcessAssetBundleDecryptorTests.EncryptAsync_ShouldRunOptionalEncryptCommand_AndReturnRequestedOutput);
        await RunTestAsync(
            failures,
            nameof(ArknightsBundleCodecTests.EncryptAndDecrypt_ShouldRoundTripSyntheticUnityFsBundle),
            ArknightsBundleCodecTests.EncryptAndDecrypt_ShouldRoundTripSyntheticUnityFsBundle);
        await RunTestAsync(
            failures,
            nameof(ArknightsBundleCodecTests.ProbeAndRoundTrip_ShouldHandleRealLz4AkSample_WhenAvailable),
            ArknightsBundleCodecTests.ProbeAndRoundTrip_ShouldHandleRealLz4AkSample_WhenAvailable);
        await RunTestAsync(
            failures,
            nameof(CrossCoreBundleCodecTests.EncryptAndDecrypt_ShouldRoundTripSyntheticUnityFsBundle),
            CrossCoreBundleCodecTests.EncryptAndDecrypt_ShouldRoundTripSyntheticUnityFsBundle);
        await RunTestAsync(
            failures,
            nameof(CrossCoreBundleCodecTests.ProbeAndDecrypt_ShouldHandleNonDuplicatedOuterHeader),
            CrossCoreBundleCodecTests.ProbeAndDecrypt_ShouldHandleNonDuplicatedOuterHeader);
        await RunTestAsync(
            failures,
            nameof(CrossCoreBundleCodecTests.ProbeAndRoundTrip_ShouldHandleRealSample_WhenAvailable),
            CrossCoreBundleCodecTests.ProbeAndRoundTrip_ShouldHandleRealSample_WhenAvailable);
        await RunTestAsync(
            failures,
            nameof(CrossCoreBundleCodecTests.ProbeAndRoundTrip_ShouldHandlePrefixedHeaderRealSample_WhenAvailable),
            CrossCoreBundleCodecTests.ProbeAndRoundTrip_ShouldHandlePrefixedHeaderRealSample_WhenAvailable);
        await RunTestAsync(
            failures,
            nameof(FileSystemResourceProcessorCatalogStoreTests.Load_ShouldIncludeBuiltInSlots_AndUserManagedProcessors),
            FileSystemResourceProcessorCatalogStoreTests.Load_ShouldIncludeBuiltInSlots_AndUserManagedProcessors);
        await RunTestAsync(
            failures,
            nameof(FileSystemResourceProcessorCatalogStoreTests.DeleteUserManagedProcessor_ShouldRemoveManifest),
            FileSystemResourceProcessorCatalogStoreTests.DeleteUserManagedProcessor_ShouldRemoveManifest);
        await RunTestAsync(
            failures,
            nameof(FileSystemPostExportPluginTests.Catalog_ShouldRoundTripAndDeleteUserManagedPlugin),
            FileSystemPostExportPluginTests.Catalog_ShouldRoundTripAndDeleteUserManagedPlugin);
        await RunTestAsync(
            failures,
            nameof(FileSystemPostExportPluginTests.Launcher_ShouldReportProcessStarted_WithoutOwningPluginCompletion),
            FileSystemPostExportPluginTests.Launcher_ShouldReportProcessStarted_WithoutOwningPluginCompletion);
        await RunTestAsync(
            failures,
            nameof(FileSystemPostExportPluginTests.Launcher_ShouldFailBeforeProcessStart_WhenManifestIsMissing),
            FileSystemPostExportPluginTests.Launcher_ShouldFailBeforeProcessStart_WhenManifestIsMissing);
        await RunTestAsync(
            failures,
            nameof(FileSystemResourceProcessorExecutorTests.ExecuteAsync_ShouldRunCli_AndParseResultJson),
            FileSystemResourceProcessorExecutorTests.ExecuteAsync_ShouldRunCli_AndParseResultJson);
        await RunTestAsync(
            failures,
            nameof(FileSystemResourceProcessorExecutorTests.ExecuteAsync_ShouldRejectOutputPathOutsideStaging),
            FileSystemResourceProcessorExecutorTests.ExecuteAsync_ShouldRejectOutputPathOutsideStaging);
        await RunTestAsync(
            failures,
            nameof(FileSystemResourceProcessorExecutorTests.ExecuteAsync_ShouldRunBuiltInFmodWrapper_AndProduceFsbResult),
            FileSystemResourceProcessorExecutorTests.ExecuteAsync_ShouldRunBuiltInFmodWrapper_AndProduceFsbResult);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRewriteAudioClipResource_WhenReplacementMatchesOriginalContainer),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRewriteAudioClipResource_WhenReplacementMatchesOriginalContainer);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRejectAudioClipResource_WhenReplacementContainerMismatchesOriginal),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldRejectAudioClipResource_WhenReplacementContainerMismatchesOriginal);
        await RunTestAsync(
            failures,
            nameof(FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldForceRewriteAudioClipResource_WhenReplacementContainerMismatchesOriginal),
            FileSystemWorkspaceBundleExporterTests.ExportAsync_ShouldForceRewriteAudioClipResource_WhenReplacementContainerMismatchesOriginal);
        await RunTestAsync(
            failures,
            nameof(OnlineComponentInstallationServiceTests.InstallAsync_ShouldVerifyAndInstallCompleteArchive),
            OnlineComponentInstallationServiceTests.InstallAsync_ShouldVerifyAndInstallCompleteArchive);
        await RunTestAsync(
            failures,
            nameof(OnlineComponentInstallationServiceTests.InstallAsync_ShouldRejectDigestMismatch_WithoutInstallingFiles),
            OnlineComponentInstallationServiceTests.InstallAsync_ShouldRejectDigestMismatch_WithoutInstallingFiles);
        await RunTestAsync(
            failures,
            nameof(OnlineComponentInstallationServiceTests.InstallAsync_ShouldRejectPathTraversal_WithoutInstallingFiles),
            OnlineComponentInstallationServiceTests.InstallAsync_ShouldRejectPathTraversal_WithoutInstallingFiles);
        await RunTestAsync(
            failures,
            nameof(AudioPreviewOnlineComponentMapperTests.AttachRequiredComponent_ShouldRequestVgmstream_WhenBackendIsUnavailable),
            AudioPreviewOnlineComponentMapperTests.AttachRequiredComponent_ShouldRequestVgmstream_WhenBackendIsUnavailable);
        await RunTestAsync(
            failures,
            nameof(GitHubReleaseOnlineComponentPackageSourceTests.ResolveAsync_ShouldReadTrustedAssetAndSha256Digest),
            GitHubReleaseOnlineComponentPackageSourceTests.ResolveAsync_ShouldReadTrustedAssetAndSha256Digest);
        await RunTestAsync(
            failures,
            nameof(GitHubReleaseOnlineComponentPackageSourceTests.ResolveAsync_ShouldRejectNonHexadecimalDigest),
            GitHubReleaseOnlineComponentPackageSourceTests.ResolveAsync_ShouldRejectNonHexadecimalDigest);

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
