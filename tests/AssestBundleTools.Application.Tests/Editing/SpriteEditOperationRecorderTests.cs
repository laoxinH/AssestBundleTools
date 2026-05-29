using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Editing;

internal static class SpriteEditOperationRecorderTests
{
    public static async Task ApplySpriteEditHandler_ShouldAppendOperationRecord_AndDraftDescriptor()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var workspaceKey = "workspace-a";
        var workspaceDataRootPath = @"D:\WorkspaceData\A";
        var bundleId = new BundleId("bundle.sprites");
        var assetId = new AssetId("asset.hero-sprite");
        var geometry = new WorkspaceSpriteEditGeometryDto(
            CanvasWidth: 256,
            CanvasHeight: 128,
            RegionX: 4,
            RegionY: 8,
            RegionWidth: 64,
            RegionHeight: 32,
            PivotX: 36,
            PivotY: 24);
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Sprite,
            "HeroSprite"));
        var changeStore = new TestWorkspaceChangeStore();
        var recorder = new WorkspaceSpriteEditOperationRecorder(
            new TestWorkspaceDataStoreFactory(changeStore),
            new WorkspaceOperationLogService());
        var handler = new ApplySpriteEditHandler(registry, registry, recorder);

        var result = await handler.HandleAsync(new ApplySpriteEditCommand(
            workspaceId,
            bundleId,
            assetId,
            geometry,
            workspaceKey,
            workspaceDataRootPath,
            DraftId: "draft-sprite-1"));

        TestAssert.True(result.IsSuccess, "Sprite edit save should succeed.");
        TestAssert.Equal(1, registry.TryApplySpriteEditCallCount, "Registry should receive the applied Sprite edit.");
        TestAssert.Equal(geometry, registry.LastSpriteEditGeometry, "Registry should receive the current edit geometry.");
        TestAssert.Equal(workspaceDataRootPath, changeStore.LastRootDirectoryPath, "Recorder should use the workspace data root.");
        TestAssert.Equal(workspaceKey, changeStore.LastLoadedWorkspaceKey, "Recorder should load the workspace change set.");

        var savedOperation = TestAssert.Single(changeStore.SavedChangeSet?.Operations ?? Array.Empty<WorkspaceOperationRecordDto>());
        TestAssert.Equal(WorkspaceOperationKind.EditDraftApplied, savedOperation.OperationKind, "Sprite save should be recorded as an applied edit draft.");
        TestAssert.Equal(WorkspaceOperationStatus.Applied, savedOperation.Status, "Saved Sprite edit should be applied.");
        TestAssert.Equal(bundleId.Value, savedOperation.Target?.BundleId, "Operation target bundle should be recorded.");
        TestAssert.Equal(assetId.Value, savedOperation.Target?.AssetId, "Operation target asset should be recorded.");
        TestAssert.Equal("draft", savedOperation.Source?.SourceKind, "Sprite edit source should be a draft.");
        TestAssert.Equal("draft-sprite-1", savedOperation.Source?.DraftId, "Draft id should be recorded.");
        TestAssert.Equal(WorkspaceSpriteEditOperationRecorder.SpriteGeometryDraftMode, savedOperation.Source?.DraftMode, "Draft mode should identify Sprite geometry.");
        TestAssert.Equal(geometry, savedOperation.Result?.SpriteEditGeometry, "Operation result should persist the Sprite geometry.");
        TestAssert.Equal(false, savedOperation.PreviousState?.WasModified, "Previous modified state should be recorded.");

        var draftDescriptor = TestAssert.Single(changeStore.SavedChangeSet?.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>());
        TestAssert.Equal("draft-sprite-1", draftDescriptor.DraftId, "Draft descriptor should be upserted.");
        TestAssert.Equal(WorkspaceSpriteEditOperationRecorder.SpriteGeometryDraftMode, draftDescriptor.DraftMode, "Draft descriptor should use the Sprite geometry mode.");
        TestAssert.False(string.IsNullOrWhiteSpace(draftDescriptor.SourceFingerprint), "Draft descriptor should carry a geometry fingerprint.");
    }

    public static async Task ApplySpriteEditHandler_ShouldReturnFailure_WhenGeometryIsInvalid()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.sprites");
        var assetId = new AssetId("asset.hero-sprite");
        var registry = new TestWorkspaceSessionRegistry();
        registry.RegisterAssetDetails(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Sprite,
            "HeroSprite"));
        var handler = new ApplySpriteEditHandler(registry, registry);

        var result = await handler.HandleAsync(new ApplySpriteEditCommand(
            workspaceId,
            bundleId,
            assetId,
            new WorkspaceSpriteEditGeometryDto(256, 128, 4, 8, 64, 32, 128, 96)));

        TestAssert.True(result.IsFailure, "Pivot outside the crop rect should fail validation.");
        TestAssert.Equal(
            "Asset.EditDraft.InvalidGeometry",
            TestAssert.Single(result.Errors).Code,
            "Invalid geometry should report the Sprite edit geometry error.");
        TestAssert.Equal(0, registry.TryApplySpriteEditCallCount, "Validation failure should not apply session state.");
    }

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        AssetType assetType,
        string assetName,
        IReadOnlyList<AssetDependencyDto>? dependencies = null) =>
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
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: dependencies ?? Array.Empty<AssetDependencyDto>());

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

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry, IWorkspaceSpriteEditSessionRegistry
    {
        private readonly Dictionary<(string BundleId, string AssetId), AssetDetailsDto> _assetDetails = new();

        public int TryApplySpriteEditCallCount { get; private set; }

        public WorkspaceSpriteEditGeometryDto? LastSpriteEditGeometry { get; private set; }

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

        public bool TryApplySpriteEdit(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            string draftId,
            string draftMode,
            WorkspaceSpriteEditGeometryDto geometry,
            out AssetSpriteEditAppliedResultDto? editResult)
        {
            TryApplySpriteEditCallCount++;
            LastSpriteEditGeometry = geometry;
            if (!_assetDetails.TryGetValue((bundleId.Value, assetId.Value), out var details))
            {
                editResult = null;
                return false;
            }

            editResult = new AssetSpriteEditAppliedResultDto(
                workspaceId,
                bundleId,
                assetId,
                details.Name,
                draftId,
                draftMode,
                geometry,
                new DateTimeOffset(2026, 5, 24, 10, 0, 0, TimeSpan.Zero));
            return true;
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

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult)
        {
            throw new NotSupportedException();
        }

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
