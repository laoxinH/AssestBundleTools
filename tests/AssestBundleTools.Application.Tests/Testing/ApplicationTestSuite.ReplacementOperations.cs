using AssestBundleTools.Application.Tests.Replacement;

namespace AssestBundleTools.Application.Tests.Testing;

internal static partial class ApplicationTestSuite
{
    private static async Task RunReplacementOperationRecorderTestsAsync(List<string> failures)
    {
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.ReplaceAssetFromFileHandler_ShouldAppendOperationRecord_WhenWorkspaceStagingIsAvailable),
            ReplacementOperationRecorderTests.ReplaceAssetFromFileHandler_ShouldAppendOperationRecord_WhenWorkspaceStagingIsAvailable);
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.ReplaceAssetFromWorkspaceHandler_ShouldSupersedeLatestActiveReplacementRecord),
            ReplacementOperationRecorderTests.ReplaceAssetFromWorkspaceHandler_ShouldSupersedeLatestActiveReplacementRecord);
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.UndoAssetReplacementHandler_ShouldAppendUndoOperation_ForLatestActiveReplacementRecord),
            ReplacementOperationRecorderTests.UndoAssetReplacementHandler_ShouldAppendUndoOperation_ForLatestActiveReplacementRecord);
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.RedoAssetReplacementHandler_ShouldAppendRedoOperation_ForLatestUndoRecord),
            ReplacementOperationRecorderTests.RedoAssetReplacementHandler_ShouldAppendRedoOperation_ForLatestUndoRecord);
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.RedoAssetReplacementHandler_ShouldReapplySelectedReplacementRecord),
            ReplacementOperationRecorderTests.RedoAssetReplacementHandler_ShouldReapplySelectedReplacementRecord);
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.UndoAssetReplacementHandler_ShouldResolveLinkedReplacementTarget_FromRelatedAssetContext),
            ReplacementOperationRecorderTests.UndoAssetReplacementHandler_ShouldResolveLinkedReplacementTarget_FromRelatedAssetContext);
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.RedoAssetReplacementHandler_ShouldResolveLinkedReplacementTarget_FromRelatedAssetContext),
            ReplacementOperationRecorderTests.RedoAssetReplacementHandler_ShouldResolveLinkedReplacementTarget_FromRelatedAssetContext);
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.WorkspaceReplacementReadModelService_ShouldMaterializeEffectiveReplacement_ForRedoStack),
            ReplacementOperationRecorderTests.WorkspaceReplacementReadModelService_ShouldMaterializeEffectiveReplacement_ForRedoStack);
        await RunTestAsync(
            failures,
            nameof(ReplacementOperationRecorderTests.WorkspaceReplacementReadModelService_ShouldMaterializeSelectedRedoSource),
            ReplacementOperationRecorderTests.WorkspaceReplacementReadModelService_ShouldMaterializeSelectedRedoSource);
    }
}
