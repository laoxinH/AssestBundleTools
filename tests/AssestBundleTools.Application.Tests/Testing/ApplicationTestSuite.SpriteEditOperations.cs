using AssestBundleTools.Application.Tests.Editing;

namespace AssestBundleTools.Application.Tests.Testing;

internal static partial class ApplicationTestSuite
{
    private static async Task RunSpriteEditOperationTestsAsync(List<string> failures)
    {
        await RunTestAsync(
            failures,
            nameof(SpriteEditOperationRecorderTests.ApplySpriteEditHandler_ShouldAppendOperationRecord_AndDraftDescriptor),
            SpriteEditOperationRecorderTests.ApplySpriteEditHandler_ShouldAppendOperationRecord_AndDraftDescriptor);
        await RunTestAsync(
            failures,
            nameof(SpriteEditOperationRecorderTests.ApplySpriteEditHandler_ShouldReturnFailure_WhenGeometryIsInvalid),
            SpriteEditOperationRecorderTests.ApplySpriteEditHandler_ShouldReturnFailure_WhenGeometryIsInvalid);
        await RunTestAsync(
            failures,
            nameof(SpriteEditOperationRecorderTests.ApplySpriteEditHandler_ShouldAppendLinkedTextureReplacement_WhenSpriteSaveIncludesAssociatedTexture),
            SpriteEditOperationRecorderTests.ApplySpriteEditHandler_ShouldAppendLinkedTextureReplacement_WhenSpriteSaveIncludesAssociatedTexture);
    }
}
