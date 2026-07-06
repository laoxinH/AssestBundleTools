using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Editing;

internal static partial class TextAssetEditDraftOpenHandlerTests
{
    public static async Task OpenTextAssetEditDraftHandler_ShouldRejectStaleAppliedBaseline_WhenNoOperationFactRemains()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData\A";
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        var updatedAtUtc = new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero);
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "story.json",
            textEditDraftId: "draft-1",
            textEditDraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
            textEditDraftRevision: 3));
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: workspaceKey,
                UpdatedAtUtc: updatedAtUtc,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        DraftId: "draft-1",
                        DraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
                        SourceFingerprint: "source-v1",
                        UpdatedAtUtc: updatedAtUtc)
                ],
                Operations:
                [
                    CreateTextEditOperation(
                        workspaceKey,
                        bundleId,
                        assetId,
                        "draft-1",
                        WorkspaceOperationKind.EditDraftCreated,
                        draftRevision: null,
                        sequence: 1,
                        updatedAtUtc)
                ])
        };
        var handler = new OpenTextAssetEditDraftHandler(
            registry,
            new TestWorkspaceDataStoreFactory(changeStore, new TestAssetEditDraftTextStore()));

        var result = await handler.HandleAsync(new OpenTextAssetEditDraftCommand(
            workspaceId,
            workspaceKey,
            workspaceDataRootPath,
            bundleId,
            assetId,
            "draft-1",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            SourceFingerprint: null,
            DraftRevision: 3,
            IsAppliedBaseline: true));

        TestAssert.True(result.IsFailure, "Stale applied baseline must not reopen the draft head after all applied text operations are deleted.");
        TestAssert.Equal(
            "Asset.EditDraft.SourceMismatch",
            TestAssert.Single(result.Errors).Code,
            "Stale applied baseline should be rejected so Presentation can create a fresh original-backed draft.");
    }

    public static async Task OpenTextAssetEditDraftHandler_ShouldRejectStaleAppliedBaseline_WhenOperationHistoryHasNoEffectiveEdit()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData\A";
        var bundleId = new BundleId("bundle.text");
        var assetId = new AssetId("asset.text");
        var updatedAtUtc = new DateTimeOffset(2026, 7, 4, 10, 0, 0, TimeSpan.Zero);
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.TextAsset,
            "story.json",
            textEditDraftId: "draft-1",
            textEditDraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
            textEditDraftRevision: 3));
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey: workspaceKey,
                UpdatedAtUtc: updatedAtUtc,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts:
                [
                    new WorkspaceAssetEditDraftDescriptorDto(
                        DraftId: "draft-1",
                        DraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
                        SourceFingerprint: "source-v1",
                        UpdatedAtUtc: updatedAtUtc)
                ],
                OperationSequence: 3,
                Operations: Array.Empty<WorkspaceOperationRecordDto>())
        };
        var handler = new OpenTextAssetEditDraftHandler(
            registry,
            new TestWorkspaceDataStoreFactory(changeStore, new TestAssetEditDraftTextStore()));

        var result = await handler.HandleAsync(new OpenTextAssetEditDraftCommand(
            workspaceId,
            workspaceKey,
            workspaceDataRootPath,
            bundleId,
            assetId,
            "draft-1",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            SourceFingerprint: null,
            DraftRevision: 3,
            IsAppliedBaseline: true));

        TestAssert.True(result.IsFailure, "Applied baseline must not reopen a stale session projection after text edit operations are deleted.");
        TestAssert.Equal(
            "Asset.EditDraft.SourceMismatch",
            TestAssert.Single(result.Errors).Code,
            "Stale applied baseline should be rejected so Presentation can create a fresh original-backed draft.");
    }
}
