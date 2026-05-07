using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

internal static class JsonWorkspaceChangeStoreTests
{
    public static void Save_ShouldRoundTripEditDraftDescriptors()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceChangeStore(temporaryDirectory.DirectoryPath);
        var updatedAtUtc = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        var expiresAtUtc = updatedAtUtc.AddDays(2);
        var changeSet = new WorkspaceChangeSetDto(
            WorkspaceKey: "workspace-a",
            UpdatedAtUtc: updatedAtUtc,
            Changes:
            [
                new WorkspaceChangeRecordDto(
                    ChangeKey: "replace:bundle-a:asset-1",
                    ChangeKind: "replacement",
                    BundleId: "bundle-a",
                    AssetId: "asset-1",
                    Summary: "Replacement summary",
                    RecordedAtUtc: updatedAtUtc,
                    SourceDescriptor: "source",
                    OutputPath: null)
            ],
            EditDrafts:
            [
                new WorkspaceAssetEditDraftDescriptorDto(
                    DraftId: "draft-1",
                    DraftMode: "text",
                    SourceFingerprint: "source-v1",
                    UpdatedAtUtc: updatedAtUtc,
                    ExpiresAtUtc: expiresAtUtc,
                    SourceDescription: "Text draft",
                    Summary: "Draft summary")
            ]);

        store.Save(changeSet);
        var loadedChangeSet = store.Load("workspace-a");

        var restoredChangeSet = TestAssert.NotNull(loadedChangeSet, "Saved change set should be readable.");
        TestAssert.Equal("workspace-a", restoredChangeSet.WorkspaceKey, "Workspace key should round-trip.");
        TestAssert.Equal(updatedAtUtc, restoredChangeSet.UpdatedAtUtc, "Updated time should round-trip.");
        TestAssert.Equal("replace:bundle-a:asset-1", TestAssert.Single(restoredChangeSet.Changes).ChangeKey, "Change record should round-trip.");

        var draft = TestAssert.Single(restoredChangeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>());
        TestAssert.Equal("draft-1", draft.DraftId, "Draft id should round-trip.");
        TestAssert.Equal("text", draft.DraftMode, "Draft mode should round-trip.");
        TestAssert.Equal("source-v1", draft.SourceFingerprint, "Source fingerprint should round-trip.");
        TestAssert.Equal(updatedAtUtc, draft.UpdatedAtUtc, "Draft updated time should round-trip.");
        TestAssert.Equal(expiresAtUtc, draft.ExpiresAtUtc, "Draft expiration should round-trip.");
        TestAssert.Equal("Text draft", draft.SourceDescription, "Draft source description should round-trip.");
        TestAssert.Equal("Draft summary", draft.Summary, "Draft summary should round-trip.");
    }
}
