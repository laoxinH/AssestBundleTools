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

    public static void Save_ShouldRoundTripOperationRecords_AsStringEnums()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceChangeStore(temporaryDirectory.DirectoryPath);
        var updatedAtUtc = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        var changeSet = new WorkspaceChangeSetDto(
            WorkspaceKey: "workspace-a",
            UpdatedAtUtc: updatedAtUtc,
            Changes: Array.Empty<WorkspaceChangeRecordDto>(),
            EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
            SchemaVersion: WorkspaceChangeSetDto.CurrentSchemaVersion,
            OperationSequence: 12,
            Operations:
            [
                new WorkspaceOperationRecordDto(
                    OperationId: "operation-12",
                    Sequence: 12,
                    OperationKind: WorkspaceOperationKind.ReplaceFromFile,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: "workspace-a",
                    Target: new WorkspaceOperationTargetDto(
                        BundleId: "bundle-a",
                        AssetId: "asset-1",
                        AssetName: "Texture_A",
                        AssetType: "Texture2D",
                        PathId: 1001),
                    Source: new WorkspaceOperationSourceDto(
                        SourceKind: "file",
                        SourceFilePath: @"C:\Source\Texture_A.png",
                        SourceFileFingerprint: "file-v1"),
                    PreviousState: new WorkspaceOperationStateDto(WasModified: false),
                    Result: new WorkspaceOperationResultDto(ReplacementSourceKind: "file"),
                    CreatedAtUtc: updatedAtUtc,
                    AppliedAtUtc: updatedAtUtc,
                    CompletedAtUtc: updatedAtUtc,
                    Diagnostics:
                    [
                        new WorkspaceOperationDiagnosticDto(
                            Code: "Workspace.Replace.Applied",
                            Severity: "Info",
                            MessageToken: "Replace.Applied")
                    ])
            ]);

        store.Save(changeSet);
        var jsonPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "changes",
            "workspace-a.json");
        var json = File.ReadAllText(jsonPath);
        var loadedChangeSet = store.Load("workspace-a");

        TestAssert.True(json.Contains("\"operationKind\": \"ReplaceFromFile\"", StringComparison.Ordinal), "Operation kind should be stored as a stable string.");
        TestAssert.True(json.Contains("\"status\": \"Applied\"", StringComparison.Ordinal), "Operation status should be stored as a stable string.");

        var restoredChangeSet = TestAssert.NotNull(loadedChangeSet, "Saved change set should be readable.");
        TestAssert.Equal(WorkspaceChangeSetDto.CurrentSchemaVersion, restoredChangeSet.SchemaVersion, "Schema version should round-trip.");
        TestAssert.Equal(12, restoredChangeSet.OperationSequence, "Operation sequence should round-trip.");

        var operation = TestAssert.Single(restoredChangeSet.Operations ?? Array.Empty<WorkspaceOperationRecordDto>());
        TestAssert.Equal("operation-12", operation.OperationId, "Operation id should round-trip.");
        TestAssert.Equal(12, operation.Sequence, "Operation sequence should round-trip.");
        TestAssert.Equal(WorkspaceOperationKind.ReplaceFromFile, operation.OperationKind, "Operation kind should round-trip.");
        TestAssert.Equal(WorkspaceOperationStatus.Applied, operation.Status, "Operation status should round-trip.");
        TestAssert.Equal("bundle-a", operation.Target?.BundleId, "Operation target should round-trip.");
        TestAssert.Equal(@"C:\Source\Texture_A.png", operation.Source?.SourceFilePath, "Operation source should round-trip.");
        TestAssert.Equal(false, operation.PreviousState?.WasModified, "Operation previous state should round-trip.");
        TestAssert.Equal("file", operation.Result?.ReplacementSourceKind, "Operation result should round-trip.");

        var diagnostic = TestAssert.Single(operation.Diagnostics ?? Array.Empty<WorkspaceOperationDiagnosticDto>());
        TestAssert.Equal("Workspace.Replace.Applied", diagnostic.Code, "Operation diagnostic should round-trip.");
    }

    public static void Load_ShouldReadLegacyChangeSetWithoutOperationFields()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceChangeStore(temporaryDirectory.DirectoryPath);
        var changeDirectoryPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "changes");
        Directory.CreateDirectory(changeDirectoryPath);
        File.WriteAllText(
            Path.Combine(changeDirectoryPath, "workspace-legacy.json"),
            """
            {
              "workspaceKey": "workspace-legacy",
              "updatedAtUtc": "2026-05-10T12:00:00+00:00",
              "changes": [
                {
                  "changeKey": "extract:bundle-a:asset-1",
                  "changeKind": "extraction",
                  "bundleId": "bundle-a",
                  "assetId": "asset-1",
                  "summary": "Legacy change",
                  "recordedAtUtc": "2026-05-10T12:00:00+00:00",
                  "sourceDescriptor": null,
                  "outputPath": "Exports/asset-1.png"
                }
              ],
              "editDrafts": []
            }
            """);

        var loadedChangeSet = store.Load("workspace-legacy");

        var restoredChangeSet = TestAssert.NotNull(loadedChangeSet, "Legacy change set should be readable.");
        TestAssert.Equal("workspace-legacy", restoredChangeSet.WorkspaceKey, "Legacy workspace key should load.");
        TestAssert.Equal(WorkspaceChangeSetDto.CurrentSchemaVersion, restoredChangeSet.SchemaVersion, "Legacy change set should use the DTO default schema version.");
        TestAssert.Equal(0, restoredChangeSet.OperationSequence, "Legacy operation sequence should default to zero.");
        TestAssert.Equal(0, (restoredChangeSet.Operations ?? Array.Empty<WorkspaceOperationRecordDto>()).Count, "Legacy operations should default to empty.");
        TestAssert.Equal("extract:bundle-a:asset-1", TestAssert.Single(restoredChangeSet.Changes).ChangeKey, "Legacy changes should still load.");
    }

    public static void Save_ShouldReplaceChangeSet_WhenExistingFileHasSharedReadLease()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceChangeStore(temporaryDirectory.DirectoryPath);
        var initialChangeSet = CreateMinimalChangeSet(
            workspaceKey: "workspace-a",
            updatedAtUtc: new DateTimeOffset(2026, 6, 7, 10, 0, 0, TimeSpan.Zero));
        store.Save(initialChangeSet);
        var changeFilePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "changes",
            "workspace-a.json");

        using var readLease = File.Open(
            changeFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var updatedAtUtc = new DateTimeOffset(2026, 6, 7, 10, 1, 0, TimeSpan.Zero);
        var updatedChangeSet = CreateMinimalChangeSet(
            workspaceKey: "workspace-a",
            updatedAtUtc: updatedAtUtc);

        store.Save(updatedChangeSet);
        var loadedChangeSet = store.Load("workspace-a");

        var restoredChangeSet = TestAssert.NotNull(loadedChangeSet, "Updated change set should be readable while an old read lease is open.");
        TestAssert.Equal(updatedAtUtc, restoredChangeSet.UpdatedAtUtc, "Atomic replace should publish the updated change set.");
    }

    private static WorkspaceChangeSetDto CreateMinimalChangeSet(
        string workspaceKey,
        DateTimeOffset updatedAtUtc) =>
        new(
            WorkspaceKey: workspaceKey,
            UpdatedAtUtc: updatedAtUtc,
            Changes: Array.Empty<WorkspaceChangeRecordDto>(),
            EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
            SchemaVersion: WorkspaceChangeSetDto.CurrentSchemaVersion,
            Operations: Array.Empty<WorkspaceOperationRecordDto>());
}
