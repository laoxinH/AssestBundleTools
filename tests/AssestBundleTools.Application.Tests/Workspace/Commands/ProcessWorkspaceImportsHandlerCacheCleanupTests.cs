using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static partial class ProcessWorkspaceImportsHandlerTests
{
    public static async Task HandleAsync_ShouldDeleteStaleBundleAndPreviewCaches_WhenRegisteredSourcesNoLongerDiscoverFiles()
    {
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\removed.ab";
        const string bundleCacheKey = "cache-removed";
        const string previewCacheKey = "preview-removed";
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Example Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 9, 30, 0, TimeSpan.Zero),
            LastBundleId: null,
            LastAssetId: null,
            LastOutputDirectory: null,
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: null,
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs: Array.Empty<WorkspaceMetadataBundleTabDto>(),
            ActiveBundleTabId: null,
            ImportedSources:
            [
                new WorkspaceImportedSourceDto(
                    Path: sourcePath,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 9, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(
        [
            new BundleIndexCacheEntryDto(
                WorkspaceKey: workspaceKey,
                BundleCacheKey: bundleCacheKey,
                BundleId: "bundle.removed",
                DisplayName: "removed",
                LogicalPath: "removed.ab",
                SourceFingerprint: new WorkspaceFileFingerprintDto(
                    SourcePath: sourcePath,
                    FileSizeInBytes: 512,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 8, 0, 0, TimeSpan.Zero),
                    ContentHash: "hash-removed"),
                CachedAtUtc: new DateTimeOffset(2026, 4, 28, 8, 5, 0, TimeSpan.Zero),
                ParserVersion: "parser-1",
                ToolVersion: "tool-1",
                Dependencies: Array.Empty<string>(),
                Assets: Array.Empty<BundleIndexAssetCacheEntryDto>(),
                DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
                DecryptorVersion: "1.0.0")
        ]);
        var previewCacheStore = new TestPreviewCacheStore(
        [
            new PreviewCacheDescriptorDto(
                WorkspaceKey: workspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheKey,
                AssetId: "asset-001",
                PreviewKind: "AudioClip",
                ContentType: "audio/wav",
                RelativePayloadPath: "payloads/preview-removed.bin",
                SourceFingerprint: new WorkspaceFileFingerprintDto(
                    SourcePath: sourcePath,
                    FileSizeInBytes: 512,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 8, 0, 0, TimeSpan.Zero),
                    ContentHash: "hash-removed"),
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 8, 6, 0, TimeSpan.Zero),
                GeneratorVersion: "generator-1.0.0",
                PreviewInfoItems:
                [
                    new AssetPreviewInfoItemDto(
                        Key: AssetPreviewInfoKey.AudioContainer,
                        ValueKind: AssetPreviewInfoValueKind.Text,
                        TextValue: "wav")
                ])
        ]);
        var decryptor = new TestAssetBundleDecryptor(
            new AssetBundleDecryptorDescriptorDto(
                AssetBundleDecryptorIds.BuiltInPassThrough,
                "PassThrough",
                "1.0.0",
                AssetBundleDecryptorKind.BuiltIn,
                SupportsAutoDetection: false,
                IsFallback: true,
                Description: "fallback"));
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(Array.Empty<DiscoveredWorkspaceImportFileDto>());
        var reader = new TestAssetBundleReader();
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore),
            sourceDiscoveryService,
            new TestWorkspaceImportFileSystemService(),
            new WorkspaceImportExecutionGate(),
            new WorkspaceImportExecutionOptionsResolver(),
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Processing imports with stale caches should still succeed.");
        var processingResult = result.Value ?? throw new InvalidOperationException("Expected processing result.");
        TestAssert.Equal(workspaceKey, processingResult.WorkspaceKey, "Workspace key should round-trip.");
        TestAssert.Equal(1, processingResult.RegisteredSourceCount, "Registered source count should reflect current metadata.");
        TestAssert.Equal(0, processingResult.DiscoveredFileCount, "No files should be discovered in the stale-cache cleanup scenario.");
        TestAssert.Equal(0, processingResult.IndexedBundleCount, "No bundles should be indexed when discovery is empty.");
        TestAssert.Equal(0, processingResult.ReusedBundleCount, "No bundles should be reused when discovery is empty.");
        TestAssert.Equal(0, processingResult.FailedFileCount, "Cleanup-only flow should not report failures.");
        TestAssert.Equal(0, processingResult.IssueDiagnostics.Count, "Cleanup-only flow should not introduce issue diagnostics.");
        TestAssert.Equal(1, processingResult.CacheDiagnostics.Count, "Stale cache cleanup should emit one cache diagnostic.");
        TestAssert.Equal(WorkspaceImportCacheDecisionKind.SourceRemoved, processingResult.CacheDiagnostics[0].Kind, "Stale cache cleanup should be diagnosed as source removed.");
        TestAssert.Equal(sourcePath, processingResult.CacheDiagnostics[0].SourcePath, "Cache diagnostic should point at the removed source.");
        TestAssert.Equal(bundleCacheKey, processingResult.CacheDiagnostics[0].BundleCacheKey, "Cache diagnostic should target the stale cache entry.");
        TestAssert.Equal(1, bundleIndexCacheStore.DeleteOperations.Count, "Stale bundle index cache should be deleted.");
        TestAssert.Equal(bundleCacheKey, bundleIndexCacheStore.DeleteOperations[0].BundleCacheKey, "Deleted cache key should match the stale bundle.");
        TestAssert.Equal(1, previewCacheStore.DeleteOperations.Count, "Matching preview cache should be deleted together with the stale bundle.");
        TestAssert.Equal(previewCacheKey, previewCacheStore.DeleteOperations[0].PreviewCacheKey, "Deleted preview cache key should match the stale bundle previews.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Cleanup-only flow should not rewrite metadata.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Stale-cache cleanup should not invoke decryptor execution.");
        TestAssert.Equal(0, decryptor.ProbeCallCount, "Stale-cache cleanup should not invoke decryptor probing.");
        TestAssert.Equal(0, reader.CanReadCallCount, "Stale-cache cleanup should not invoke reader probing.");
        TestAssert.Equal(0, reader.ReadCallCount, "Stale-cache cleanup should not invoke reader parsing.");
        TestAssert.Equal(1, sourceDiscoveryService.IgnoredDirectoryPaths.Count, "Workspace discovery should receive one reserved data root.");
        TestAssert.Equal(workspaceDataRootPath, sourceDiscoveryService.IgnoredDirectoryPaths[0], "Workspace data root should be excluded from import discovery.");
    }
}
