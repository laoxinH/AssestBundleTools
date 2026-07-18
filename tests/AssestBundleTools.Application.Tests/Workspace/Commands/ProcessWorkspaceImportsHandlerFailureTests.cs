using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static partial class ProcessWorkspaceImportsHandlerTests
{
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
            new WorkspaceImportExecutionGate(),
            new WorkspaceImportExecutionOptionsResolver(),
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
            new WorkspaceImportExecutionGate(),
            new WorkspaceImportExecutionOptionsResolver(),
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Reader rejection should be reported inside the processing result.");
        var processingResult = result.Value ?? throw new InvalidOperationException("Expected processing result.");
        TestAssert.Equal(0, processingResult.RegisteredSourceCount, "A stable reader-rejected direct file should be removed from registered sources.");
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
        TestAssert.Equal(1, metadataStore.SaveCallCount, "Stable reader rejection should remove the invalid direct import from metadata.");
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
            new WorkspaceImportExecutionGate(),
            new WorkspaceImportExecutionOptionsResolver(),
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
}
