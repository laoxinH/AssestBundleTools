using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
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
            new WorkspaceImportExecutionGate(),
            new WorkspaceImportExecutionOptionsResolver(),
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
        TestAssert.True(processingResult.PerformanceDiagnostic is not null, "Processing result should include stage timing diagnostics.");
        TestAssert.True(processingResult.PerformanceDiagnostic?.TotalDurationMs >= 0, "Total import duration should be recorded.");
        TestAssert.True(processingResult.PerformanceDiagnostic?.PlanningDurationMs >= 0, "Planning duration should be recorded.");
        TestAssert.True(processingResult.PerformanceDiagnostic?.ScreeningDurationMs >= 0, "Screening duration should be recorded.");
        TestAssert.Equal(0, processingResult.PerformanceDiagnostic?.ProbeDurationMs ?? -1, "Reuse path should not record probe duration.");
        TestAssert.True(processingResult.PerformanceDiagnostic?.WorkerDurationMs >= 0, "Worker duration should be recorded.");
        TestAssert.True(processingResult.PerformanceDiagnostic?.CommitDurationMs >= 0, "Commit duration should be recorded.");
        TestAssert.Equal(0, processingResult.PerformanceDiagnostic?.ProbeDecryptorCount ?? -1, "Reuse path should not record probe decryptor count.");
        TestAssert.Equal(0, processingResult.PerformanceDiagnostic?.ProbeEvaluationCount ?? -1, "Reuse path should not record probe file evaluation count.");
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

    public static async Task HandleAsync_ShouldPassBundleCacheKeyToDecryptorRequest()
    {
        const string workspaceKey = "workspace-success";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\hero.ab";
        const string logicalPath = "hero.ab";
        const string resolvedBundlePath = @"C:\Temp\hero.decrypted.ab";
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePath,
            FileSizeInBytes: 12288,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 11, 45, 0, TimeSpan.Zero),
            ContentHash: "hash-hero-success");
        var bundleFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: resolvedBundlePath,
            FileSizeInBytes: 12288,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 11, 46, 0, TimeSpan.Zero),
            ContentHash: "hash-hero-decrypted");
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Success Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 11, 50, 0, TimeSpan.Zero),
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
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 11, 45, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        AssetBundleDecryptionRequestDto? capturedRequest = null;
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = request =>
            {
                capturedRequest = request;
                return Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                    CreateDecryptionResult(decryptorDescriptor, bundleFingerprint, resolvedBundlePath)));
            }
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
            ReadResultFactory = request => Task.FromResult(Result<AssetBundleReadResultDto>.Success(
                new AssetBundleReadResultDto(
                    DisplayName: "hero",
                    LogicalPath: request.LogicalPath,
                    ParserVersion: "parser-1",
                    Dependencies: Array.Empty<string>(),
                    Assets:
                    [
                        new AssetBundleReadAssetDto(
                            AssetId: "asset-hero",
                            AssetName: "Hero",
                            AssetType: "Texture2D",
                            AssetPath: "Assets/Hero.png",
                            ContainerPath: null,
                            SizeInBytes: 1024,
                            PreferredDisplaySizeInBytes: 1024,
                            HasPreview: true,
                            CanExtract: true,
                            CanReplace: true,
                            SerializedFileName: "sharedassets0.assets",
                            PathId: 1)
                    ])))
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

        TestAssert.True(result.IsSuccess, "Successful import should complete.");
        var decryptionRequest = capturedRequest ?? throw new InvalidOperationException("Decrypt request should be captured.");
        TestAssert.Equal(CreateBundleCacheKey(sourcePath), decryptionRequest.BundleCacheKey, "Decrypt request should carry the stable bundle cache key.");
        TestAssert.Equal(workspaceKey, decryptionRequest.WorkspaceKey, "Decrypt request should preserve workspace key.");
        TestAssert.Equal(workspaceDataRootPath, decryptionRequest.WorkspaceDataRootPath, "Decrypt request should preserve workspace data root.");
        TestAssert.Equal(1, bundleIndexCacheStore.SavedEntries.Count, "Successful import should persist one bundle cache entry.");
    }

    public static async Task HandleAsync_ShouldPreserveSourceFileName_WhenReaderReadsNormalizedBundleArtifact()
    {
        const string workspaceKey = "workspace-display-name";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\ab\char_151_myrtle.ab";
        const string logicalPath = "ab/char_151_myrtle.ab";
        const string normalizedBundlePath =
            @"D:\WorkspaceData\cache\bundles\workspace-display-name\bundle-cache-001\normalized.bundle";
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePath,
            FileSizeInBytes: 12288,
            LastWriteTimeUtc: new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero),
            ContentHash: "hash-myrtle-source");
        var normalizedBundleFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: normalizedBundlePath,
            FileSizeInBytes: 12288,
            LastWriteTimeUtc: new DateTimeOffset(2026, 5, 17, 10, 1, 0, TimeSpan.Zero),
            ContentHash: "hash-myrtle-normalized");
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Display Name Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 9, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 5, 17, 9, 30, 0, TimeSpan.Zero),
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
                    RecordedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(decryptorDescriptor, normalizedBundleFingerprint, normalizedBundlePath)))
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
        AssetBundleReadRequestDto? capturedReadRequest = null;
        var reader = new TestAssetBundleReader
        {
            ReadResultFactory = request =>
            {
                capturedReadRequest = request;
                return Task.FromResult(Result<AssetBundleReadResultDto>.Success(
                    new AssetBundleReadResultDto(
                        DisplayName: "normalized.bundle",
                        LogicalPath: request.LogicalPath,
                        ParserVersion: "parser-1",
                        Dependencies: Array.Empty<string>(),
                        Assets: Array.Empty<AssetBundleReadAssetDto>())));
            }
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

        TestAssert.True(result.IsSuccess, "Successful import should complete.");
        var savedEntry = TestAssert.Single(bundleIndexCacheStore.SavedEntries);
        TestAssert.Equal("char_151_myrtle.ab", savedEntry.DisplayName, "Bundle display name should come from the imported source file, not the normalized artifact file.");
        TestAssert.Equal(logicalPath, savedEntry.LogicalPath, "Logical path should still preserve the discovered workspace-relative path.");
        TestAssert.Equal(sourcePath, savedEntry.SourceFingerprint.SourcePath, "Source fingerprint should continue to point at the imported source.");
        TestAssert.Equal(1, reader.ReadCallCount, "Import should still read the resolved normalized artifact once.");
        TestAssert.Equal(normalizedBundlePath, capturedReadRequest?.BundlePath ?? string.Empty, "Reader should still consume the normalized artifact path.");
    }

    public static async Task HandleAsync_ShouldProcessCandidateBundlesInParallel_WhenExecutionOptionsAllowMultipleWorkers()
    {
        const string workspaceKey = "workspace-parallel";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePathA = @"C:\Bundles\hero.ab";
        const string sourcePathB = @"C:\Bundles\villain.ab";
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Parallel Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero),
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
                    Path: sourcePathA,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero)),
                new WorkspaceImportedSourceDto(
                    Path: sourcePathB,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 12, 0, 5, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var sourceFingerprintA = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePathA,
            FileSizeInBytes: 2048,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 12, 1, 0, TimeSpan.Zero),
            ContentHash: "hash-hero-parallel");
        var sourceFingerprintB = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePathB,
            FileSizeInBytes: 3072,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 12, 1, 5, TimeSpan.Zero),
            ContentHash: "hash-villain-parallel");
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
        [
            new DiscoveredWorkspaceImportFileDto(sourcePathA, "hero.ab"),
            new DiscoveredWorkspaceImportFileDto(sourcePathB, "villain.ab")
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePathA] = sourceFingerprintA,
                [sourcePathB] = sourceFingerprintB
            });
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var activeDecryptions = 0;
        var maxConcurrentDecryptions = 0;
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = async request =>
            {
                var currentConcurrentDecryptions = Interlocked.Increment(ref activeDecryptions);
                RecordMaximumConcurrency(ref maxConcurrentDecryptions, currentConcurrentDecryptions);

                try
                {
                    await Task.Delay(120);
                    var resolvedBundlePath = $@"C:\Temp\{Path.GetFileNameWithoutExtension(request.SourcePath)}.decrypted.ab";
                    return Result<AssetBundleDecryptionResultDto>.Success(
                        CreateDecryptionResult(
                            decryptorDescriptor,
                            new WorkspaceFileFingerprintDto(
                                SourcePath: resolvedBundlePath,
                                FileSizeInBytes: currentConcurrentDecryptions == 1 ? 2048 : 3072,
                                LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 12, 2, 0, TimeSpan.Zero),
                                ContentHash: $"hash-{Path.GetFileNameWithoutExtension(request.SourcePath)}-decrypted"),
                            resolvedBundlePath));
                }
                finally
                {
                    Interlocked.Decrement(ref activeDecryptions);
                }
            }
        };
        var reader = new TestAssetBundleReader
        {
            ReadResultFactory = request => Task.FromResult(Result<AssetBundleReadResultDto>.Success(
                new AssetBundleReadResultDto(
                    DisplayName: Path.GetFileNameWithoutExtension(request.BundlePath),
                    LogicalPath: request.LogicalPath,
                    ParserVersion: "parser-1",
                    Dependencies: Array.Empty<string>(),
                    Assets:
                    [
                        new AssetBundleReadAssetDto(
                            AssetId: $"asset-{Path.GetFileNameWithoutExtension(request.BundlePath)}",
                            AssetName: Path.GetFileNameWithoutExtension(request.BundlePath),
                            AssetType: "Texture2D",
                            AssetPath: $"Assets/{Path.GetFileNameWithoutExtension(request.BundlePath)}.png",
                            ContainerPath: null,
                            SizeInBytes: 1024,
                            PreferredDisplaySizeInBytes: 1024,
                            HasPreview: true,
                            CanExtract: true,
                            CanReplace: true,
                            SerializedFileName: "sharedassets0.assets",
                            PathId: 1)
                    ])))
        };
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore),
            sourceDiscoveryService,
            fileSystemService,
            new WorkspaceImportExecutionGate(),
            new FixedWorkspaceImportExecutionOptionsResolver(2),
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Parallel import should still complete successfully.");
        TestAssert.Equal(2, maxConcurrentDecryptions, "When worker count is 2, bundle decrypt work should overlap.");
        TestAssert.Equal(2, result.Value?.IndexedBundleCount ?? 0, "Both bundles should be indexed successfully.");
        TestAssert.Equal(2, result.Value?.PerformanceDiagnostic?.BundleWorkerCount ?? 0, "Performance diagnostics should record the resolved worker count.");
    }

    public static async Task HandleAsync_ShouldReportBundleProgress_WhenProcessingCandidates()
    {
        const string workspaceKey = "workspace-progress";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePathA = @"C:\Bundles\hero.ab";
        const string sourcePathB = @"C:\Bundles\villain.ab";
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Progress Workspace",
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
                    Path: sourcePathA,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 12, 30, 0, TimeSpan.Zero)),
                new WorkspaceImportedSourceDto(
                    Path: sourcePathB,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 12, 30, 5, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
        [
            new DiscoveredWorkspaceImportFileDto(sourcePathA, "hero.ab"),
            new DiscoveredWorkspaceImportFileDto(sourcePathB, "villain.ab")
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePathA] = new WorkspaceFileFingerprintDto(
                    sourcePathA,
                    FileSizeInBytes: 2048,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 12, 31, 0, TimeSpan.Zero),
                    ContentHash: "hash-hero-progress"),
                [sourcePathB] = new WorkspaceFileFingerprintDto(
                    sourcePathB,
                    FileSizeInBytes: 3072,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 12, 31, 5, TimeSpan.Zero),
                    ContentHash: "hash-villain-progress")
            });
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = request =>
            {
                var resolvedBundlePath = $@"C:\Temp\{Path.GetFileNameWithoutExtension(request.SourcePath)}.ab";
                return Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                    CreateDecryptionResult(
                        decryptorDescriptor,
                        new WorkspaceFileFingerprintDto(
                            resolvedBundlePath,
                            FileSizeInBytes: 2048,
                            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 12, 32, 0, TimeSpan.Zero),
                            ContentHash: $"hash-{Path.GetFileNameWithoutExtension(request.SourcePath)}-resolved"),
                        resolvedBundlePath)));
            }
        };
        var reader = new TestAssetBundleReader
        {
            ReadResultFactory = request => Task.FromResult(Result<AssetBundleReadResultDto>.Success(
                new AssetBundleReadResultDto(
                    DisplayName: Path.GetFileNameWithoutExtension(request.BundlePath),
                    LogicalPath: request.LogicalPath,
                    ParserVersion: "parser-1",
                    Dependencies: Array.Empty<string>(),
                    Assets: Array.Empty<AssetBundleReadAssetDto>())))
        };
        var progressSnapshots = new List<WorkspaceImportProgressDto>();
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>()),
                new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>())),
            sourceDiscoveryService,
            fileSystemService,
            new WorkspaceImportExecutionGate(),
            new FixedWorkspaceImportExecutionOptionsResolver(1),
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            Progress: new ProgressRecorder(progressSnapshots)));

        TestAssert.True(result.IsSuccess, "Successful import should complete before progress assertions.");
        TestAssert.True(
            progressSnapshots.Any(progress => progress.Stage == WorkspaceImportProgressStage.Planning),
            "Progress should report the planning stage.");
        TestAssert.True(
            progressSnapshots.Any(progress =>
                progress.Stage == WorkspaceImportProgressStage.ProcessingBundles &&
                progress.CompletedBundleCount == 0 &&
                progress.TotalBundleCount == 2),
            "Progress should report the worker stage with total bundle count.");
        TestAssert.True(
            progressSnapshots.Any(progress =>
                progress.Stage == WorkspaceImportProgressStage.ProcessingBundles &&
                progress.CompletedBundleCount == 2 &&
                progress.TotalBundleCount == 2 &&
                string.Equals(progress.CurrentBundleLogicalPath, "villain.ab", StringComparison.Ordinal)),
            "Progress should report completed bundle count and current logical path.");
        TestAssert.True(
            progressSnapshots.Any(progress =>
                progress.Stage == WorkspaceImportProgressStage.Committing &&
                progress.CompletedBundleCount == 2 &&
                progress.TotalBundleCount == 2),
            "Progress should report commit stage after workers finish.");
    }

}
