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
    }
}
