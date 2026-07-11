using AssestBundleTools.Application.Tests.Workspace.Commands;
using AssestBundleTools.Application.Tests.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Testing;

internal static partial class ApplicationTestSuite
{
    private static async Task RunWorkspaceImportTestsAsync(List<string> failures)
    {
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
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldSelectCodecPerBundle_AndRecordProbeTelemetry),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldSelectCodecPerBundle_AndRecordProbeTelemetry);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldKeepAutoDetection_WhenProbeExecutionFails),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldKeepAutoDetection_WhenProbeExecutionFails);
        await RunTestAsync(
            failures,
            nameof(ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldProbeEverySource_WhenDecryptorIsExplicitlyConfigured),
            ProcessWorkspaceImportsHandlerTests.HandleAsync_ShouldProbeEverySource_WhenDecryptorIsExplicitlyConfigured);
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
    }
}
