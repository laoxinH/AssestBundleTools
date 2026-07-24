using AssestBundleTools.Application.Tests.Workspace.Commands;
using AssestBundleTools.Application.Tests.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Testing;

internal static partial class ApplicationTestSuite
{
    private static async Task RunWorkspaceOperationLogTestsAsync(List<string> failures)
    {
        await RunTestAsync(
            failures,
            nameof(WorkspaceOperationLogServiceTests.AppendOperation_ShouldAssignSequence_AndPreserveExistingChangeSetState),
            WorkspaceOperationLogServiceTests.AppendOperation_ShouldAssignSequence_AndPreserveExistingChangeSetState);
        await RunTestAsync(
            failures,
            nameof(WorkspaceOperationLogServiceTests.UpdateOperationStatus_ShouldUpdateStatusTimestamps_AndDiagnostics),
            WorkspaceOperationLogServiceTests.UpdateOperationStatus_ShouldUpdateStatusTimestamps_AndDiagnostics);
        await RunTestAsync(
            failures,
            nameof(WorkspaceOperationLogServiceTests.GetActiveOperations_ShouldExcludeAppliedUndoTargets),
            WorkspaceOperationLogServiceTests.GetActiveOperations_ShouldExcludeAppliedUndoTargets);
        await RunTestAsync(
            failures,
            nameof(WorkspaceOperationLogServiceTests.DeleteOperationRecord_ShouldRemoveRecord_AndPreserveSequenceWatermark),
            WorkspaceOperationLogServiceTests.DeleteOperationRecord_ShouldRemoveRecord_AndPreserveSequenceWatermark);
        await RunTestAsync(
            failures,
            nameof(WorkspaceOperationLogServiceTests.AppendOperation_ShouldRespectRecordLimit_AndProtectCurrentReplacementChain),
            WorkspaceOperationLogServiceTests.AppendOperation_ShouldRespectRecordLimit_AndProtectCurrentReplacementChain);
        await RunTestAsync(
            failures,
            nameof(WorkspaceOperationLogServiceTests.CreateRecoveryPlan_ShouldReportPendingOperationsAsInterrupted),
            WorkspaceOperationLogServiceTests.CreateRecoveryPlan_ShouldReportPendingOperationsAsInterrupted);
        await RunTestAsync(
            failures,
            nameof(WorkspaceOperationRelationMatcherTests.OperationConcernsTarget_ShouldMatchRelatedSourceFingerprint),
            WorkspaceOperationRelationMatcherTests.OperationConcernsTarget_ShouldMatchRelatedSourceFingerprint);
        await RunTestAsync(
            failures,
            nameof(DeleteWorkspaceOperationRecordHandlerTests.HandleAsync_ShouldDeleteMultipleRecordsAtomically_AndMergeAffectedTargets),
            DeleteWorkspaceOperationRecordHandlerTests.HandleAsync_ShouldDeleteMultipleRecordsAtomically_AndMergeAffectedTargets);
        await RunTestAsync(
            failures,
            nameof(DeleteWorkspaceOperationRecordHandlerTests.HandleAsync_ShouldRejectEntireBatch_WhenAnyRecordIsProtected),
            DeleteWorkspaceOperationRecordHandlerTests.HandleAsync_ShouldRejectEntireBatch_WhenAnyRecordIsProtected);
        await RunTestAsync(
            failures,
            nameof(DeleteWorkspaceOperationRecordHandlerTests.HandleAsync_ShouldRejectExtractionOperationDeletion),
            DeleteWorkspaceOperationRecordHandlerTests.HandleAsync_ShouldRejectExtractionOperationDeletion);
        await RunTestAsync(
            failures,
            nameof(DeleteWorkspaceOperationRecordHandlerTests.HandleAsync_ShouldRejectBatchExtractionOperationDeletion),
            DeleteWorkspaceOperationRecordHandlerTests.HandleAsync_ShouldRejectBatchExtractionOperationDeletion);
    }
}
