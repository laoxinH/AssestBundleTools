using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Tests.Saving;

internal static partial class SaveWorkspaceHandlersTests
{
    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldForwardSpriteEditWriteItems_FromOperationRecords()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-alpha");
        var assetId = new AssetId("asset-sprite");
        var geometry = new WorkspaceSpriteEditGeometryDto(
            CanvasWidth: 256,
            CanvasHeight: 128,
            RegionX: 8,
            RegionY: 12,
            RegionWidth: 64,
            RegionHeight: 32,
            PivotX: 40,
            PivotY: 28);
        var indexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", bundleId, "Alpha.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                indexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                indexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: bundleId,
                        assetId: assetId,
                        bundleDisplayName: "Alpha.ab",
                        assetName: "HeroSprite",
                        isModified: true)
                ])
        };
        registry.AddAssetDetails(CreateAssetDetails(workspaceId, bundleId, assetId, "HeroSprite", AssetType.Sprite, "CAB-main", 88));
        var changeSet = WorkspaceChangeSetDto.Empty(workspaceKey) with
        {
            Operations =
            [
                new WorkspaceOperationRecordDto(
                    OperationId: "sprite-edit-1",
                    Sequence: 2,
                    OperationKind: WorkspaceOperationKind.EditDraftApplied,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(
                        BundleId: bundleId.Value,
                        AssetId: assetId.Value,
                        AssetName: "HeroSprite",
                        AssetType: AssetType.Sprite.ToString(),
                        AssetTypeDisplayName: "Sprite",
                        SerializedFileName: "CAB-main",
                        PathId: 88),
                    Source: new WorkspaceOperationSourceDto(
                        SourceKind: "draft",
                        DraftId: "draft-sprite-1",
                        DraftMode: AssestBundleTools.Application.Editing.Services.WorkspaceSpriteEditOperationRecorder.SpriteGeometryDraftMode),
                    PreviousState: null,
                    Result: new WorkspaceOperationResultDto(SpriteEditGeometry: geometry),
                    CreatedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero),
                    AppliedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero),
                    CompletedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero))
            ]
        };
        var exporter = new RecordingBundleExporter();
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([indexEntry]),
                artifactStore,
                new TestWorkspaceChangeStore(changeSet)),
            new ReadyWorkspaceSaveOutputProbe(),
            exporter,
            new RecordingBundleExportManifestStore());

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Save execution should succeed with Sprite edit write items.");
        var writeItem = TestAssert.Single(TestAssert.Single(exporter.Requests).AssetWriteItems);
        TestAssert.Equal(assetId, writeItem.AssetId, "Sprite write item should keep target asset identity.");
        TestAssert.Equal(AssetType.Sprite, writeItem.AssetType, "Sprite write item should keep target asset type.");
        TestAssert.Equal("sprite-edit", writeItem.SourceKind, "Sprite edit should use a dedicated source kind for the writer.");
        TestAssert.Equal(geometry, writeItem.SpriteEditGeometry, "Sprite edit geometry should be forwarded to the bundle writer.");
        TestAssert.Equal("sprite-edit-1", writeItem.OperationId, "Sprite edit operation id should be forwarded for manifest/checkpoint correlation.");
    }
}
