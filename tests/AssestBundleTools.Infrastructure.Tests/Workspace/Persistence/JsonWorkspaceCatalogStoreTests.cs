using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

internal static class JsonWorkspaceCatalogStoreTests
{
    public static void Load_ShouldReturnEmptySnapshot_WhenCatalogFileDoesNotExist()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceCatalogStore(temporaryDirectory.DirectoryPath);

        var snapshot = store.Load();

        TestAssert.Equal(WorkspaceCatalogSnapshotDto.Empty.Version, snapshot.Version, "Missing catalog should use the empty snapshot version.");
        TestAssert.Null(snapshot.LastActiveWorkspaceKey, "Missing catalog should not have a last active workspace.");
        TestAssert.Equal(0, snapshot.RecentWorkspaces.Count, "Missing catalog should not restore any entries.");
    }

    public static void Save_ShouldRoundTripCatalogSnapshot()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceCatalogStore(temporaryDirectory.DirectoryPath);
        var expectedActivatedAt = new DateTimeOffset(2026, 4, 28, 12, 34, 56, TimeSpan.Zero);
        var snapshot = new WorkspaceCatalogSnapshotDto(
            Version: 1,
            LastActiveWorkspaceKey: "workspace-a",
            RecentWorkspaces:
            [
                new WorkspaceCatalogEntryDto(
                    WorkspaceKey: "workspace-a",
                    DisplayName: "Example Workspace",
                    SourcePath: @"C:\Example",
                    WorkspaceDataRootPath: @"D:\WorkspaceData",
                    WorkspaceMode: WorkspaceMode.SinglePane,
                    LastActivatedAtUtc: expectedActivatedAt,
                    IsPinned: true,
                    LastKnownState: "bundleCount=3;assetCount=12")
            ]);

        store.Save(snapshot);
        var loadedSnapshot = store.Load();

        TestAssert.Equal(snapshot.Version, loadedSnapshot.Version, "Catalog version should round-trip.");
        TestAssert.Equal(snapshot.LastActiveWorkspaceKey, loadedSnapshot.LastActiveWorkspaceKey, "Last active workspace key should round-trip.");
        var loadedEntry = TestAssert.Single(loadedSnapshot.RecentWorkspaces);
        TestAssert.Equal("workspace-a", loadedEntry.WorkspaceKey, "Workspace key should round-trip.");
        TestAssert.Equal("Example Workspace", loadedEntry.DisplayName, "Display name should round-trip.");
        TestAssert.Equal(@"C:\Example", loadedEntry.SourcePath, "Source path should round-trip.");
        TestAssert.Equal(@"D:\WorkspaceData", loadedEntry.WorkspaceDataRootPath, "Workspace data root should round-trip.");
        TestAssert.Equal(WorkspaceMode.SinglePane, loadedEntry.WorkspaceMode, "Workspace mode should round-trip.");
        TestAssert.Equal(expectedActivatedAt, loadedEntry.LastActivatedAtUtc, "Last activated time should round-trip.");
        TestAssert.True(loadedEntry.IsPinned, "Pinned state should round-trip.");
        TestAssert.Equal("bundleCount=3;assetCount=12", loadedEntry.LastKnownState, "Last known state should round-trip.");
    }
}
