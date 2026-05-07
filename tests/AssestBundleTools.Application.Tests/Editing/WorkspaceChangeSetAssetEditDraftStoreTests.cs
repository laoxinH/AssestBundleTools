using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Tests.TestDoubles;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Editing;

internal static class WorkspaceChangeSetAssetEditDraftStoreTests
{
    public static Task Load_ShouldResolveDraftDescriptor_FromWorkspaceChangeSet()
    {
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData\A";
        var updatedAtUtc = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        var expiresAtUtc = updatedAtUtc.AddDays(2);
        var catalogStore = new TestWorkspaceCatalogStore(new WorkspaceCatalogSnapshotDto(
            Version: 1,
            LastActiveWorkspaceKey: workspaceKey,
            RecentWorkspaces:
            [
                new WorkspaceCatalogEntryDto(
                    WorkspaceKey: workspaceKey,
                    DisplayName: "Workspace A",
                    SourcePath: @"C:\Bundles\A",
                    WorkspaceDataRootPath: workspaceDataRootPath,
                    WorkspaceMode: WorkspaceMode.SinglePane,
                    LastActivatedAtUtc: updatedAtUtc,
                    IsPinned: false,
                    LastKnownState: null)
            ]));
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: workspaceKey,
                UpdatedAtUtc: updatedAtUtc,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        DraftId: " draft-1 ",
                        DraftMode: " text ",
                        SourceFingerprint: " source-v1 ",
                        UpdatedAtUtc: updatedAtUtc,
                        ExpiresAtUtc: expiresAtUtc,
                        SourceDescription: " Text draft ",
                        Summary: " Draft summary ")
                ])
        };
        var dataStoreFactory = new TestWorkspaceDataStoreFactory(changeStore);
        var store = new WorkspaceChangeSetAssetEditDraftStore(catalogStore, dataStoreFactory);

        var descriptor = store.Load(new AssetEditDraftLookupKey(
            WorkspaceKey: " workspace-a ",
            DraftId: " draft-1 ",
            DraftMode: " text "));

        var resolvedDescriptor = descriptor ?? throw new InvalidOperationException("Expected descriptor.");
        TestAssert.Equal(workspaceDataRootPath, dataStoreFactory.LastWorkspaceChangeStoreRootPath, "Store should use the catalog data root.");
        TestAssert.Equal(workspaceKey, changeStore.LastLoadedWorkspaceKey, "Store should load the normalized workspace key.");
        TestAssert.Equal(workspaceKey, resolvedDescriptor.WorkspaceKey, "Descriptor workspace key should be normalized.");
        TestAssert.Equal("draft-1", resolvedDescriptor.DraftId, "Draft id should be normalized.");
        TestAssert.Equal("text", resolvedDescriptor.DraftMode, "Draft mode should be normalized.");
        TestAssert.Equal("source-v1", resolvedDescriptor.SourceFingerprint, "Source fingerprint should be normalized.");
        TestAssert.Equal(updatedAtUtc, resolvedDescriptor.UpdatedAtUtc, "Updated timestamp should be preserved.");
        TestAssert.Equal(expiresAtUtc, resolvedDescriptor.ExpiresAtUtc, "Expiration timestamp should be preserved.");
        TestAssert.Equal("Text draft", resolvedDescriptor.SourceDescription, "Source description should be normalized.");
        TestAssert.Equal("Draft summary", resolvedDescriptor.Summary, "Summary should be normalized.");
        return Task.CompletedTask;
    }

    public static Task Load_ShouldReturnNull_WhenChangeSetWorkspaceDoesNotMatchLookup()
    {
        var catalogStore = new TestWorkspaceCatalogStore(new WorkspaceCatalogSnapshotDto(
            Version: 1,
            LastActiveWorkspaceKey: "workspace-a",
            RecentWorkspaces:
            [
                new WorkspaceCatalogEntryDto(
                    WorkspaceKey: "workspace-a",
                    DisplayName: "Workspace A",
                    SourcePath: @"C:\Bundles\A",
                    WorkspaceDataRootPath: @"D:\WorkspaceData\A",
                    WorkspaceMode: WorkspaceMode.SinglePane,
                    LastActivatedAtUtc: DateTimeOffset.UtcNow,
                    IsPinned: false,
                    LastKnownState: null)
            ]));
        var dataStoreFactory = new TestWorkspaceDataStoreFactory(new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: "workspace-b",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        DraftId: "draft-1",
                        DraftMode: "text",
                        SourceFingerprint: "source-v1",
                        UpdatedAtUtc: DateTimeOffset.UtcNow)
                ])
        });
        var store = new WorkspaceChangeSetAssetEditDraftStore(catalogStore, dataStoreFactory);

        var descriptor = store.Load(new AssetEditDraftLookupKey("workspace-a", "draft-1", "text"));

        if (descriptor is not null)
        {
            throw new InvalidOperationException("Mismatched workspace change set should not return a descriptor.");
        }

        return Task.CompletedTask;
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IWorkspaceChangeStore _workspaceChangeStore;

        public TestWorkspaceDataStoreFactory(IWorkspaceChangeStore workspaceChangeStore)
        {
            _workspaceChangeStore = workspaceChangeStore;
        }

        public string? LastWorkspaceChangeStoreRootPath { get; private set; }

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null)
        {
            LastWorkspaceChangeStoreRootPath = rootDirectoryPath;
            return _workspaceChangeStore;
        }

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        public WorkspaceChangeSetDto? ChangeSet { get; init; }

        public string? LastLoadedWorkspaceKey { get; private set; }

        public WorkspaceChangeSetDto? Load(string workspaceKey)
        {
            LastLoadedWorkspaceKey = workspaceKey;
            return ChangeSet;
        }

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            throw new NotSupportedException();
        }

        public void Delete(string workspaceKey)
        {
            throw new NotSupportedException();
        }
    }
}
