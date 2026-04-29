using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static class RemoveWorkspaceBundleHandlerTests
{
    public static async Task HandleAsync_ShouldRemoveBundleArtifacts_AndPersistExcludedPath_WhenBundleRemainsCoveredByDirectoryImport()
    {
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string bundleSourcePath = @"C:\Bundles\Characters\hero.ab";
        const string directorySourcePath = @"C:\Bundles\Characters";
        var bundleId = new BundleId("bundle.hero");
        var bundleCacheKey = CreateBundleCacheKey(bundleSourcePath);
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Example Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 26, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 9, 30, 0, TimeSpan.Zero),
            LastBundleId: bundleId.Value,
            LastAssetId: "asset-removed",
            LastOutputDirectory: @"D:\Exports",
            BundleSearchKeyword: "hero",
            AssetSearchKeyword: "texture",
            AssetTypeFilter: "Texture2D",
            ShowModifiedOnly: false,
            ShowPreviewableOnly: true,
            OpenBundleTabs:
            [
                new WorkspaceMetadataBundleTabDto(
                    BundleId: bundleId.Value,
                    LogicalPath: "characters/hero.ab",
                    IsPreview: false,
                    SelectedAssetId: "asset-removed",
                    SearchKeyword: "hero",
                    AssetTypeFilter: "Texture2D",
                    ShowModifiedOnly: false,
                    ShowPreviewableOnly: true),
                new WorkspaceMetadataBundleTabDto(
                    BundleId: "bundle.environment",
                    LogicalPath: "environment/world.ab",
                    IsPreview: false,
                    SelectedAssetId: "asset-keep",
                    SearchKeyword: string.Empty,
                    AssetTypeFilter: null,
                    ShowModifiedOnly: false,
                    ShowPreviewableOnly: false)
            ],
            ActiveBundleTabId: bundleId.Value,
            ImportedSources:
            [
                new WorkspaceImportedSourceDto(
                    Path: directorySourcePath,
                    ImportOrigin: WorkspaceImportOriginKind.BundleDirectory,
                    PathKind: WorkspaceImportPathKind.Directory,
                    RecordedAtUtc: DateTimeOffset.UtcNow),
                new WorkspaceImportedSourceDto(
                    Path: bundleSourcePath,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: DateTimeOffset.UtcNow)
            ],
            AssetBundleDecryptorId: "decryptor-a",
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore();
        var previewCacheStore = new TestPreviewCacheStore(
        [
            new PreviewCacheDescriptorDto(
                WorkspaceKey: workspaceKey,
                PreviewCacheKey: "preview-match",
                BundleCacheKey: bundleCacheKey,
                AssetId: "asset-removed",
                PreviewKind: "AudioClip",
                ContentType: "audio/wav",
                RelativePayloadPath: "payloads/preview-match.bin",
                SourceFingerprint: new WorkspaceFileFingerprintDto(
                    SourcePath: bundleSourcePath,
                    FileSizeInBytes: 128,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero),
                    ContentHash: "hash-a"),
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 9, 5, 0, TimeSpan.Zero),
                GeneratorVersion: "generator-a"),
            new PreviewCacheDescriptorDto(
                WorkspaceKey: workspaceKey,
                PreviewCacheKey: "preview-other",
                BundleCacheKey: "another-bundle",
                AssetId: "asset-keep",
                PreviewKind: "AudioClip",
                ContentType: "audio/wav",
                RelativePayloadPath: "payloads/preview-other.bin",
                SourceFingerprint: new WorkspaceFileFingerprintDto(
                    SourcePath: @"C:\Bundles\Environment\world.ab",
                    FileSizeInBytes: 256,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 9, 10, 0, TimeSpan.Zero),
                    ContentHash: "hash-b"),
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 9, 12, 0, TimeSpan.Zero),
                GeneratorVersion: "generator-a")
        ]);
        var handler = new RemoveWorkspaceBundleHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore));

        var result = await handler.HandleAsync(new RemoveWorkspaceBundleCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            BundleId: bundleId,
            BundleSourcePath: bundleSourcePath));

        TestAssert.True(result.IsSuccess, "Removing a registered workspace bundle should succeed.");
        var removalResult = result.Value ?? throw new InvalidOperationException("Expected removal result.");
        TestAssert.True(removalResult.RemovedDirectImportSource, "Direct import source should be removed.");
        TestAssert.True(removalResult.AddedExcludedBundleSourcePath, "Covered bundle should remain excluded after removal.");
        TestAssert.Equal(1, removalResult.RemainingRegisteredSourceCount, "Directory import should remain registered.");
        TestAssert.Equal(1, metadataStore.SaveCallCount, "Successful removal should persist workspace metadata.");

        var savedMetadata = metadataStore.LastSavedMetadata ?? throw new InvalidOperationException("Expected saved metadata.");
        TestAssert.Equal(1, savedMetadata.ImportedSources!.Count, "Only the directory import should remain.");
        TestAssert.Equal(directorySourcePath, savedMetadata.ImportedSources[0].Path, "Remaining import should be the directory source.");
        TestAssert.Equal(1, savedMetadata.ExcludedBundleSourcePaths!.Count, "Removed bundle should become an excluded path.");
        TestAssert.Equal(bundleSourcePath, savedMetadata.ExcludedBundleSourcePaths[0], "Excluded path should match the removed bundle.");
        TestAssert.Equal("bundle.environment", savedMetadata.ActiveBundleTabId, "Active tab should move to the remaining open bundle.");
        TestAssert.Equal(1, savedMetadata.OpenBundleTabs.Count, "Removed bundle tab should be cleared from restored metadata.");
        TestAssert.Equal("bundle.environment", savedMetadata.OpenBundleTabs[0].BundleId, "Remaining bundle tab should stay intact.");
        TestAssert.Equal<string?>(null, savedMetadata.LastBundleId, "Last bundle should clear when it points to the removed bundle.");
        TestAssert.Equal<string?>(null, savedMetadata.LastAssetId, "Last asset should clear together with the removed bundle.");

        TestAssert.Equal(1, bundleIndexCacheStore.DeleteOperations.Count, "Bundle index cache should be invalidated once.");
        TestAssert.Equal(workspaceKey, bundleIndexCacheStore.DeleteOperations[0].WorkspaceKey, "Bundle cache invalidation should use the current workspace.");
        TestAssert.Equal(bundleCacheKey, bundleIndexCacheStore.DeleteOperations[0].BundleCacheKey, "Bundle cache invalidation should target the removed bundle cache key.");
        TestAssert.Equal(1, previewCacheStore.DeletedPreviewKeys.Count, "Only matching preview cache entries should be removed.");
        TestAssert.Equal("preview-match", previewCacheStore.DeletedPreviewKeys[0], "Preview invalidation should target entries bound to the removed bundle.");
    }

    public static async Task HandleAsync_ShouldReturnFailure_WhenWorkspaceMetadataDoesNotExist()
    {
        var metadataStore = new TestWorkspaceMetadataStore(metadata: null);
        var bundleIndexCacheStore = new TestBundleIndexCacheStore();
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var handler = new RemoveWorkspaceBundleHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore));

        var result = await handler.HandleAsync(new RemoveWorkspaceBundleCommand(
            WorkspaceKey: "workspace-missing",
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            BundleId: new BundleId("bundle.missing"),
            BundleSourcePath: @"C:\Bundles\missing.ab"));

        TestAssert.True(result.IsFailure, "Missing metadata should fail removal.");
        TestAssert.Equal(
            "Workspace.Import.MetadataNotFound",
            TestAssert.Single(result.Errors).Code,
            "Missing metadata should use the expected error code.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Failure should not persist metadata.");
        TestAssert.Equal(0, bundleIndexCacheStore.DeleteOperations.Count, "Failure should not invalidate bundle caches.");
        TestAssert.Equal(0, previewCacheStore.DeletedPreviewKeys.Count, "Failure should not invalidate preview caches.");
    }

    private static string CreateBundleCacheKey(string sourcePath)
    {
        var normalizedSourcePath = WorkspaceCatalogKeyFactory.NormalizeSourcePath(sourcePath);
        var sourceBytes = Encoding.UTF8.GetBytes(normalizedSourcePath.ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant();
    }

    private sealed class TestWorkspaceMetadataStore : IWorkspaceMetadataStore
    {
        public TestWorkspaceMetadataStore(WorkspaceMetadataDto? metadata)
        {
            Metadata = metadata;
        }

        public WorkspaceMetadataDto? Metadata { get; private set; }

        public WorkspaceMetadataDto? LastSavedMetadata { get; private set; }

        public int SaveCallCount { get; private set; }

        public WorkspaceMetadataDto? Load(string workspaceKey) => Metadata;

        public void Save(WorkspaceMetadataDto metadata)
        {
            Metadata = metadata;
            LastSavedMetadata = metadata;
            SaveCallCount++;
        }

        public void Delete(string workspaceKey)
        {
            Metadata = null;
        }
    }

    private sealed class TestWorkspaceMetadataStoreFactory : IWorkspaceMetadataStoreFactory
    {
        private readonly IWorkspaceMetadataStore _store;

        public TestWorkspaceMetadataStoreFactory(IWorkspaceMetadataStore store)
        {
            _store = store;
        }

        public IWorkspaceMetadataStore Create(string? rootDirectoryPath = null) => _store;

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;
        private readonly IPreviewCacheStore _previewCacheStore;

        public TestWorkspaceDataStoreFactory(
            IBundleIndexCacheStore bundleIndexCacheStore,
            IPreviewCacheStore previewCacheStore)
        {
            _bundleIndexCacheStore = bundleIndexCacheStore;
            _previewCacheStore = previewCacheStore;
        }

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) => _bundleIndexCacheStore;

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) => _previewCacheStore;

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) => new TestWorkspaceChangeStore();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleIndexCacheStore : IBundleIndexCacheStore
    {
        public List<(string WorkspaceKey, string BundleCacheKey)> DeleteOperations { get; } = [];

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) => null;

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) => Array.Empty<BundleIndexCacheEntryDto>();

        public void Save(BundleIndexCacheEntryDto cacheEntry)
        {
        }

        public void Delete(string workspaceKey, string bundleCacheKey)
        {
            DeleteOperations.Add((workspaceKey, bundleCacheKey));
        }

        public void DeleteWorkspace(string workspaceKey)
        {
        }
    }

    private sealed class TestPreviewCacheStore : IPreviewCacheStore
    {
        private readonly List<PreviewCacheDescriptorDto> _descriptors;

        public TestPreviewCacheStore(IReadOnlyList<PreviewCacheDescriptorDto> descriptors)
        {
            _descriptors = descriptors.ToList();
        }

        public List<string> DeletedPreviewKeys { get; } = [];

        public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey) => null;

        public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey) =>
            _descriptors
                .Where(descriptor => string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.Ordinal))
                .ToArray();

        public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor) => null;

        public void Save(PreviewCachePayloadDto previewCachePayload)
        {
        }

        public void Delete(string workspaceKey, string previewCacheKey)
        {
            DeletedPreviewKeys.Add(previewCacheKey);
            _descriptors.RemoveAll(descriptor =>
                string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
                string.Equals(descriptor.PreviewCacheKey, previewCacheKey, StringComparison.Ordinal));
        }

        public void DeleteWorkspace(string workspaceKey)
        {
            _descriptors.RemoveAll(descriptor =>
                string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.Ordinal));
        }
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        public WorkspaceChangeSetDto? Load(string workspaceKey) => null;

        public void Save(WorkspaceChangeSetDto changeSet)
        {
        }

        public void Delete(string workspaceKey)
        {
        }
    }
}
