using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldUpdateBundleArtifactLastAccess_WhenManifestIsReused()
    {
        const string artifactBundlePath = @"D:\WorkspaceData\cache\bundles\workspace-key-1\bundle-cache-001\normalized.bundle";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var bundleArtifactStore = new TestBundleArtifactStore(
        [
            new BundleArtifactManifestDto(
                WorkspaceKey: query.WorkspaceKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                ResolvedBundlePath: artifactBundlePath,
                IsMaterialized: true,
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                BundleFingerprint: new WorkspaceFileFingerprintDto(
                    SourcePath: artifactBundlePath,
                    FileSizeInBytes: 4096,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero),
                    ContentHash: null),
                DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
                DecryptorVersion: "1.0.0",
                NormalizerVersion: "bundle-artifact/1",
                CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero))
        ]);
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain; charset=utf-8",
                    Content: Encoding.UTF8.GetBytes("artifact payload"))))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore,
                bundleArtifactStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Preview cache miss should generate preview from reusable bundle artifact.");
        TestAssert.Equal(1, bundleArtifactStore.UpdateLastAccessedCallCount, "Reusable bundle artifact should update last access time.");
        TestAssert.Equal(
            bundleCacheEntry.BundleCacheKey,
            bundleArtifactStore.LastAccessedManifest?.BundleCacheKey,
            "Updated access manifest should target the reused bundle artifact.");
        TestAssert.True(
            bundleArtifactStore.LastAccessedManifest?.LastAccessedAtUtc is not null,
            "Reusable bundle artifact manifest should carry last access time after resolve.");
    }

    public static async Task HandleAsync_ShouldApplyAutoPrunePolicy_AfterArtifactSave()
    {
        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var bundleArtifactStore = new TestBundleArtifactStore();
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor())
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    ResolvedBundlePath: @"D:\ReadableBundles\hero.ab",
                    ProducedDerivedFile: true,
                    Decryptor: CreatePassThroughDecryptorDescriptor(),
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain; charset=utf-8",
                    Content: Encoding.UTF8.GetBytes("artifact payload"))))
        };
        var prunePolicy = new TestBundleArtifactCachePrunePolicy(64L);
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                new TestPreviewCacheStore(),
                bundleArtifactStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService(),
            bundleArtifactCachePrunePolicy: prunePolicy);

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Preview generation should succeed after saving the artifact.");
        TestAssert.Equal(1, bundleArtifactStore.SaveCallCount, "Cache miss should save one artifact.");
        TestAssert.Equal(1, bundleArtifactStore.PruneCallCount, "Auto prune policy should run after artifact save.");
        TestAssert.Equal(query.WorkspaceKey, bundleArtifactStore.LastPruneRequest?.WorkspaceKey, "Auto prune should target the current workspace.");
        TestAssert.Equal(64L, bundleArtifactStore.LastPruneRequest?.MaximumSizeInBytes ?? -1, "Auto prune should use the configured capacity limit.");
        TestAssert.Equal(
            bundleCacheEntry.BundleCacheKey,
            TestAssert.Single(bundleArtifactStore.LastPruneRequest?.ProtectedBundleCacheKeys ?? []),
            "Auto prune should protect the artifact that was just generated.");
    }

    private sealed class TestBundleArtifactCachePrunePolicy : IBundleArtifactCachePrunePolicy
    {
        private readonly long _maximumSizeInBytes;

        public TestBundleArtifactCachePrunePolicy(long maximumSizeInBytes)
        {
            _maximumSizeInBytes = maximumSizeInBytes;
        }

        public BundleArtifactPruneRequestDto? CreateRequest(string workspaceKey, string protectedBundleCacheKey) =>
            new(
                WorkspaceKey: workspaceKey,
                MaximumSizeInBytes: _maximumSizeInBytes,
                MaximumArtifactCount: null,
                ProtectedBundleCacheKeys: [protectedBundleCacheKey]);
    }
}
