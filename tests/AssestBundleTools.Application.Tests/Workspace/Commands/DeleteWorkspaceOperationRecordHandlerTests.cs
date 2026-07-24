using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static class DeleteWorkspaceOperationRecordHandlerTests
{
    public static async Task HandleAsync_ShouldDeleteMultipleRecordsAtomically_AndMergeAffectedTargets()
    {
        var workspaceId = new WorkspaceId("workspace-delete-batch");
        var firstOperation = CreateOperation("replace-1", 1, "bundle-1", "asset-1") with
        {
            Relations =
            [
                new WorkspaceOperationRelationDto(
                    "relation-1",
                    "AssociatedAsset",
                    "RelatedTarget",
                    new WorkspaceOperationTargetDto("bundle-1", "asset-related", "related", "Texture2D"))
            ]
        };
        var secondOperation = CreateOperation("replace-2", 2, "bundle-2", "asset-2");
        var extractionOperation = CreateOperation("extract-1", 3, "bundle-3", "asset-3") with
        {
            OperationKind = WorkspaceOperationKind.ExtractAsset
        };
        var changeStore = CreateChangeStore(firstOperation, secondOperation, extractionOperation);
        var sessionRegistry = new TestWorkspaceSessionRegistry(workspaceId);
        var handler = CreateHandler(sessionRegistry, changeStore);

        var result = await handler.HandleAsync(new DeleteWorkspaceOperationRecordCommand(
            workspaceId,
            firstOperation.OperationId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            [secondOperation.OperationId]));

        TestAssert.True(result.IsSuccess, "A valid batch restore should succeed.");
        TestAssert.Equal(1, changeStore.SaveCount, "The batch restore should persist exactly once.");
        TestAssert.Equal(1, sessionRegistry.ReplayCount, "The batch restore should replay the session exactly once.");
        TestAssert.Equal(1, changeStore.SavedChangeSet?.Operations?.Count ?? 0, "Only protected extraction history should remain.");
        TestAssert.Equal(extractionOperation.OperationId, changeStore.SavedChangeSet!.Operations![0].OperationId, "Extraction history should be preserved.");
        TestAssert.Equal(2, result.Value?.DeletedOperationIds?.Count ?? 0, "The result should report every deleted operation.");
        TestAssert.Equal(3, result.Value?.AffectedTargets?.Count ?? 0, "Primary and explicitly related targets should be merged.");
    }

    public static async Task HandleAsync_ShouldRejectEntireBatch_WhenAnyRecordIsProtected()
    {
        var workspaceId = new WorkspaceId("workspace-delete-batch-protected");
        var replacementOperation = CreateOperation("replace-1", 1, "bundle-1", "asset-1");
        var extractionOperation = CreateOperation("extract-1", 2, "bundle-1", "asset-1") with
        {
            OperationKind = WorkspaceOperationKind.ExtractAsset
        };
        var changeStore = CreateChangeStore(replacementOperation, extractionOperation);
        var sessionRegistry = new TestWorkspaceSessionRegistry(workspaceId);
        var handler = CreateHandler(sessionRegistry, changeStore);

        var result = await handler.HandleAsync(new DeleteWorkspaceOperationRecordCommand(
            workspaceId,
            replacementOperation.OperationId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            [extractionOperation.OperationId]));

        TestAssert.True(result.IsFailure, "A batch containing protected extraction history should fail.");
        TestAssert.True(changeStore.SavedChangeSet is null, "An invalid batch must not persist a partial deletion.");
        TestAssert.Equal(0, changeStore.SaveCount, "An invalid batch must not save the change set.");
        TestAssert.Equal(0, sessionRegistry.ReplayCount, "An invalid batch must not replay the session.");
    }

    public static Task HandleAsync_ShouldRejectExtractionOperationDeletion()
    {
        return ShouldRejectProtectedOperationDeletion(WorkspaceOperationKind.ExtractAsset);
    }

    public static Task HandleAsync_ShouldRejectBatchExtractionOperationDeletion()
    {
        return ShouldRejectProtectedOperationDeletion(WorkspaceOperationKind.ExtractBatch);
    }

    private static async Task ShouldRejectProtectedOperationDeletion(WorkspaceOperationKind operationKind)
    {
        var workspaceId = new WorkspaceId("workspace-delete");
        var changeStore = new TestWorkspaceChangeStore
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
                SchemaVersion: WorkspaceChangeSetDto.CurrentSchemaVersion,
                OperationSequence: 1,
                Operations:
                [
                    new WorkspaceOperationRecordDto(
                        OperationId: "extract-op",
                        Sequence: 1,
                        OperationKind: operationKind,
                        Status: WorkspaceOperationStatus.Applied,
                        WorkspaceKey: WorkspaceKey,
                        Target: new WorkspaceOperationTargetDto(
                            BundleId: "bundle-1",
                            AssetId: "asset-1",
                            AssetName: "asset",
                            AssetType: "Texture2D"),
                        Source: null,
                        PreviousState: null,
                        Result: null,
                        CreatedAtUtc: DateTimeOffset.UtcNow)
                ])
        };
        var handler = new DeleteWorkspaceOperationRecordHandler(
            new TestWorkspaceSessionRegistry(workspaceId),
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());

        var result = await handler.HandleAsync(new DeleteWorkspaceOperationRecordCommand(
            workspaceId,
            "extract-op",
            WorkspaceKey,
            WorkspaceDataRootPath));

        TestAssert.True(result.IsFailure, "Extraction operation records should not be deletable.");
        TestAssert.Equal(
            "Workspace.OperationRecord.DeleteNotAllowed",
            TestAssert.Single(result.Errors).Code,
            "Protected extraction records should return a stable validation error.");
        TestAssert.True(changeStore.SavedChangeSet is null, "Rejected deletion should not persist a modified change set.");
    }

    private const string WorkspaceKey = "workspace-delete-key";
    private const string WorkspaceDataRootPath = @"D:\WorkspaceData";

    private static DeleteWorkspaceOperationRecordHandler CreateHandler(
        TestWorkspaceSessionRegistry sessionRegistry,
        TestWorkspaceChangeStore changeStore) =>
        new(
            sessionRegistry,
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());

    private static TestWorkspaceChangeStore CreateChangeStore(params WorkspaceOperationRecordDto[] operations) =>
        new()
        {
            ChangeSet = new WorkspaceChangeSetDto(
                WorkspaceKey,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Changes: Array.Empty<WorkspaceChangeRecordDto>(),
                EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
                SchemaVersion: WorkspaceChangeSetDto.CurrentSchemaVersion,
                OperationSequence: operations.Length,
                Operations: operations)
        };

    private static WorkspaceOperationRecordDto CreateOperation(
        string operationId,
        long sequence,
        string bundleId,
        string assetId) =>
        new(
            operationId,
            sequence,
            WorkspaceOperationKind.ReplaceFromFile,
            WorkspaceOperationStatus.Applied,
            WorkspaceKey,
            new WorkspaceOperationTargetDto(bundleId, assetId, assetId, "Texture2D"),
            Source: null,
            PreviousState: null,
            Result: null,
            CreatedAtUtc: DateTimeOffset.UtcNow);

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly TestWorkspaceChangeStore _changeStore;

        public TestWorkspaceDataStoreFactory(TestWorkspaceChangeStore changeStore)
        {
            _changeStore = changeStore;
        }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) => _changeStore;

        public string GetDefaultRootDirectoryPath() => WorkspaceDataRootPath;
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        public WorkspaceChangeSetDto? ChangeSet { get; init; }

        public WorkspaceChangeSetDto? SavedChangeSet { get; private set; }

        public int SaveCount { get; private set; }

        public WorkspaceChangeSetDto? Load(string workspaceKey) => ChangeSet;

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            SavedChangeSet = changeSet;
            SaveCount++;
        }

        public void Delete(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class TestWorkspaceSessionRegistry :
        IWorkspaceSessionRegistry,
        IWorkspaceOperationReplaySessionRegistry
    {
        private readonly WorkspaceId _workspaceId;

        public TestWorkspaceSessionRegistry(WorkspaceId workspaceId)
        {
            _workspaceId = workspaceId;
        }

        public int ReplayCount { get; private set; }

        public bool TryReplayWorkspaceOperations(WorkspaceId workspaceId, WorkspaceChangeSetDto? changeSet)
        {
            ReplayCount++;
            return _workspaceId.Equals(workspaceId);
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) =>
            throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) => _workspaceId.Equals(workspaceId);

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) => throw new NotSupportedException();

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) =>
            throw new NotSupportedException();

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree) =>
            throw new NotSupportedException();

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            string? assetTypeDisplayNameFilter,
            out AssetListResultDto? assetListResult) =>
            throw new NotSupportedException();

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails) =>
            throw new NotSupportedException();

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary) =>
            throw new NotSupportedException();

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult) =>
            throw new NotSupportedException();

        public bool TryGetWorkspaceReplacementCandidates(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates) =>
            throw new NotSupportedException();

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult) =>
            throw new NotSupportedException();

        public bool TryReplaceAssetFromWorkspace(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            BundleId sourceBundleId,
            AssetId sourceAssetId,
            out AssetReplacementResultDto? replacementResult) =>
            throw new NotSupportedException();

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult) =>
            throw new NotSupportedException();

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext) =>
            throw new NotSupportedException();
    }
}
