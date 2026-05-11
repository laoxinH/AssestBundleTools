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
            nameof(WorkspaceOperationLogServiceTests.CreateRecoveryPlan_ShouldReportPendingOperationsAsInterrupted),
            WorkspaceOperationLogServiceTests.CreateRecoveryPlan_ShouldReportPendingOperationsAsInterrupted);
    }
}
