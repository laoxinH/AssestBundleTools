using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

internal static class JsonWorkspaceImportRecognitionCacheStoreTests
{
    public static void Save_ShouldRoundTripReplaceDeleteAndDeleteWorkspace()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceImportRecognitionCacheStore(temporaryDirectory.DirectoryPath);
        const string workspaceKey = "workspace-recognition";
        const string bundleCacheKey = "bundle-cache-key";
        var sourcePath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.ab");
        var fingerprint = new WorkspaceFileFingerprintDto(
            sourcePath,
            FileSizeInBytes: 4096,
            LastWriteTimeUtc: new DateTimeOffset(2026, 7, 19, 1, 0, 0, TimeSpan.Zero),
            ContentHash: null);
        var entry = new WorkspaceImportRecognitionCacheEntryDto(
            workspaceKey,
            bundleCacheKey,
            sourcePath,
            "hero.ab",
            fingerprint,
            "recognition-v1",
            "catalog-v1",
            WorkspaceImportRecognitionDisposition.Candidate,
            "builtin.codec",
            "1.0.0",
            ReaderParserVersion: null,
            IssueKind: null,
            DetailMessage: null,
            CachedAtUtc: new DateTimeOffset(2026, 7, 19, 1, 1, 0, TimeSpan.Zero));

        store.Save(entry);

        var loaded = TestAssert.Single(store.LoadAll(workspaceKey));
        TestAssert.Equal(WorkspaceImportRecognitionDisposition.Candidate, loaded.Disposition, "Recognition disposition should round-trip.");
        TestAssert.Equal("builtin.codec", loaded.DecryptorId, "Recognition codec identity should round-trip.");

        store.Save(entry with { Disposition = WorkspaceImportRecognitionDisposition.ScreenedOut, DecryptorId = null });
        loaded = TestAssert.Single(store.LoadAll(workspaceKey));
        TestAssert.Equal(WorkspaceImportRecognitionDisposition.ScreenedOut, loaded.Disposition, "Saving the same bundle key should replace its recognition result.");

        store.Delete(workspaceKey, bundleCacheKey);
        TestAssert.Equal(0, store.LoadAll(workspaceKey).Count, "Deleting a recognition entry should remove it.");

        store.Save(entry);
        store.DeleteWorkspace(workspaceKey);
        TestAssert.Equal(0, store.LoadAll(workspaceKey).Count, "Deleting a workspace should remove all recognition entries.");
    }
}
