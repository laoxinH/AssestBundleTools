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
        var metadataStore = new TestWorkspaceMetadataStore(
            CreateAutoDetectWorkspaceMetadata(
                workspaceKey,
                "Probe Workspace",
                sourcePath,
                standardSourcePath));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
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
            new DiscoveredWorkspaceImportFileDto(standardSourcePath, standardLogicalPath)
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePath] = sourceFingerprint,
                [standardSourcePath] = standardFingerprint
            });
        var inspector = new TestAssetBundleSourceInspector
        {
            InspectResultFactory = path => new AssetBundleSourceInspectionResultDto(
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
            new TestWorkspaceDataStoreFactory(bundleIndexCacheStore, previewCacheStore),
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
        TestAssert.Equal(2, autoDecryptor.ProbeCallCount, "Auto-detection should probe every bundle, including directly readable standard bundles.");
        TestAssert.Equal(1, autoDecryptor.DecryptCallCount, "Matched auto decryptor should process the source.");
        TestAssert.Equal(1, fallbackDecryptor.DecryptCallCount, "Only the standard bundle with no codec match should use pass-through.");
        TestAssert.Equal(2, processingResult.IndexedBundleCount, "Both matched and pass-through sources should be indexed.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Per-bundle codec selections must not replace the workspace auto-detection strategy.");
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
        TestAssert.Equal(2, processingResult.PerformanceDiagnostic?.ProbeEvaluationCount ?? 0, "Probe telemetry should count every evaluated bundle.");
        TestAssert.Equal(1, processingResult.AutoDetectionDiagnostic?.ProbeDiagnostics.Count ?? 0, "Auto-detection diagnostic should include the probe summary.");
        TestAssert.True(processingResult.AutoDetectionDiagnostic?.ProbeDiagnostics[0].WasSelected ?? false, "Probe diagnostic should mark the selected decryptor.");
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
                new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>())),
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
    }

    private static WorkspaceMetadataDto CreateAutoDetectWorkspaceMetadata(
        string workspaceKey,
        string displayName,
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
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInAutoDetect,
            ExcludedBundleSourcePaths: Array.Empty<string>());
}
