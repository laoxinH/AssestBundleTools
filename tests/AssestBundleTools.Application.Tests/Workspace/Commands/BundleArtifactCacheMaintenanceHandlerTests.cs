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
        var handler = new PruneBundleArtifactCacheHandler(factory);

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

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleArtifactStore _bundleArtifactStore;

        public TestWorkspaceDataStoreFactory(IBundleArtifactStore bundleArtifactStore)
        {
            _bundleArtifactStore = bundleArtifactStore;
        }

        public string? LastRootDirectoryPath { get; private set; }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null)
        {
            LastRootDirectoryPath = rootDirectoryPath;
            return _bundleArtifactStore;
        }

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) =>
            throw new NotSupportedException();

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
}
