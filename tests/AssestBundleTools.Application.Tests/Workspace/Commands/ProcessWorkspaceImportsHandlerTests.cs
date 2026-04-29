using System.IO;
using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static class ProcessWorkspaceImportsHandlerTests
{
    public static async Task HandleAsync_ShouldReuseExistingCache_WhenFingerprintAndVersionsStillMatch()
    {
        const string workspaceKey = "workspace-reuse";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\hero.ab";
        const string logicalPath = "hero.ab";
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePath,
            FileSizeInBytes: 4096,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero),
            ContentHash: "hash-hero");
        var bundleCacheKey = CreateBundleCacheKey(sourcePath);
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Reuse Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 10, 30, 0, TimeSpan.Zero),
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
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(
        [
            new BundleIndexCacheEntryDto(
                WorkspaceKey: workspaceKey,
                BundleCacheKey: bundleCacheKey,
                BundleId: "bundle.hero",
                DisplayName: "hero",
                LogicalPath: logicalPath,
                SourceFingerprint: sourceFingerprint,
                CachedAtUtc: new DateTimeOffset(2026, 4, 28, 10, 5, 0, TimeSpan.Zero),
                ParserVersion: "parser-1",
                ToolVersion: CreateCurrentToolVersion(),
                Dependencies: Array.Empty<string>(),
                Assets: Array.Empty<BundleIndexAssetCacheEntryDto>(),
                DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
                DecryptorVersion: "1.0.0")
        ]);
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var decryptor = new TestAssetBundleDecryptor(
            new AssetBundleDecryptorDescriptorDto(
                AssetBundleDecryptorIds.BuiltInPassThrough,
                "PassThrough",
                "1.0.0",
                AssetBundleDecryptorKind.BuiltIn,
                SupportsAutoDetection: false,
                IsFallback: true,
                Description: "fallback"));
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
        [
            new DiscoveredWorkspaceImportFileDto(sourcePath, logicalPath)
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePath] = sourceFingerprint
            });
        var reader = new TestAssetBundleReader();
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore),
            sourceDiscoveryService,
            fileSystemService,
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Matching cache entry should be reused successfully.");
        var processingResult = result.Value ?? throw new InvalidOperationException("Expected processing result.");
        TestAssert.Equal(1, processingResult.RegisteredSourceCount, "Registered source count should match metadata.");
        TestAssert.Equal(1, processingResult.DiscoveredFileCount, "Discovered file count should include the reused bundle.");
        TestAssert.Equal(0, processingResult.IndexedBundleCount, "Reuse path should not rebuild the bundle cache.");
        TestAssert.Equal(1, processingResult.ReusedBundleCount, "Reuse path should increment reused bundle count.");
        TestAssert.Equal(0, processingResult.SkippedFileCount, "Reuse path should not skip files.");
        TestAssert.Equal(0, processingResult.FailedFileCount, "Reuse path should not fail files.");
        TestAssert.Equal(0, processingResult.IssueDiagnostics.Count, "Reuse path should not emit issue diagnostics.");
        TestAssert.Equal(1, processingResult.CacheDiagnostics.Count, "Reuse path should emit one cache diagnostic.");
        TestAssert.Equal(WorkspaceImportCacheDecisionKind.Reused, processingResult.CacheDiagnostics[0].Kind, "Cache diagnostic should report a reuse hit.");
        TestAssert.Equal(bundleCacheKey, processingResult.CacheDiagnostics[0].BundleCacheKey, "Reuse diagnostic should target the existing cache entry.");
        TestAssert.Equal(0, bundleIndexCacheStore.DeleteOperations.Count, "Reuse path should not delete bundle cache artifacts.");
        TestAssert.Equal(0, previewCacheStore.DeleteOperations.Count, "Reuse path should not delete preview cache artifacts.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Reuse path should not rewrite metadata when decryptor stays unchanged.");
        TestAssert.Equal(1, fileSystemService.CreateFingerprintCallCount, "Reuse evaluation should still create the current source fingerprint.");
        TestAssert.Equal(0, decryptor.ProbeCallCount, "Explicit decryptor reuse path should not invoke probe.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Reuse hit should not invoke decryptor execution.");
        TestAssert.Equal(1, reader.SupportsCacheParserVersionCallCount, "Reuse decision should validate parser compatibility once.");
        TestAssert.Equal(0, reader.CanReadCallCount, "Reuse hit should not invoke reader probing.");
        TestAssert.Equal(0, reader.ReadCallCount, "Reuse hit should not invoke reader parsing.");
    }

    public static async Task HandleAsync_ShouldReportDecryptionFailure_WhenDecryptorExecutionFails()
    {
        const string workspaceKey = "workspace-failure";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\encrypted.ab";
        const string logicalPath = "encrypted.ab";
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePath,
            FileSizeInBytes: 8192,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero),
            ContentHash: "hash-encrypted");
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Failure Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 11, 30, 0, TimeSpan.Zero),
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
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 11, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var decryptor = new TestAssetBundleDecryptor(
            new AssetBundleDecryptorDescriptorDto(
                AssetBundleDecryptorIds.BuiltInPassThrough,
                "PassThrough",
                "1.0.0",
                AssetBundleDecryptorKind.BuiltIn,
                SupportsAutoDetection: false,
                IsFallback: true,
                Description: "fallback"))
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Failure(
                ApplicationErrors.BundleDecryptionExecutionFailed(AssetBundleDecryptorIds.BuiltInPassThrough)))
        };
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
        [
            new DiscoveredWorkspaceImportFileDto(sourcePath, logicalPath)
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePath] = sourceFingerprint
            });
        var reader = new TestAssetBundleReader();
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore),
            sourceDiscoveryService,
            fileSystemService,
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Per-file decryption failure should be reported inside the processing result.");
        var processingResult = result.Value ?? throw new InvalidOperationException("Expected processing result.");
        TestAssert.Equal(1, processingResult.RegisteredSourceCount, "Registered source count should match metadata.");
        TestAssert.Equal(1, processingResult.DiscoveredFileCount, "Discovered file count should include the failing source.");
        TestAssert.Equal(0, processingResult.IndexedBundleCount, "Decryption failure should not produce a bundle cache entry.");
        TestAssert.Equal(0, processingResult.ReusedBundleCount, "Decryption failure should not reuse cache.");
        TestAssert.Equal(0, processingResult.SkippedFileCount, "Decryption failure occurs before reader rejection.");
        TestAssert.Equal(1, processingResult.FailedFileCount, "Decryption failure should increment failed file count.");
        TestAssert.Equal(1, processingResult.CacheDiagnostics.Count, "Cache miss should still be diagnosed before decryption.");
        TestAssert.Equal(WorkspaceImportCacheDecisionKind.CacheMiss, processingResult.CacheDiagnostics[0].Kind, "Missing cache should be reported before decryption execution.");
        TestAssert.Equal(1, processingResult.IssueDiagnostics.Count, "Decryption failure should emit one issue diagnostic.");
        TestAssert.Equal(WorkspaceImportIssueKind.DecryptionFailed, processingResult.IssueDiagnostics[0].Kind, "Issue diagnostic kind should reflect decryption failure.");
        TestAssert.Equal(AssetBundleDecryptorIds.BuiltInPassThrough, processingResult.IssueDiagnostics[0].DecryptorId, "Issue diagnostic should record the decryptor id.");
        TestAssert.Equal(
            "Bundle.Decrypt.ExecutionFailed",
            TestAssert.Single(processingResult.IssueDiagnostics[0].Errors).Code,
            "Issue diagnostic should carry the original decryption error.");
        TestAssert.Equal(0, bundleIndexCacheStore.DeleteOperations.Count, "Decryption failure without prior cache should not delete bundle artifacts.");
        TestAssert.Equal(0, previewCacheStore.DeleteOperations.Count, "Decryption failure without prior cache should not delete preview artifacts.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Decryption failure should not rewrite metadata.");
        TestAssert.Equal(1, fileSystemService.CreateFingerprintCallCount, "Decryption path should create fingerprint once.");
        TestAssert.Equal(0, decryptor.ProbeCallCount, "Explicit decryptor path should not invoke probe.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Decryption path should invoke decryptor execution once.");
        TestAssert.Equal(0, reader.SupportsCacheParserVersionCallCount, "Cache miss should not evaluate parser compatibility.");
        TestAssert.Equal(0, reader.CanReadCallCount, "Decryption failure should stop before reader probing.");
        TestAssert.Equal(0, reader.ReadCallCount, "Decryption failure should stop before reader parsing.");
    }

    public static async Task HandleAsync_ShouldReportReaderRejected_WhenDecryptedBundleCannotBeRead()
    {
        const string workspaceKey = "workspace-reader-rejected";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\unsupported.ab";
        const string logicalPath = "unsupported.ab";
        const string resolvedBundlePath = @"C:\Temp\unsupported.decrypted";
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePath,
            FileSizeInBytes: 6144,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero),
            ContentHash: "hash-unsupported");
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Reader Rejected Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 12, 30, 0, TimeSpan.Zero),
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
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(decryptorDescriptor, sourceFingerprint, resolvedBundlePath)))
        };
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
        [
            new DiscoveredWorkspaceImportFileDto(sourcePath, logicalPath)
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePath] = sourceFingerprint
            });
        var reader = new TestAssetBundleReader
        {
            CanReadResult = false
        };
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore),
            sourceDiscoveryService,
            fileSystemService,
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Reader rejection should be reported inside the processing result.");
        var processingResult = result.Value ?? throw new InvalidOperationException("Expected processing result.");
        TestAssert.Equal(1, processingResult.RegisteredSourceCount, "Registered source count should match metadata.");
        TestAssert.Equal(1, processingResult.DiscoveredFileCount, "Discovered file count should include the rejected source.");
        TestAssert.Equal(0, processingResult.IndexedBundleCount, "Reader rejection should not produce a bundle cache entry.");
        TestAssert.Equal(0, processingResult.ReusedBundleCount, "Reader rejection should not reuse cache.");
        TestAssert.Equal(1, processingResult.SkippedFileCount, "Reader rejection should increment skipped file count.");
        TestAssert.Equal(0, processingResult.FailedFileCount, "Reader rejection should not count as a failed file.");
        TestAssert.Equal(1, processingResult.CacheDiagnostics.Count, "Cache miss should still be diagnosed before reader probing.");
        TestAssert.Equal(WorkspaceImportCacheDecisionKind.CacheMiss, processingResult.CacheDiagnostics[0].Kind, "Reader rejection scenario should start from cache miss.");
        TestAssert.Equal(1, processingResult.IssueDiagnostics.Count, "Reader rejection should emit one issue diagnostic.");
        TestAssert.Equal(WorkspaceImportIssueKind.ReaderRejected, processingResult.IssueDiagnostics[0].Kind, "Issue diagnostic kind should reflect reader rejection.");
        TestAssert.Equal(AssetBundleDecryptorIds.BuiltInPassThrough, processingResult.IssueDiagnostics[0].DecryptorId, "Issue diagnostic should record the decryptor id.");
        TestAssert.Equal(
            WorkspaceImportMessageTokens.ReaderNoRegisteredReaderRecognizedOutput,
            processingResult.IssueDiagnostics[0].DetailMessage?.MessageToken,
            "Reader rejection should emit the structured reader-rejected detail token.");
        TestAssert.Equal(
            resolvedBundlePath,
            TestAssert.Single(processingResult.IssueDiagnostics[0].DetailMessage?.Arguments ?? Array.Empty<string>()),
            "Reader rejection detail should carry the resolved bundle path.");
        TestAssert.Empty(processingResult.IssueDiagnostics[0].Errors, "Reader rejection should not attach AppError entries.");
        TestAssert.Equal(0, bundleIndexCacheStore.DeleteOperations.Count, "Reader rejection without prior cache should not delete bundle artifacts.");
        TestAssert.Equal(0, previewCacheStore.DeleteOperations.Count, "Reader rejection without prior cache should not delete preview artifacts.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Reader rejection should not rewrite metadata.");
        TestAssert.Equal(1, fileSystemService.CreateFingerprintCallCount, "Reader rejection path should create fingerprint once.");
        TestAssert.Equal(0, decryptor.ProbeCallCount, "Explicit decryptor path should not invoke probe.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Reader rejection path should invoke decryptor execution once.");
        TestAssert.Equal(0, reader.SupportsCacheParserVersionCallCount, "Cache miss should not evaluate parser compatibility.");
        TestAssert.Equal(1, reader.CanReadCallCount, "Reader rejection path should invoke reader probing once.");
        TestAssert.Equal(0, reader.ReadCallCount, "Reader rejection should stop before reader parsing.");
    }

    public static async Task HandleAsync_ShouldReportReadFailure_WhenReaderReturnsFailure()
    {
        const string workspaceKey = "workspace-read-failed";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\broken.ab";
        const string logicalPath = "broken.ab";
        const string resolvedBundlePath = @"C:\Temp\broken.decrypted";
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePath,
            FileSizeInBytes: 7168,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 13, 0, 0, TimeSpan.Zero),
            ContentHash: "hash-broken");
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Read Failed Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 13, 30, 0, TimeSpan.Zero),
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
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 13, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(decryptorDescriptor, sourceFingerprint, resolvedBundlePath)))
        };
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
        [
            new DiscoveredWorkspaceImportFileDto(sourcePath, logicalPath)
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePath] = sourceFingerprint
            });
        var reader = new TestAssetBundleReader
        {
            ReadResultFactory = _ => Task.FromResult(Result<AssetBundleReadResultDto>.Failure(
                ApplicationErrors.BundleReadFailed(resolvedBundlePath, "reader returned invalid payload")))
        };
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore),
            sourceDiscoveryService,
            fileSystemService,
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Reader failure should be reported inside the processing result.");
        var processingResult = result.Value ?? throw new InvalidOperationException("Expected processing result.");
        TestAssert.Equal(1, processingResult.RegisteredSourceCount, "Registered source count should match metadata.");
        TestAssert.Equal(1, processingResult.DiscoveredFileCount, "Discovered file count should include the failing source.");
        TestAssert.Equal(0, processingResult.IndexedBundleCount, "Read failure should not produce a bundle cache entry.");
        TestAssert.Equal(0, processingResult.ReusedBundleCount, "Read failure should not reuse cache.");
        TestAssert.Equal(0, processingResult.SkippedFileCount, "Read failure occurs after reader acceptance.");
        TestAssert.Equal(1, processingResult.FailedFileCount, "Read failure should increment failed file count.");
        TestAssert.Equal(1, processingResult.CacheDiagnostics.Count, "Cache miss should still be diagnosed before reader execution.");
        TestAssert.Equal(WorkspaceImportCacheDecisionKind.CacheMiss, processingResult.CacheDiagnostics[0].Kind, "Read failure scenario should start from cache miss.");
        TestAssert.Equal(1, processingResult.IssueDiagnostics.Count, "Read failure should emit one issue diagnostic.");
        TestAssert.Equal(WorkspaceImportIssueKind.ReadFailed, processingResult.IssueDiagnostics[0].Kind, "Issue diagnostic kind should reflect read failure.");
        TestAssert.Equal(AssetBundleDecryptorIds.BuiltInPassThrough, processingResult.IssueDiagnostics[0].DecryptorId, "Issue diagnostic should record the decryptor id.");
        TestAssert.Equal(
            "Bundle.Read.Failed",
            TestAssert.Single(processingResult.IssueDiagnostics[0].Errors).Code,
            "Issue diagnostic should carry the original read error.");
        TestAssert.Equal(0, bundleIndexCacheStore.DeleteOperations.Count, "Read failure without prior cache should not delete bundle artifacts.");
        TestAssert.Equal(0, previewCacheStore.DeleteOperations.Count, "Read failure without prior cache should not delete preview artifacts.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Read failure should not rewrite metadata.");
        TestAssert.Equal(1, fileSystemService.CreateFingerprintCallCount, "Read failure path should create fingerprint once.");
        TestAssert.Equal(0, decryptor.ProbeCallCount, "Explicit decryptor path should not invoke probe.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Read failure path should invoke decryptor execution once.");
        TestAssert.Equal(0, reader.SupportsCacheParserVersionCallCount, "Cache miss should not evaluate parser compatibility.");
        TestAssert.Equal(1, reader.CanReadCallCount, "Read failure path should invoke reader probing once.");
        TestAssert.Equal(1, reader.ReadCallCount, "Read failure path should invoke reader parsing once.");
    }

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
    }

    private sealed class TestWorkspaceMetadataStore : IWorkspaceMetadataStore
    {
        public TestWorkspaceMetadataStore(WorkspaceMetadataDto? metadata)
        {
            Metadata = metadata;
        }

        public WorkspaceMetadataDto? Metadata { get; private set; }

        public int SaveCallCount { get; private set; }

        public WorkspaceMetadataDto? Load(string workspaceKey) => Metadata;

        public void Save(WorkspaceMetadataDto metadata)
        {
            Metadata = metadata;
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
        private readonly List<BundleIndexCacheEntryDto> _cacheEntries;

        public TestBundleIndexCacheStore(IReadOnlyList<BundleIndexCacheEntryDto> cacheEntries)
        {
            _cacheEntries = cacheEntries.ToList();
        }

        public List<(string WorkspaceKey, string BundleCacheKey)> DeleteOperations { get; } = [];

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) =>
            _cacheEntries.FirstOrDefault(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) =>
            _cacheEntries
                .Where(entry => string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        public void Save(BundleIndexCacheEntryDto cacheEntry)
        {
            _cacheEntries.RemoveAll(entry =>
                string.Equals(entry.WorkspaceKey, cacheEntry.WorkspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.BundleCacheKey, cacheEntry.BundleCacheKey, StringComparison.OrdinalIgnoreCase));
            _cacheEntries.Add(cacheEntry);
        }

        public void Delete(string workspaceKey, string bundleCacheKey)
        {
            DeleteOperations.Add((workspaceKey, bundleCacheKey));
            _cacheEntries.RemoveAll(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));
        }

        public void DeleteWorkspace(string workspaceKey)
        {
            _cacheEntries.RemoveAll(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class TestPreviewCacheStore : IPreviewCacheStore
    {
        private readonly List<PreviewCacheDescriptorDto> _descriptors;

        public TestPreviewCacheStore(IReadOnlyList<PreviewCacheDescriptorDto> descriptors)
        {
            _descriptors = descriptors.ToList();
        }

        public List<(string WorkspaceKey, string PreviewCacheKey)> DeleteOperations { get; } = [];

        public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey) => null;

        public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey) =>
            _descriptors
                .Where(descriptor => string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor) => null;

        public void Save(PreviewCachePayloadDto previewCachePayload)
        {
        }

        public void Delete(string workspaceKey, string previewCacheKey)
        {
            DeleteOperations.Add((workspaceKey, previewCacheKey));
            _descriptors.RemoveAll(descriptor =>
                string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(descriptor.PreviewCacheKey, previewCacheKey, StringComparison.OrdinalIgnoreCase));
        }

        public void DeleteWorkspace(string workspaceKey)
        {
            _descriptors.RemoveAll(descriptor =>
                string.Equals(descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase));
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

    private sealed class TestWorkspaceImportSourceDiscoveryService : IWorkspaceImportSourceDiscoveryService
    {
        private readonly IReadOnlyList<DiscoveredWorkspaceImportFileDto> _discoveredFiles;

        public TestWorkspaceImportSourceDiscoveryService(IReadOnlyList<DiscoveredWorkspaceImportFileDto> discoveredFiles)
        {
            _discoveredFiles = discoveredFiles;
        }

        public IReadOnlyList<DiscoveredWorkspaceImportFileDto> Discover(
            IReadOnlyList<WorkspaceImportedSourceDto> importedSources,
            IReadOnlyList<string>? excludedSourcePaths = null) => _discoveredFiles;
    }

    private sealed class TestWorkspaceImportFileSystemService : IWorkspaceImportFileSystemService
    {
        private readonly IReadOnlyDictionary<string, WorkspaceFileFingerprintDto> _fingerprintsBySourcePath;

        public TestWorkspaceImportFileSystemService(
            IReadOnlyDictionary<string, WorkspaceFileFingerprintDto>? fingerprintsBySourcePath = null)
        {
            _fingerprintsBySourcePath = fingerprintsBySourcePath ??
                                        new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase);
        }

        public int CreateFingerprintCallCount { get; private set; }

        public WorkspaceImportPathKind ResolvePathKind(string path) => WorkspaceImportPathKind.File;

        public WorkspaceFileFingerprintDto CreateFingerprint(string sourcePath)
        {
            CreateFingerprintCallCount++;

            if (_fingerprintsBySourcePath.TryGetValue(sourcePath, out var fingerprint))
            {
                return fingerprint;
            }

            throw new InvalidOperationException($"Missing test fingerprint for '{sourcePath}'.");
        }
    }

    private sealed class TestAssetBundleSourceInspector : IAssetBundleSourceInspector
    {
        public Func<string, AssetBundleSourceInspectionResultDto>? InspectResultFactory { get; init; }

        public AssetBundleSourceInspectionResultDto Inspect(string sourcePath) =>
            InspectResultFactory?.Invoke(sourcePath) ??
            new AssetBundleSourceInspectionResultDto(AssetBundleSourceInspectionKind.Bundle);
    }

    private sealed class TestAssetBundleDecryptorRegistry : IAssetBundleDecryptorRegistry
    {
        private readonly IAssetBundleDecryptor _decryptor;

        public TestAssetBundleDecryptorRegistry(IAssetBundleDecryptor decryptor)
        {
            _decryptor = decryptor;
        }

        public IReadOnlyList<IAssetBundleDecryptor> GetAll() => [_decryptor];

        public bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor)
        {
            decryptor = _decryptor;
            return string.Equals(_decryptor.Descriptor.Id, decryptorId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class TestAssetBundleDecryptor : IAssetBundleDecryptor
    {
        public TestAssetBundleDecryptor(AssetBundleDecryptorDescriptorDto descriptor)
        {
            Descriptor = descriptor;
        }

        public AssetBundleDecryptorDescriptorDto Descriptor { get; }

        public Func<AssetBundleDecryptorProbeRequestDto, Task<AssetBundleDecryptorProbeResultDto>>? ProbeResultFactory { get; init; }

        public Func<AssetBundleDecryptionRequestDto, Task<Result<AssetBundleDecryptionResultDto>>>? DecryptResultFactory { get; init; }

        public int ProbeCallCount { get; private set; }

        public int DecryptCallCount { get; private set; }

        public Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
            AssetBundleDecryptorProbeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ProbeCallCount++;
            if (ProbeResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return ProbeResultFactory.Invoke(request);
        }

        public Task<Result<AssetBundleDecryptionResultDto>> DecryptAsync(
            AssetBundleDecryptionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            DecryptCallCount++;
            if (DecryptResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return DecryptResultFactory.Invoke(request);
        }
    }

    private sealed class TestAssetBundleReader : IAssetBundleReader
    {
        public string CacheParserVersion => "parser-1";

        public bool CanReadResult { get; init; } = true;

        public bool SupportsCacheParserVersionResult { get; init; } = true;

        public Func<AssetBundleReadRequestDto, Task<Result<AssetBundleReadResultDto>>>? ReadResultFactory { get; init; }

        public int SupportsCacheParserVersionCallCount { get; private set; }

        public int CanReadCallCount { get; private set; }

        public int ReadCallCount { get; private set; }

        public bool CanRead(string bundlePath)
        {
            CanReadCallCount++;
            return CanReadResult;
        }

        public bool SupportsCacheParserVersion(string parserVersion)
        {
            SupportsCacheParserVersionCallCount++;
            return SupportsCacheParserVersionResult;
        }

        public Task<Result<AssetBundleReadResultDto>> ReadAsync(
            AssetBundleReadRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ReadCallCount++;
            if (ReadResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return ReadResultFactory.Invoke(request);
        }
    }

    private static string CreateBundleCacheKey(string sourcePath)
    {
        var normalizedPath = Path.GetFullPath(sourcePath).Trim();
        var bytes = Encoding.UTF8.GetBytes(normalizedPath.ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static AssetBundleDecryptorDescriptorDto CreatePassThroughDecryptorDescriptor()
    {
        return new AssetBundleDecryptorDescriptorDto(
            AssetBundleDecryptorIds.BuiltInPassThrough,
            "PassThrough",
            "1.0.0",
            AssetBundleDecryptorKind.BuiltIn,
            SupportsAutoDetection: false,
            IsFallback: true,
            Description: "fallback");
    }

    private static AssetBundleDecryptionResultDto CreateDecryptionResult(
        AssetBundleDecryptorDescriptorDto decryptorDescriptor,
        WorkspaceFileFingerprintDto bundleFingerprint,
        string resolvedBundlePath)
    {
        return new AssetBundleDecryptionResultDto(
            Decryptor: decryptorDescriptor,
            ResolvedBundlePath: resolvedBundlePath,
            ProducedDerivedFile: false,
            BundleFingerprint: bundleFingerprint,
            DiagnosticMessage: null);
    }

    private static string CreateCurrentToolVersion()
    {
        var assemblyVersion = typeof(ProcessWorkspaceImportsHandler).Assembly.GetName().Version?.ToString()
                              ?? "1.0.0";
        return $"{assemblyVersion}+workspace-cache/preview-1";
    }
}
