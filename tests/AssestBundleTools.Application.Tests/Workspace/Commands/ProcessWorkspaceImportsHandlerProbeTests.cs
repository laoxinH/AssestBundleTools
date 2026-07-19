using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static partial class ProcessWorkspaceImportsHandlerTests
{
    public static async Task HandleAsync_ShouldSelectCodecPerBundle_AndRecordProbeTelemetry()
    {
        const string workspaceKey = "workspace-probe";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\encrypted.ab";
        const string logicalPath = "encrypted.ab";
        const string standardSourcePath = @"C:\Bundles\standard.ab";
        const string standardLogicalPath = "standard.ab";
        const string unrelatedSourcePath = @"C:\Bundles\cover.png";
        const string unrelatedLogicalPath = "cover.png";
        const string resolvedBundlePath = @"C:\Temp\encrypted.decrypted.ab";
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePath,
            FileSizeInBytes: 4096,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 14, 0, 0, TimeSpan.Zero),
            ContentHash: "hash-encrypted-source");
        var bundleFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: resolvedBundlePath,
            FileSizeInBytes: 4096,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 14, 1, 0, TimeSpan.Zero),
            ContentHash: "hash-encrypted-decrypted");
        var standardFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: standardSourcePath,
            FileSizeInBytes: 2048,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 14, 2, 0, TimeSpan.Zero),
            ContentHash: "hash-standard-source");
        var unrelatedFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: unrelatedSourcePath,
            FileSizeInBytes: 128,
            LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 14, 3, 0, TimeSpan.Zero),
            ContentHash: "hash-unrelated-source");
        var metadataStore = new TestWorkspaceMetadataStore(
            CreateAutoDetectWorkspaceMetadata(
                workspaceKey,
                "Probe Workspace",
                sourcePath,
                standardSourcePath,
                unrelatedSourcePath));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var recognitionCacheStore = new TestWorkspaceImportRecognitionCacheStore();
        var autoDecryptorDescriptor = CreateAutoDetectDecryptorDescriptor("test.auto", "Test Auto");
        var autoDecryptor = new TestAssetBundleDecryptor(autoDecryptorDescriptor)
        {
            ProbeResultFactory = request => Task.FromResult(new AssetBundleDecryptorProbeResultDto(
                IsSuccessful: true,
                IsMatch: string.Equals(request.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase),
                Confidence: string.Equals(request.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase) ? 90 : 0,
                DiagnosticMessage: null)),
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(autoDecryptorDescriptor, bundleFingerprint, resolvedBundlePath)))
        };
        var fallbackDescriptor = CreatePassThroughDecryptorDescriptor();
        var fallbackDecryptor = new TestAssetBundleDecryptor(fallbackDescriptor)
        {
            DecryptResultFactory = request => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(fallbackDescriptor, standardFingerprint, request.SourcePath)))
        };
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
        [
            new DiscoveredWorkspaceImportFileDto(sourcePath, logicalPath),
            new DiscoveredWorkspaceImportFileDto(standardSourcePath, standardLogicalPath),
            new DiscoveredWorkspaceImportFileDto(unrelatedSourcePath, unrelatedLogicalPath)
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePath] = sourceFingerprint,
                [standardSourcePath] = standardFingerprint,
                [unrelatedSourcePath] = unrelatedFingerprint
            });
        var inspector = new TestAssetBundleSourceInspector
        {
            InspectResultFactory = path => string.Equals(path, unrelatedSourcePath, StringComparison.OrdinalIgnoreCase)
                ? new AssetBundleSourceInspectionResultDto(
                    AssetBundleSourceInspectionKind.Undetermined,
                    CanUsePassThrough: false)
                : new AssetBundleSourceInspectionResultDto(
                    string.Equals(path, standardSourcePath, StringComparison.OrdinalIgnoreCase)
                        ? AssetBundleSourceInspectionKind.Bundle
                        : AssetBundleSourceInspectionKind.Undetermined)
        };
        var reader = new TestAssetBundleReader
        {
            ReadResultFactory = request => Task.FromResult(Result<AssetBundleReadResultDto>.Success(
                new AssetBundleReadResultDto(
                    DisplayName: "encrypted",
                    LogicalPath: request.LogicalPath,
                    ParserVersion: "parser-1",
                    Dependencies: Array.Empty<string>(),
                    Assets: Array.Empty<AssetBundleReadAssetDto>())))
        };
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore, recognitionCacheStore),
            sourceDiscoveryService,
            fileSystemService,
            new WorkspaceImportExecutionGate(),
            new WorkspaceImportExecutionOptionsResolver(),
            inspector,
            new TestAssetBundleDecryptorRegistry(autoDecryptor, fallbackDecryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Auto-detection import should complete when a probe candidate matches.");
        var processingResult = result.Value ?? throw new InvalidOperationException("Expected processing result.");
        TestAssert.True(processingResult.UsedAutoDetection, "Import should report auto-detection usage.");
        TestAssert.Equal(AssetBundleDecryptorIds.BuiltInAutoDetect, processingResult.ResolvedDecryptorId, "Processing result should retain the workspace auto-detection strategy.");
        TestAssert.Equal(3, autoDecryptor.ProbeCallCount, "Auto-detection should probe every source before applying pass-through restrictions.");
        TestAssert.Equal(1, autoDecryptor.DecryptCallCount, "Matched auto decryptor should process the source.");
        TestAssert.Equal(1, fallbackDecryptor.DecryptCallCount, "Only the standard bundle with no codec match should use pass-through.");
        TestAssert.Equal(2, processingResult.IndexedBundleCount, "Both matched and pass-through sources should be indexed.");
        TestAssert.Equal(1, processingResult.ScreenedOutFileCount, "A known non-bundle source should be screened out only after no codec matches.");
        TestAssert.Equal(1, processingResult.NewlyScreenedOutFileCount, "The first recognition pass should report one newly screened-out source.");
        TestAssert.Equal(1, metadataStore.SaveCallCount, "A stable invalid direct file should be removed from registered import sources.");
        TestAssert.Equal(2, metadataStore.Metadata?.ImportedSources?.Count ?? -1, "Only recognized bundle sources should remain directly registered.");
        TestAssert.Equal(AssetBundleDecryptorIds.BuiltInAutoDetect, metadataStore.Metadata?.AssetBundleDecryptorId, "Workspace metadata should retain auto detection.");
        TestAssert.Equal(
            autoDecryptorDescriptor.Id,
            bundleIndexCacheStore.SavedEntries.Single(entry => string.Equals(
                entry.SourceFingerprint.SourcePath,
                sourcePath,
                StringComparison.OrdinalIgnoreCase)).DecryptorId,
            "Encrypted bundle index should record its matched codec for later export encoding.");
        TestAssert.Equal(
            fallbackDescriptor.Id,
            bundleIndexCacheStore.SavedEntries.Single(entry => string.Equals(
                entry.SourceFingerprint.SourcePath,
                standardSourcePath,
                StringComparison.OrdinalIgnoreCase)).DecryptorId,
            "Standard bundle index should record pass-through as its actual codec.");
        TestAssert.True(processingResult.PerformanceDiagnostic is not null, "Processing result should include performance diagnostics.");
        TestAssert.True(processingResult.PerformanceDiagnostic?.ProbeDurationMs >= 0, "Probe duration should be recorded.");
        TestAssert.Equal(1, processingResult.PerformanceDiagnostic?.ProbeDecryptorCount ?? 0, "Probe telemetry should count the evaluated decryptor.");
        TestAssert.Equal(3, processingResult.PerformanceDiagnostic?.ProbeEvaluationCount ?? 0, "Probe telemetry should count every evaluated source, including the rejected PNG.");
        TestAssert.Equal(1, processingResult.AutoDetectionDiagnostic?.ProbeDiagnostics.Count ?? 0, "Auto-detection diagnostic should include the probe summary.");
        TestAssert.True(processingResult.AutoDetectionDiagnostic?.ProbeDiagnostics[0].WasSelected ?? false, "Probe diagnostic should mark the selected decryptor.");

        var secondResult = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(secondResult.IsSuccess, "An unchanged workspace should reuse persisted recognition results.");
        TestAssert.Equal(3, autoDecryptor.ProbeCallCount, "Unchanged sources must not start additional codec probes.");
        TestAssert.Equal(3, secondResult.Value?.PerformanceDiagnostic?.RecognitionCacheHitCount ?? -1, "All unchanged sources should hit the recognition cache.");
        TestAssert.Equal(0, secondResult.Value?.PerformanceDiagnostic?.ProbeInputFileCount ?? -1, "No unchanged source should enter recognition again.");
        TestAssert.Equal(0, secondResult.Value?.PerformanceDiagnostic?.ProbeEvaluationCount ?? -1, "The second pass should perform zero probe evaluations.");
        TestAssert.Equal(1, secondResult.Value?.ScreenedOutFileCount ?? -1, "The refresh summary should retain the cached screened-out total for diagnostics.");
        TestAssert.Equal(0, secondResult.Value?.NewlyScreenedOutFileCount ?? -1, "A cached screened-out result must not be reported as a new issue.");
    }

    public static async Task HandleAsync_ShouldKeepAutoDetection_WhenProbeExecutionFails()
    {
        const string workspaceKey = "workspace-probe-failure";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\probe-failure.ab";
        const string logicalPath = "probe-failure.ab";
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: sourcePath,
            FileSizeInBytes: 1024,
            LastWriteTimeUtc: new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero),
            ContentHash: "hash-probe-failure");
        var metadataStore = new TestWorkspaceMetadataStore(
            CreateAutoDetectWorkspaceMetadata(workspaceKey, "Probe Failure Workspace", sourcePath));
        var recognitionCacheStore = new TestWorkspaceImportRecognitionCacheStore();
        var autoDecryptor = new TestAssetBundleDecryptor(
            CreateAutoDetectDecryptorDescriptor("test.auto.failure", "Test Auto Failure"))
        {
            ProbeResultFactory = _ => Task.FromResult(new AssetBundleDecryptorProbeResultDto(
                IsSuccessful: false,
                IsMatch: false,
                Confidence: 0,
                DiagnosticMessage: null))
        };
        var fallbackDescriptor = CreatePassThroughDecryptorDescriptor();
        var fallbackDecryptor = new TestAssetBundleDecryptor(fallbackDescriptor)
        {
            DecryptResultFactory = request => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(fallbackDescriptor, sourceFingerprint, request.SourcePath)))
        };
        var reader = new TestAssetBundleReader
        {
            ReadResultFactory = request => Task.FromResult(Result<AssetBundleReadResultDto>.Success(
                new AssetBundleReadResultDto(
                    DisplayName: "probe-failure",
                    LogicalPath: request.LogicalPath,
                    ParserVersion: "parser-1",
                    Dependencies: Array.Empty<string>(),
                    Assets: Array.Empty<AssetBundleReadAssetDto>())))
        };
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>()),
                new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>()),
                recognitionCacheStore),
            new TestWorkspaceImportSourceDiscoveryService(
            [
                new DiscoveredWorkspaceImportFileDto(sourcePath, logicalPath)
            ]),
            new TestWorkspaceImportFileSystemService(
                new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
                {
                    [sourcePath] = sourceFingerprint
                }),
            new WorkspaceImportExecutionGate(),
            new WorkspaceImportExecutionOptionsResolver(),
            new TestAssetBundleSourceInspector
            {
                InspectResultFactory = _ => new AssetBundleSourceInspectionResultDto(
                    AssetBundleSourceInspectionKind.Undetermined)
            },
            new TestAssetBundleDecryptorRegistry(autoDecryptor, fallbackDecryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Import may temporarily use fallback when a probe process fails.");
        TestAssert.Equal(
            AssetBundleDecryptorIds.BuiltInAutoDetect,
            result.Value?.ResolvedDecryptorId,
            "The processing result should continue to report the auto-detection strategy.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Transient probe failure must not persist fallback selection.");
        TestAssert.Equal(
            AssetBundleDecryptorIds.BuiltInAutoDetect,
            metadataStore.Metadata?.AssetBundleDecryptorId,
            "Workspace metadata should keep auto detection so a later refresh probes again.");

        var secondResult = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(secondResult.IsSuccess, "A later refresh should retry after a transient probe failure.");
        TestAssert.Equal(2, autoDecryptor.ProbeCallCount, "Transient probe failures must not be persisted as recognition cache hits.");
        TestAssert.Equal(0, recognitionCacheStore.Entries.Count, "Transient probe failure results should not enter the recognition cache.");
    }

    public static async Task HandleAsync_ShouldProbeEverySource_WhenDecryptorIsExplicitlyConfigured()
    {
        const string workspaceKey = "workspace-explicit-probe";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string matchedPath = @"C:\Bundles\matched-private-data";
        const string rejectedPath = @"C:\Bundles\unrelated.ab";
        var matchedFingerprint = new WorkspaceFileFingerprintDto(
            matchedPath,
            4096,
            new DateTimeOffset(2026, 7, 11, 1, 0, 0, TimeSpan.Zero),
            "hash-matched");
        var rejectedFingerprint = new WorkspaceFileFingerprintDto(
            rejectedPath,
            128,
            new DateTimeOffset(2026, 7, 11, 1, 1, 0, TimeSpan.Zero),
            "hash-rejected");
        var decryptorDescriptor = CreateAutoDetectDecryptorDescriptor("test.explicit", "Explicit Test Codec");
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            ProbeResultFactory = request => Task.FromResult(new AssetBundleDecryptorProbeResultDto(
                IsSuccessful: true,
                IsMatch: string.Equals(request.SourcePath, matchedPath, StringComparison.OrdinalIgnoreCase),
                Confidence: string.Equals(request.SourcePath, matchedPath, StringComparison.OrdinalIgnoreCase) ? 100 : 0,
                DiagnosticMessage: null)),
            DecryptResultFactory = request => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(decryptorDescriptor, matchedFingerprint, request.SourcePath)))
        };
        var metadataStore = new TestWorkspaceMetadataStore(
            CreateWorkspaceMetadata(
                workspaceKey,
                "Explicit Probe Workspace",
                decryptorDescriptor.Id,
                matchedPath,
                rejectedPath));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(
                bundleIndexCacheStore,
                new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>())),
            new TestWorkspaceImportSourceDiscoveryService(
            [
                new DiscoveredWorkspaceImportFileDto(matchedPath, "matched-private-data"),
                new DiscoveredWorkspaceImportFileDto(rejectedPath, "unrelated.ab")
            ]),
            new TestWorkspaceImportFileSystemService(
                new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
                {
                    [matchedPath] = matchedFingerprint,
                    [rejectedPath] = rejectedFingerprint
                }),
            new WorkspaceImportExecutionGate(),
            new WorkspaceImportExecutionOptionsResolver(),
            new TestAssetBundleSourceInspector
            {
                InspectResultFactory = path => new AssetBundleSourceInspectionResultDto(
                    string.Equals(path, rejectedPath, StringComparison.OrdinalIgnoreCase)
                        ? AssetBundleSourceInspectionKind.Bundle
                        : AssetBundleSourceInspectionKind.Undetermined)
            },
            new TestAssetBundleDecryptorRegistry(decryptor),
            new TestAssetBundleReader
            {
                ReadResultFactory = request => Task.FromResult(Result<AssetBundleReadResultDto>.Success(
                    new AssetBundleReadResultDto(
                        "matched",
                        request.LogicalPath,
                        "parser-1",
                        Array.Empty<string>(),
                        Array.Empty<AssetBundleReadAssetDto>())))
            });

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            workspaceKey,
            workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Explicit codec screening should complete successfully.");
        TestAssert.Equal(2, decryptor.ProbeCallCount, "The explicit codec must probe both extension and unresolved candidates.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Only the source matched by the explicit codec should be decrypted.");
        TestAssert.Equal(1, result.Value?.IndexedBundleCount ?? -1, "Only the matched source should be indexed.");
        TestAssert.Equal(1, result.Value?.ScreenedOutFileCount ?? -1, "The unrelated .ab source should be screened out.");
        TestAssert.Equal(1, bundleIndexCacheStore.SavedEntries.Count, "Only one bundle index should be persisted.");
    }

    private static WorkspaceMetadataDto CreateAutoDetectWorkspaceMetadata(
        string workspaceKey,
        string displayName,
        params string[] sourcePaths) =>
        CreateWorkspaceMetadata(
            workspaceKey,
            displayName,
            AssetBundleDecryptorIds.BuiltInAutoDetect,
            sourcePaths);

    private static WorkspaceMetadataDto CreateWorkspaceMetadata(
        string workspaceKey,
        string displayName,
        string decryptorId,
        params string[] sourcePaths) =>
        new(
            WorkspaceKey: workspaceKey,
            DisplayName: displayName,
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 30, 0, TimeSpan.Zero),
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
            ImportedSources: sourcePaths
                .Select(sourcePath => new WorkspaceImportedSourceDto(
                    Path: sourcePath,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 0, 0, TimeSpan.Zero)))
                .ToArray(),
            AssetBundleDecryptorId: decryptorId,
            ExcludedBundleSourcePaths: Array.Empty<string>());
}
