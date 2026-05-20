using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Runtime;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Runtime;

internal static class CachedWorkspaceSessionRegistryOperationRestoreTests
{
    public static void OpenWorkspace_ShouldMaterializePersistedExtractionOperations_FromChangeSet()
    {
        var workspaceKey = "workspace-a";
        var bundleId = "bundle.textures";
        var assetId = "asset.texture";
        var now = new DateTimeOffset(2026, 5, 10, 14, 30, 0, TimeSpan.Zero);
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: "bundle-cache-textures",
            BundleId: bundleId,
            DisplayName: "textures.ab",
            LogicalPath: "textures.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("textures.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(assetId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 10)
            ],
            SerializedFileNames: ["CAB-textures"]);
        var changeSet = new WorkspaceChangeSetDto(
            WorkspaceKey: workspaceKey,
            UpdatedAtUtc: now,
            Changes: Array.Empty<WorkspaceChangeRecordDto>(),
            EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
            SchemaVersion: WorkspaceChangeSetDto.CurrentSchemaVersion,
            OperationSequence: 1,
            Operations:
            [
                new WorkspaceOperationRecordDto(
                    OperationId: "extract-texture",
                    Sequence: 1,
                    OperationKind: WorkspaceOperationKind.ExtractAsset,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(BundleId: bundleId, AssetId: assetId),
                    Source: null,
                    PreviousState: null,
                    Result: new WorkspaceOperationResultDto(
                        OutputPath: @"D:\Exports\HeroTexture.png",
                        OutputFileExists: true),
                    CreatedAtUtc: now,
                    AppliedAtUtc: now)
            ]);
        var registry = new CachedWorkspaceSessionRegistry(
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([cacheEntry]),
                new TestWorkspaceChangeStore(changeSet)),
            new WorkspaceOperationLogService());

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: workspaceKey,
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));
        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(bundleId),
            new AssetId(assetId),
            out var details);

        TestAssert.True(hasDetails, "Extracted asset details should be available.");
        TestAssert.Equal(now, details!.LastExtractedAtUtc, "Persisted extraction should restore the last extracted timestamp.");
    }

    public static void OpenWorkspace_ShouldMaterializePersistedReplacementOperations_FromChangeSet()
    {
        var workspaceKey = "workspace-a";
        var bundleId = "bundle.textures";
        var targetAssetId = "asset.target";
        var sourceAssetId = "asset.source";
        var now = new DateTimeOffset(2026, 5, 10, 14, 0, 0, TimeSpan.Zero);
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: "bundle-cache-textures",
            BundleId: bundleId,
            DisplayName: "textures.ab",
            LogicalPath: "textures.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("textures.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(targetAssetId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 10),
                CreateAsset(sourceAssetId, "EnemyTexture", AssetType.Texture2D, "Texture2D", pathId: 20)
            ],
            SerializedFileNames: ["CAB-textures"]);
        var changeSet = CreateReplacementChangeSet(workspaceKey, bundleId, targetAssetId, sourceAssetId, now);
        var registry = new CachedWorkspaceSessionRegistry(
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([cacheEntry]),
                new TestWorkspaceChangeStore(changeSet)),
            new WorkspaceOperationLogService());

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: workspaceKey,
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));
        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(bundleId),
            new AssetId(targetAssetId),
            out var details);

        TestAssert.True(hasDetails, "Target details should be available.");
        TestAssert.True(details!.IsModified, "Persisted replacement should mark target as modified.");
        TestAssert.Equal(@"C:\Imports\hero.png", details.FileReplacementSourcePath, "Undo should reveal the previous active file replacement.");
        TestAssert.Equal("hero.png", details.FileReplacementSourceFileName, "File replacement name should be restored.");
        TestAssert.Equal(null, details.WorkspaceReplacementSourceAssetName, "Superseded workspace replacement should not be materialized.");
    }

    public static void OpenWorkspace_ShouldMaterializePersistedRedoReplacementOperation_FromChangeSet()
    {
        var workspaceKey = "workspace-a";
        var bundleId = "bundle.textures";
        var targetAssetId = "asset.target";
        var sourceAssetId = "asset.source";
        var now = new DateTimeOffset(2026, 5, 10, 14, 0, 0, TimeSpan.Zero);
        var cacheEntry = new BundleIndexCacheEntryDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: "bundle-cache-textures",
            BundleId: bundleId,
            DisplayName: "textures.ab",
            LogicalPath: "textures.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto("textures.ab", 100, DateTimeOffset.UnixEpoch, "hash"),
            CachedAtUtc: DateTimeOffset.UnixEpoch,
            ParserVersion: "test",
            ToolVersion: "test",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                CreateAsset(targetAssetId, "HeroTexture", AssetType.Texture2D, "Texture2D", pathId: 10),
                CreateAsset(sourceAssetId, "EnemyTexture", AssetType.Texture2D, "Texture2D", pathId: 20)
            ],
            SerializedFileNames: ["CAB-textures"]);
        var baseChangeSet = CreateReplacementChangeSet(workspaceKey, bundleId, targetAssetId, sourceAssetId, now);
        var changeSet = baseChangeSet with
        {
            OperationSequence = 4,
            Operations = baseChangeSet.Operations!
                .Concat(
                [
                    new WorkspaceOperationRecordDto(
                        OperationId: "redo-workspace",
                        Sequence: 4,
                        OperationKind: WorkspaceOperationKind.RedoReplacement,
                        Status: WorkspaceOperationStatus.Applied,
                        WorkspaceKey: workspaceKey,
                        Target: new WorkspaceOperationTargetDto(BundleId: bundleId, AssetId: targetAssetId),
                        Source: null,
                        PreviousState: null,
                        Result: null,
                        CreatedAtUtc: now.AddSeconds(3),
                        AppliedAtUtc: now.AddSeconds(3),
                        SupersedesOperationId: "undo-workspace")
                ])
                .ToArray()
        };
        var registry = new CachedWorkspaceSessionRegistry(
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([cacheEntry]),
                new TestWorkspaceChangeStore(changeSet)),
            new WorkspaceOperationLogService());

        var summary = registry.OpenWorkspace(new WorkspaceSessionOpenRequestDto(
            WorkspaceKey: workspaceKey,
            WorkspacePath: "workspace",
            WorkspaceDataRootPath: "data",
            WorkspaceDisplayName: "Workspace A",
            InitialMode: WorkspaceMode.SinglePane));
        var hasDetails = registry.TryGetAssetDetails(
            summary.WorkspaceId,
            new BundleId(bundleId),
            new AssetId(targetAssetId),
            out var details);

        TestAssert.True(hasDetails, "Target details should be available.");
        TestAssert.True(details!.IsModified, "Redo should restore modified replacement state.");
        TestAssert.Equal("EnemyTexture", details.WorkspaceReplacementSourceAssetName, "Redo should restore the workspace replacement source.");
        TestAssert.Equal(bundleId, details.WorkspaceReplacementSourceBundleId?.Value, "Redo should restore source bundle id for export/read-model consumers.");
        TestAssert.Equal(sourceAssetId, details.WorkspaceReplacementSourceAssetId?.Value, "Redo should restore source asset id for export/read-model consumers.");
        TestAssert.Equal(null, details.FileReplacementSourceFileName, "Redo of workspace replacement should no longer expose the older file source.");
        TestAssert.False(details.CanRedoReplacement, "After redo, the target should no longer advertise another redo step.");
    }

    private static WorkspaceChangeSetDto CreateReplacementChangeSet(
        string workspaceKey,
        string bundleId,
        string targetAssetId,
        string sourceAssetId,
        DateTimeOffset now) =>
        new(
            WorkspaceKey: workspaceKey,
            UpdatedAtUtc: now,
            Changes: Array.Empty<WorkspaceChangeRecordDto>(),
            EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>(),
            SchemaVersion: WorkspaceChangeSetDto.CurrentSchemaVersion,
            OperationSequence: 3,
            Operations:
            [
                new WorkspaceOperationRecordDto(
                    OperationId: "replace-file",
                    Sequence: 1,
                    OperationKind: WorkspaceOperationKind.ReplaceFromFile,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(BundleId: bundleId, AssetId: targetAssetId),
                    Source: new WorkspaceOperationSourceDto(
                        SourceKind: "file",
                        SourceFilePath: @"C:\Imports\hero.png",
                        SourceFileName: "hero.png",
                        SourceAssetType: "Texture2D"),
                    PreviousState: null,
                    Result: null,
                    CreatedAtUtc: now,
                    AppliedAtUtc: now),
                new WorkspaceOperationRecordDto(
                    OperationId: "replace-workspace",
                    Sequence: 2,
                    OperationKind: WorkspaceOperationKind.ReplaceFromWorkspace,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(BundleId: bundleId, AssetId: targetAssetId),
                    Source: new WorkspaceOperationSourceDto(
                        SourceKind: "workspace",
                        SourceBundleId: bundleId,
                        SourceAssetId: sourceAssetId),
                    PreviousState: null,
                    Result: null,
                    CreatedAtUtc: now.AddSeconds(1),
                    AppliedAtUtc: now.AddSeconds(1)),
                new WorkspaceOperationRecordDto(
                    OperationId: "undo-workspace",
                    Sequence: 3,
                    OperationKind: WorkspaceOperationKind.UndoReplacement,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(BundleId: bundleId, AssetId: targetAssetId),
                    Source: null,
                    PreviousState: null,
                    Result: null,
                    CreatedAtUtc: now.AddSeconds(2),
                    AppliedAtUtc: now.AddSeconds(2),
                    SupersedesOperationId: "replace-workspace")
            ]);

    private static BundleIndexAssetCacheEntryDto CreateAsset(
        string assetId,
        string name,
        AssetType assetType,
        string? displayType,
        long pathId) =>
        new(
            AssetId: assetId,
            AssetName: name,
            AssetType: assetType.ToString(),
            AssetPath: $"CAB-test/{name}",
            ContainerPath: string.Empty,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            HasPreview: true,
            CanExtract: true,
            CanReplace: true,
            SerializedFileName: "CAB-test",
            PathId: pathId,
            AssetTypeDisplayName: displayType,
            Dependencies: Array.Empty<BundleIndexAssetDependencyCacheEntryDto>(),
            IncludeInAssetList: true);

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;
        private readonly IWorkspaceChangeStore _workspaceChangeStore;

        public TestWorkspaceDataStoreFactory(
            IBundleIndexCacheStore bundleIndexCacheStore,
            IWorkspaceChangeStore workspaceChangeStore)
        {
            _bundleIndexCacheStore = bundleIndexCacheStore;
            _workspaceChangeStore = workspaceChangeStore;
        }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            _bundleIndexCacheStore;

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) =>
            _workspaceChangeStore;

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleIndexCacheStore : IBundleIndexCacheStore
    {
        private readonly IReadOnlyList<BundleIndexCacheEntryDto> _entries;

        public TestBundleIndexCacheStore(IReadOnlyList<BundleIndexCacheEntryDto> entries)
        {
            _entries = entries;
        }

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) =>
            _entries.FirstOrDefault(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
                string.Equals(entry.BundleCacheKey, bundleCacheKey, StringComparison.Ordinal));

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) =>
            _entries
                .Where(entry => string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal))
                .ToArray();

        public void Save(BundleIndexCacheEntryDto cacheEntry) =>
            throw new NotSupportedException();

        public void Delete(string workspaceKey, string bundleCacheKey) =>
            throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) =>
            throw new NotSupportedException();
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        private readonly WorkspaceChangeSetDto? _changeSet;

        public TestWorkspaceChangeStore(WorkspaceChangeSetDto? changeSet)
        {
            _changeSet = changeSet;
        }

        public WorkspaceChangeSetDto? Load(string workspaceKey) => _changeSet;

        public void Save(WorkspaceChangeSetDto changeSet) =>
            throw new NotSupportedException();

        public void Delete(string workspaceKey) =>
            throw new NotSupportedException();
    }
}
