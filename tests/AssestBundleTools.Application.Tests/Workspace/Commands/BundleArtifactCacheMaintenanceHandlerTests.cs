using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static class BundleArtifactCacheMaintenanceHandlerTests
{
    public static async Task GetUsage_ShouldReadUsage_FromRequestedWorkspaceDataRootAndWorkspace()
    {
        var store = new TestBundleArtifactStore(
            new BundleArtifactCacheUsageDto(
                TotalSizeInBytes: 42,
                ArtifactCount: 1,
                Entries:
                [
                    new BundleArtifactCacheEntryDto(
                        "workspace-a",
                        "bundle-a",
                        42,
                        new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2026, 5, 17, 10, 5, 0, TimeSpan.Zero),
                        IsMaterialized: true)
                ]));
        var factory = new TestWorkspaceDataStoreFactory(store);
        var handler = new GetBundleArtifactCacheUsageHandler(factory);

        var result = await handler.HandleAsync(new GetBundleArtifactCacheUsageQuery(
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            WorkspaceKey: "workspace-a"));

        TestAssert.True(result.IsSuccess, "Usage query should succeed.");
        TestAssert.Equal(@"D:\WorkspaceData", factory.LastRootDirectoryPath, "Handler should use requested data root.");
        TestAssert.Equal("workspace-a", store.LastUsageWorkspaceKey, "Handler should pass workspace scope to the store.");
        TestAssert.Equal(42L, result.Value?.TotalSizeInBytes ?? -1, "Usage result should expose store total size.");
    }

    public static async Task Prune_ShouldUseRequestedLimits_AndReturnPruneResult()
    {
        var prunedEntry = new BundleArtifactCacheEntryDto(
            "workspace-a",
            "bundle-old",
            128,
            new DateTimeOffset(2026, 5, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 17, 9, 5, 0, TimeSpan.Zero),
            IsMaterialized: true);
        var store = new TestBundleArtifactStore(
            usage: new BundleArtifactCacheUsageDto(128, 1, [prunedEntry]),
            pruneResult: new BundleArtifactPruneResultDto(
                SizeBeforeInBytes: 128,
                SizeAfterInBytes: 0,
                ArtifactCountBefore: 1,
                ArtifactCountAfter: 0,
                PrunedEntries: [prunedEntry]));
        var factory = new TestWorkspaceDataStoreFactory(store);
        var invalidator = new TestBundleArtifactSessionInvalidator();
        var handler = new PruneBundleArtifactCacheHandler(factory, invalidator);

        var result = await handler.HandleAsync(new PruneBundleArtifactCacheCommand(
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            WorkspaceKey: "workspace-a",
            MaximumSizeInBytes: 0,
            MaximumArtifactCount: 0));

        TestAssert.True(result.IsSuccess, "Prune command should succeed.");
        TestAssert.Equal(@"D:\WorkspaceData", factory.LastRootDirectoryPath, "Handler should use requested data root.");
        TestAssert.Equal("workspace-a", store.LastPruneRequest?.WorkspaceKey, "Handler should pass workspace scope.");
        TestAssert.Equal(0L, store.LastPruneRequest?.MaximumSizeInBytes ?? -1, "Handler should pass requested size limit.");
        TestAssert.Equal(0, store.LastPruneRequest?.MaximumArtifactCount ?? -1, "Handler should pass requested count limit.");
        TestAssert.Equal(128L, result.Value?.SizeBeforeInBytes ?? -1, "Result should expose pre-prune size.");
        TestAssert.Equal(0L, result.Value?.SizeAfterInBytes ?? -1, "Result should expose post-prune size.");
        var invalidatedArtifact = TestAssert.Single(invalidator.InvalidatedBundleArtifacts);
        TestAssert.Equal("workspace-a", invalidatedArtifact.WorkspaceKey, "Pruned artifacts should invalidate loaded sessions.");
        TestAssert.Equal("bundle-old", invalidatedArtifact.BundleCacheKey, "Loaded-session invalidation should target pruned bundle artifacts.");
    }

    public static async Task Prune_ShouldReturnValidationFailure_WhenLimitsAreNegative()
    {
        var store = new TestBundleArtifactStore(new BundleArtifactCacheUsageDto(0, 0, Array.Empty<BundleArtifactCacheEntryDto>()));
        var handler = new PruneBundleArtifactCacheHandler(new TestWorkspaceDataStoreFactory(store));

        var result = await handler.HandleAsync(new PruneBundleArtifactCacheCommand(
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            WorkspaceKey: "workspace-a",
            MaximumSizeInBytes: -1,
            MaximumArtifactCount: null));

        TestAssert.False(result.IsSuccess, "Negative limits should fail validation.");
        TestAssert.Equal(ErrorCategory.Validation, TestAssert.Single(result.Errors).Category, "Failure should be validation.");
        TestAssert.True(store.LastPruneRequest is null, "Invalid prune command should not reach the store.");
    }

    public static async Task GetWorkspaceCacheUsage_ShouldAggregateBundleArtifactAndPreviewUsage()
    {
        var bundleArtifactStore = new TestBundleArtifactStore(
            new BundleArtifactCacheUsageDto(
                TotalSizeInBytes: 128,
                ArtifactCount: 1,
                Entries: Array.Empty<BundleArtifactCacheEntryDto>()));
        var previewCacheStore = new TestPreviewCacheStore(
            usage: new PreviewCacheUsageDto(
                TotalSizeInBytes: 64,
                EntryCount: 2,
                Entries: Array.Empty<PreviewCacheEntryDto>()));
        var handler = new GetWorkspaceCacheUsageHandler(
            new TestWorkspaceDataStoreFactory(bundleArtifactStore, previewCacheStore));

        var result = await handler.HandleAsync(new GetWorkspaceCacheUsageQuery(
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            WorkspaceKey: "workspace-a"));

        TestAssert.True(result.IsSuccess, "Workspace cache usage query should succeed.");
        TestAssert.Equal(192L, result.Value?.TotalSizeInBytes ?? -1, "Total usage should include bundle artifacts and preview cache.");
        TestAssert.Equal(3, result.Value?.TotalEntryCount ?? -1, "Total entry count should include both cache families.");
        TestAssert.Equal("workspace-a", bundleArtifactStore.LastUsageWorkspaceKey, "Bundle artifact usage should be scoped.");
        TestAssert.Equal("workspace-a", previewCacheStore.LastUsageWorkspaceKey, "Preview cache usage should be scoped.");
    }

    public static async Task ClearWorkspaceCache_ShouldClearBundleArtifactsAndPreviewCache()
    {
        var prunedArtifact = new BundleArtifactCacheEntryDto(
            "workspace-a",
            "bundle-old",
            128,
            new DateTimeOffset(2026, 5, 17, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 17, 9, 5, 0, TimeSpan.Zero),
            IsMaterialized: true);
        var prunedPreview = new PreviewCacheEntryDto(
            "workspace-a",
            "preview-old",
            "bundle-old",
            "asset-001",
            "Texture2D",
            64,
            new DateTimeOffset(2026, 5, 17, 9, 10, 0, TimeSpan.Zero));
        var bundleArtifactStore = new TestBundleArtifactStore(
            usage: new BundleArtifactCacheUsageDto(128, 1, [prunedArtifact]),
            pruneResult: new BundleArtifactPruneResultDto(
                SizeBeforeInBytes: 128,
                SizeAfterInBytes: 0,
                ArtifactCountBefore: 1,
                ArtifactCountAfter: 0,
                PrunedEntries: [prunedArtifact]));
        var previewCacheStore = new TestPreviewCacheStore(
            usage: new PreviewCacheUsageDto(64, 1, [prunedPreview]),
            pruneResult: new PreviewCachePruneResultDto(
                SizeBeforeInBytes: 64,
                SizeAfterInBytes: 0,
                EntryCountBefore: 1,
                EntryCountAfter: 0,
                PrunedEntries: [prunedPreview]));
        var invalidator = new TestBundleArtifactSessionInvalidator();
        var handler = new ClearWorkspaceCacheHandler(
            new TestWorkspaceDataStoreFactory(bundleArtifactStore, previewCacheStore),
            invalidator);

        var result = await handler.HandleAsync(new ClearWorkspaceCacheCommand(
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            WorkspaceKey: "workspace-a"));

        TestAssert.True(result.IsSuccess, "Workspace cache clear should succeed.");
        TestAssert.Equal(192L, result.Value?.SizeBeforeInBytes ?? -1, "Clear result should aggregate pre-clear size.");
        TestAssert.Equal(0L, result.Value?.SizeAfterInBytes ?? -1, "Clear result should aggregate post-clear size.");
        TestAssert.Equal(2, result.Value?.EntryCountBefore ?? -1, "Clear result should aggregate pre-clear count.");
        TestAssert.Equal(0, result.Value?.EntryCountAfter ?? -1, "Clear result should aggregate post-clear count.");
        TestAssert.Equal(0L, bundleArtifactStore.LastPruneRequest?.MaximumSizeInBytes ?? -1, "Bundle artifacts should be cleared.");
        TestAssert.Equal(0L, previewCacheStore.LastPruneRequest?.MaximumSizeInBytes ?? -1, "Preview cache should be cleared.");
        var invalidatedArtifact = TestAssert.Single(invalidator.InvalidatedBundleArtifacts);
        TestAssert.Equal("bundle-old", invalidatedArtifact.BundleCacheKey, "Cleared bundle artifacts should invalidate loaded sessions.");
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleArtifactStore _bundleArtifactStore;
        private readonly IPreviewCacheStore? _previewCacheStore;

        public TestWorkspaceDataStoreFactory(
            IBundleArtifactStore bundleArtifactStore,
            IPreviewCacheStore? previewCacheStore = null)
        {
            _bundleArtifactStore = bundleArtifactStore;
            _previewCacheStore = previewCacheStore;
        }

        public string? LastRootDirectoryPath { get; private set; }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null)
        {
            LastRootDirectoryPath = rootDirectoryPath;
            return _bundleArtifactStore;
        }

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null)
        {
            LastRootDirectoryPath = rootDirectoryPath;
            return _previewCacheStore ?? throw new NotSupportedException();
        }

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleArtifactStore : IBundleArtifactStore
    {
        private readonly BundleArtifactCacheUsageDto _usage;
        private readonly BundleArtifactPruneResultDto _pruneResult;

        public TestBundleArtifactStore(
            BundleArtifactCacheUsageDto usage,
            BundleArtifactPruneResultDto? pruneResult = null)
        {
            _usage = usage;
            _pruneResult = pruneResult ?? new BundleArtifactPruneResultDto(0, 0, 0, 0, Array.Empty<BundleArtifactCacheEntryDto>());
        }

        public string? LastUsageWorkspaceKey { get; private set; }

        public BundleArtifactPruneRequestDto? LastPruneRequest { get; private set; }

        public BundleArtifactManifestDto? Load(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public string? ResolveBundlePath(BundleArtifactManifestDto manifest) => throw new NotSupportedException();

        public BundleArtifactManifestDto Save(BundleArtifactSaveRequestDto request) => throw new NotSupportedException();

        public BundleArtifactManifestDto UpdateLastAccessed(
            BundleArtifactManifestDto manifest,
            DateTimeOffset lastAccessedAtUtc) => throw new NotSupportedException();

        public BundleArtifactCacheUsageDto GetUsage(string? workspaceKey = null)
        {
            LastUsageWorkspaceKey = workspaceKey;
            return _usage;
        }

        public BundleArtifactPruneResultDto Prune(BundleArtifactPruneRequestDto request)
        {
            LastPruneRequest = request;
            return _pruneResult;
        }

        public void Delete(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class TestPreviewCacheStore : IPreviewCacheStore
    {
        private readonly PreviewCacheUsageDto _usage;
        private readonly PreviewCachePruneResultDto _pruneResult;

        public TestPreviewCacheStore(
            PreviewCacheUsageDto usage,
            PreviewCachePruneResultDto? pruneResult = null)
        {
            _usage = usage;
            _pruneResult = pruneResult ?? new PreviewCachePruneResultDto(0, 0, 0, 0, Array.Empty<PreviewCacheEntryDto>());
        }

        public string? LastUsageWorkspaceKey { get; private set; }

        public PreviewCachePruneRequestDto? LastPruneRequest { get; private set; }

        public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey) => throw new NotSupportedException();

        public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey) => throw new NotSupportedException();

        public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor) => throw new NotSupportedException();

        public PreviewCacheUsageDto GetUsage(string workspaceKey)
        {
            LastUsageWorkspaceKey = workspaceKey;
            return _usage;
        }

        public PreviewCachePruneResultDto Prune(PreviewCachePruneRequestDto request)
        {
            LastPruneRequest = request;
            return _pruneResult;
        }

        public void Save(PreviewCachePayloadDto previewCachePayload) => throw new NotSupportedException();

        public void Delete(string workspaceKey, string previewCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class TestBundleArtifactSessionInvalidator : IBundleArtifactSessionInvalidator
    {
        public List<(string WorkspaceKey, string BundleCacheKey)> InvalidatedBundleArtifacts { get; } = [];

        public List<string> InvalidatedWorkspaces { get; } = [];

        public void InvalidateBundleArtifact(string workspaceKey, string bundleCacheKey)
        {
            InvalidatedBundleArtifacts.Add((workspaceKey, bundleCacheKey));
        }

        public void InvalidateWorkspace(string workspaceKey)
        {
            InvalidatedWorkspaces.Add(workspaceKey);
        }
    }
}
