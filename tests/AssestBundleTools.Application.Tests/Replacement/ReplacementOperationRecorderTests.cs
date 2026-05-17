using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Replacement;

internal static class ReplacementOperationRecorderTests
{
    public static async Task ReplaceAssetFromFileHandler_ShouldAppendOperationRecord_WhenWorkspaceStagingIsAvailable()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var workspaceKey = "workspace-a";
        var workspaceDataRootPath = @"D:\WorkspaceData\A";
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var replacedAtUtc = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            AssetType.TextAsset,
            "DialogueText"));
        registry.TryReplaceAssetFromFileResult = new AssetFileReplacementResultDto(
            WorkspaceId: workspaceId,
            TargetBundleId: targetBundleId,
            TargetAssetId: targetAssetId,
            TargetAssetName: "DialogueText",
            SourceFilePath: @"C:\Imports\dialogue.txt",
            SourceFileName: "dialogue.txt",
            SourceAssetType: AssetType.TextAsset,
            ReplacedAtUtc: replacedAtUtc);
        var changeStore = new TestWorkspaceChangeStore();
        var recorder = new WorkspaceReplacementOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var handler = new ReplaceAssetFromFileHandler(registry, recorder);

        var result = await handler.HandleAsync(new ReplaceAssetFromFileCommand(
            workspaceId,
            targetBundleId,
            targetAssetId,
            @"C:\Imports\dialogue.txt",
            AssetType.TextAsset,
            workspaceKey,
            workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "File replacement should still succeed.");
        TestAssert.Equal(workspaceDataRootPath, changeStore.LastRootDirectoryPath, "Recorder should use the workspace data root.");
        TestAssert.Equal(workspaceKey, changeStore.LastLoadedWorkspaceKey, "Recorder should load the workspace change set.");

        var savedOperation = TestAssert.Single(changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>());
        TestAssert.Equal(WorkspaceOperationKind.ReplaceFromFile, savedOperation.OperationKind, "Operation kind should record file replacement.");
        TestAssert.Equal(WorkspaceOperationStatus.Applied, savedOperation.Status, "Successful replacement should be applied.");
        TestAssert.Equal(1, savedOperation.Sequence, "First persisted operation should receive sequence 1.");
        TestAssert.Equal(targetBundleId.Value, savedOperation.Target?.BundleId, "Operation target bundle should be recorded.");
        TestAssert.Equal(targetAssetId.Value, savedOperation.Target?.AssetId, "Operation target asset should be recorded.");
        TestAssert.Equal("file", savedOperation.Source?.SourceKind, "Operation source kind should be file.");
        TestAssert.Equal(@"C:\Imports\dialogue.txt", savedOperation.Source?.SourceFilePath, "Operation source file path should be recorded.");
        TestAssert.Equal("TextAsset", savedOperation.Source?.SourceAssetType, "Operation source type should be recorded.");
        TestAssert.Equal(false, savedOperation.PreviousState?.WasModified, "Previous modified state should be recorded.");
    }

    public static async Task ReplaceAssetFromWorkspaceHandler_ShouldSupersedeLatestActiveReplacementRecord()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var workspaceKey = "workspace-a";
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var sourceBundleId = new BundleId("bundle.source");
        var sourceAssetId = new AssetId("asset.source");
        var replacedAtUtc = new DateTimeOffset(2026, 5, 10, 12, 30, 0, TimeSpan.Zero);
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, targetBundleId, targetAssetId, AssetType.Texture2D, "HeroTexture"));
        registry.RegisterAssetDetails(CreateAssetDetails(workspaceId, sourceBundleId, sourceAssetId, AssetType.Texture2D, "EnemyTexture"));
        registry.TryReplaceAssetFromWorkspaceResult = new AssetReplacementResultDto(
            WorkspaceId: workspaceId,
            TargetBundleId: targetBundleId,
            TargetAssetId: targetAssetId,
            TargetAssetName: "HeroTexture",
            SourceBundleId: sourceBundleId,
            SourceAssetId: sourceAssetId,
            SourceBundleDisplayName: "source.ab",
            SourceAssetName: "EnemyTexture",
            SourceAssetType: AssetType.Texture2D,
            ReplacedAtUtc: replacedAtUtc);
        var existingChangeSet = new WorkspaceChangeSetDto(
            WorkspaceKey: workspaceKey,
            UpdatedAtUtc: replacedAtUtc.AddMinutes(-1),
            Changes: Array.Empty<WorkspaceChangeRecordDto>(),
            EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
            SchemaVersion: WorkspaceChangeSetDto.CurrentSchemaVersion,
            OperationSequence: 7,
            Operations:
            [
                new WorkspaceOperationRecordDto(
                    OperationId: "replace-old",
                    Sequence: 7,
                    OperationKind: WorkspaceOperationKind.ReplaceFromFile,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(BundleId: targetBundleId.Value, AssetId: targetAssetId.Value),
                    Source: new WorkspaceOperationSourceDto(SourceKind: "file", SourceFilePath: @"C:\Old.png"),
                    PreviousState: null,
                    Result: null,
                    CreatedAtUtc: replacedAtUtc.AddMinutes(-1),
                    AppliedAtUtc: replacedAtUtc.AddMinutes(-1))
            ]);
        var changeStore = new TestWorkspaceChangeStore { ChangeSet = existingChangeSet };
        var recorder = new WorkspaceReplacementOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var handler = new ReplaceAssetFromWorkspaceHandler(registry, recorder);

        var result = await handler.HandleAsync(new ReplaceAssetFromWorkspaceCommand(
            workspaceId,
            targetBundleId,
            targetAssetId,
            sourceBundleId,
            sourceAssetId,
            workspaceKey,
            @"D:\WorkspaceData\A"));

        TestAssert.True(result.IsSuccess, "Workspace replacement should succeed.");
        var savedOperations = changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>();
        TestAssert.Equal(2, savedOperations.Count, "New operation should append to the existing log.");
        var newOperation = savedOperations[1];
        TestAssert.Equal(8, newOperation.Sequence, "New operation should advance operation sequence.");
        TestAssert.Equal(WorkspaceOperationKind.ReplaceFromWorkspace, newOperation.OperationKind, "Operation kind should record workspace replacement.");
        TestAssert.Equal("replace-old", newOperation.SupersedesOperationId, "New replacement should point at the previous active replacement.");
        TestAssert.Equal(sourceBundleId.Value, newOperation.Source?.SourceBundleId, "Source bundle id should be recorded.");
        TestAssert.Equal(sourceAssetId.Value, newOperation.Source?.SourceAssetId, "Source asset id should be recorded.");
    }

    public static async Task UndoAssetReplacementHandler_ShouldAppendUndoOperation_ForLatestActiveReplacementRecord()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var workspaceKey = "workspace-a";
        var targetBundleId = new BundleId("bundle.target");
        var targetAssetId = new AssetId("asset.target");
        var now = new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero);
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            targetBundleId,
            targetAssetId,
            AssetType.Texture2D,
            "HeroTexture",
            isModified: true,
            fileReplacementSourceFileName: "hero.png",
            fileReplacementSourcePath: @"C:\Imports\hero.png"));
        registry.TryUndoAssetReplacementResult = new AssetReplacementUndoResultDto(
            WorkspaceId: workspaceId,
            TargetBundleId: targetBundleId,
            TargetAssetId: targetAssetId,
            TargetAssetName: "HeroTexture");
        var existingChangeSet = new WorkspaceChangeSetDto(
            WorkspaceKey: workspaceKey,
            UpdatedAtUtc: now,
            Changes: Array.Empty<WorkspaceChangeRecordDto>(),
            EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
            SchemaVersion: WorkspaceChangeSetDto.CurrentSchemaVersion,
            OperationSequence: 3,
            Operations:
            [
                new WorkspaceOperationRecordDto(
                    OperationId: "replace-latest",
                    Sequence: 3,
                    OperationKind: WorkspaceOperationKind.ReplaceFromFile,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(BundleId: targetBundleId.Value, AssetId: targetAssetId.Value),
                    Source: new WorkspaceOperationSourceDto(SourceKind: "file", SourceFilePath: @"C:\Imports\hero.png"),
                    PreviousState: null,
                    Result: null,
                    CreatedAtUtc: now,
                    AppliedAtUtc: now)
            ]);
        var changeStore = new TestWorkspaceChangeStore { ChangeSet = existingChangeSet };
        var recorder = new WorkspaceReplacementOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var handler = new UndoAssetReplacementHandler(registry, recorder);

        var result = await handler.HandleAsync(new UndoAssetReplacementCommand(
            workspaceId,
            targetBundleId,
            targetAssetId,
            workspaceKey,
            @"D:\WorkspaceData\A"));

        TestAssert.True(result.IsSuccess, "Undo should succeed.");
        var savedOperations = changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>();
        TestAssert.Equal(2, savedOperations.Count, "Undo operation should append to the log.");
        var undoOperation = savedOperations[1];
        TestAssert.Equal(WorkspaceOperationKind.UndoReplacement, undoOperation.OperationKind, "Undo should be recorded as its own operation.");
        TestAssert.Equal("replace-latest", undoOperation.SupersedesOperationId, "Undo should point at the latest active replacement.");
        TestAssert.Equal(true, undoOperation.PreviousState?.WasModified, "Undo previous state should capture the replaced target state.");
        TestAssert.Equal(@"C:\Imports\hero.png", undoOperation.PreviousState?.ReplacementSource?.SourceFilePath, "Undo previous replacement source should be recorded.");
    }

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetType assetType,
        string assetName,
        bool isModified = false,
        string? fileReplacementSourceFileName = null,
        string? fileReplacementSourcePath = null) =>
        new(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: assetName,
            AssetPath: $"Assets/{assetName}",
            ContainerPath: string.Empty,
            AssetType: assetType,
            SizeInBytes: 256,
            PreferredDisplaySizeInBytes: 256,
            PathId: 12345,
            IsModified: isModified,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: fileReplacementSourceFileName,
            FileReplacementSourcePath: fileReplacementSourcePath,
            FileReplacementSourceAssetType: string.IsNullOrWhiteSpace(fileReplacementSourcePath) ? null : assetType,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssestBundleTools.Application.Assets.Dtos.AssetDependencyDto>());

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

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null)
        {
            _changeStore.LastRootDirectoryPath = rootDirectoryPath;
            return _changeStore;
        }

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        public WorkspaceChangeSetDto? ChangeSet { get; init; }

        public WorkspaceChangeSetDto? SavedChangeSet { get; private set; }

        public string? LastLoadedWorkspaceKey { get; private set; }

        public string? LastRootDirectoryPath { get; set; }

        public WorkspaceChangeSetDto? Load(string workspaceKey)
        {
            LastLoadedWorkspaceKey = workspaceKey;
            return ChangeSet;
        }

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            SavedChangeSet = changeSet;
        }

        public void Delete(string workspaceKey) =>
            throw new NotSupportedException();
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly Dictionary<(string BundleId, string AssetId), AssetDetailsDto> _assetDetails = new();

        public AssetFileReplacementResultDto? TryReplaceAssetFromFileResult { get; set; }

        public AssetReplacementResultDto? TryReplaceAssetFromWorkspaceResult { get; set; }

        public AssetReplacementUndoResultDto? TryUndoAssetReplacementResult { get; set; }

        public void RegisterAssetDetails(AssetDetailsDto details)
        {
            _assetDetails[(details.BundleId.Value, details.AssetId.Value)] = details;
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) =>
            throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) => true;

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) => true;

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) =>
            _assetDetails.ContainsKey((bundleId.Value, assetId.Value));

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails) =>
            _assetDetails.TryGetValue((bundleId.Value, assetId.Value), out assetDetails);

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult)
        {
            replacementResult = TryReplaceAssetFromFileResult;
            return replacementResult is not null;
        }

        public bool TryReplaceAssetFromWorkspace(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            BundleId sourceBundleId,
            AssetId sourceAssetId,
            out AssetReplacementResultDto? replacementResult)
        {
            replacementResult = TryReplaceAssetFromWorkspaceResult;
            return replacementResult is not null;
        }

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult)
        {
            undoResult = TryUndoAssetReplacementResult;
            return undoResult is not null;
        }

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

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext) =>
            throw new NotSupportedException();
    }
}
