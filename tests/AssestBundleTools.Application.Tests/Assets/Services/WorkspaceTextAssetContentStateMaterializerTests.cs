using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Services;

public static class WorkspaceTextAssetContentStateMaterializerTests
{
    public static Task Materialize_ShouldSelectLatestReplacementOverOlderTextEdit()
    {
        var changeSet = CreateChangeSet(
            CreateTextEdit("edit-1", revision: 1, sequence: 1),
            CreateFileReplacement("replace-1", sequence: 2));
        var materializer = new WorkspaceTextAssetContentStateMaterializer();

        var content = materializer.FindEffectiveContent(changeSet, "bundle-text", "asset-atlas");

        TestAssert.NotNull(content, "TextAsset content should resolve from the operation stream.");
        TestAssert.Equal(WorkspaceTextAssetContentKind.FileReplacement, content!.Kind, "A later replacement should become the current text content source.");
        TestAssert.Equal("replace-1", content.OperationId, "The replacement operation should be the effective content record.");
        return Task.CompletedTask;
    }

    public static Task Materialize_ShouldSelectLatestTextEditOverOlderReplacement()
    {
        var changeSet = CreateChangeSet(
            CreateTextEdit("edit-1", revision: 1, sequence: 1),
            CreateFileReplacement("replace-1", sequence: 2),
            CreateTextEdit("edit-2", revision: 2, sequence: 3));
        var materializer = new WorkspaceTextAssetContentStateMaterializer();

        var content = materializer.FindEffectiveContent(changeSet, "bundle-text", "asset-atlas");

        TestAssert.NotNull(content, "TextAsset content should resolve from the operation stream.");
        TestAssert.Equal(WorkspaceTextAssetContentKind.TextEdit, content!.Kind, "A later text edit should become the current text content source.");
        TestAssert.Equal("edit-2", content.OperationId, "The newer edit operation should be effective.");
        TestAssert.Equal(2, content.DraftRevision, "The newer edit revision should be pinned.");
        return Task.CompletedTask;
    }

    public static Task Materialize_ShouldRestorePreviousTextEdit_WhenLaterReplacementIsUndone()
    {
        var changeSet = CreateChangeSet(
            CreateTextEdit("edit-1", revision: 1, sequence: 1),
            CreateFileReplacement("replace-1", sequence: 2),
            CreateUndoReplacement("undo-replace-1", "replace-1", sequence: 3));
        var materializer = new WorkspaceTextAssetContentStateMaterializer();

        var content = materializer.FindEffectiveContent(changeSet, "bundle-text", "asset-atlas");

        TestAssert.NotNull(content, "Undoing the replacement should reveal the previous text content source.");
        TestAssert.Equal(WorkspaceTextAssetContentKind.TextEdit, content!.Kind, "The previous text edit should become current again.");
        TestAssert.Equal("edit-1", content.OperationId, "The previous edit operation should be effective after undo.");
        TestAssert.Equal(1, content.DraftRevision, "The previous edit revision should be restored.");
        return Task.CompletedTask;
    }

    private static WorkspaceChangeSetDto CreateChangeSet(params WorkspaceOperationRecordDto[] operations) =>
        new(
            WorkspaceKey: "workspace-key",
            UpdatedAtUtc: DateTimeOffset.UnixEpoch,
            Changes: [],
            Operations: operations,
            OperationSequence: operations.Length == 0 ? 0 : operations.Max(static operation => operation.Sequence));

    private static WorkspaceOperationRecordDto CreateTextEdit(
        string operationId,
        int revision,
        long sequence) =>
        new(
            OperationId: operationId,
            Sequence: sequence,
            OperationKind: WorkspaceOperationKind.EditDraftApplied,
            Status: WorkspaceOperationStatus.Applied,
            WorkspaceKey: "workspace-key",
            Target: CreateTarget(),
            Source: new WorkspaceOperationSourceDto(
                SourceKind: "draft",
                DraftId: "draft-atlas",
                DraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode),
            PreviousState: null,
            Result: new WorkspaceOperationResultDto(
                DraftId: "draft-atlas",
                DraftRevision: revision),
            CreatedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            AppliedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            CompletedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence));

    private static WorkspaceOperationRecordDto CreateFileReplacement(
        string operationId,
        long sequence) =>
        new(
            OperationId: operationId,
            Sequence: sequence,
            OperationKind: WorkspaceOperationKind.ReplaceFromFile,
            Status: WorkspaceOperationStatus.Applied,
            WorkspaceKey: "workspace-key",
            Target: CreateTarget(),
            Source: new WorkspaceOperationSourceDto(
                SourceKind: "file",
                SourceFilePath: @"C:\Imports\hero.atlas",
                SourceFileName: "hero.atlas",
                SourceAssetType: AssetType.TextAsset.ToString()),
            PreviousState: null,
            Result: new WorkspaceOperationResultDto(ReplacementSourceKind: "file"),
            CreatedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            AppliedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            CompletedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence));

    private static WorkspaceOperationRecordDto CreateUndoReplacement(
        string operationId,
        string supersedesOperationId,
        long sequence) =>
        new(
            OperationId: operationId,
            Sequence: sequence,
            OperationKind: WorkspaceOperationKind.UndoReplacement,
            Status: WorkspaceOperationStatus.Applied,
            WorkspaceKey: "workspace-key",
            Target: CreateTarget(),
            Source: null,
            PreviousState: null,
            Result: null,
            CreatedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            AppliedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            CompletedAtUtc: DateTimeOffset.UnixEpoch.AddSeconds(sequence),
            SupersedesOperationId: supersedesOperationId);

    private static WorkspaceOperationTargetDto CreateTarget() =>
        new(
            BundleId: "bundle-text",
            AssetId: "asset-atlas",
            AssetName: "hero.atlas",
            AssetType: AssetType.TextAsset.ToString(),
            AssetTypeDisplayName: AssetType.TextAsset.ToString(),
            SerializedFileName: "characters.assets",
            PathId: 1001);
}
