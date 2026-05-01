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
    public static async Task HandleAsync_ShouldRecordProbeTelemetry_WhenAutoDetectionProbesUnresolvedCandidates()
    {
        const string workspaceKey = "workspace-probe";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string sourcePath = @"C:\Bundles\encrypted.ab";
        const string logicalPath = "encrypted.ab";
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
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Probe Workspace",
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
            ImportedSources:
            [
                new WorkspaceImportedSourceDto(
                    Path: sourcePath,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInAutoDetect,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var bundleIndexCacheStore = new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>());
        var previewCacheStore = new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>());
        var autoDecryptorDescriptor = CreateAutoDetectDecryptorDescriptor("test.auto", "Test Auto");
        var autoDecryptor = new TestAssetBundleDecryptor(autoDecryptorDescriptor)
        {
            ProbeResultFactory = _ => Task.FromResult(new AssetBundleDecryptorProbeResultDto(
                IsSuccessful: true,
                IsMatch: true,
                Confidence: 90,
                DiagnosticMessage: null)),
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(autoDecryptorDescriptor, bundleFingerprint, resolvedBundlePath)))
        };
        var fallbackDecryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
        [
            new DiscoveredWorkspaceImportFileDto(sourcePath, logicalPath)
        ]);
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            new Dictionary<string, WorkspaceFileFingerprintDto>(StringComparer.OrdinalIgnoreCase)
            {
                [sourcePath] = sourceFingerprint
            });
        var inspector = new TestAssetBundleSourceInspector
        {
            InspectResultFactory = _ => new AssetBundleSourceInspectionResultDto(
                AssetBundleSourceInspectionKind.Undetermined)
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
        TestAssert.Equal(autoDecryptorDescriptor.Id, processingResult.ResolvedDecryptorId, "Matched probe decryptor should be selected.");
        TestAssert.Equal(1, autoDecryptor.ProbeCallCount, "Auto-detection should probe the unresolved source once.");
        TestAssert.Equal(1, autoDecryptor.DecryptCallCount, "Matched auto decryptor should process the source.");
        TestAssert.Equal(0, fallbackDecryptor.DecryptCallCount, "Fallback decryptor should not be used after a probe match.");
        TestAssert.Equal(1, processingResult.IndexedBundleCount, "Matched source should be indexed.");
        TestAssert.Equal(1, metadataStore.SaveCallCount, "Resolved auto-detected decryptor should be persisted after a successful import.");
        TestAssert.Equal(autoDecryptorDescriptor.Id, metadataStore.Metadata?.AssetBundleDecryptorId, "Metadata should persist the selected decryptor id.");
        TestAssert.True(processingResult.PerformanceDiagnostic is not null, "Processing result should include performance diagnostics.");
        TestAssert.True(processingResult.PerformanceDiagnostic?.ProbeDurationMs >= 0, "Probe duration should be recorded.");
        TestAssert.Equal(1, processingResult.PerformanceDiagnostic?.ProbeDecryptorCount ?? 0, "Probe telemetry should count the evaluated decryptor.");
        TestAssert.Equal(1, processingResult.PerformanceDiagnostic?.ProbeEvaluationCount ?? 0, "Probe telemetry should count the evaluated source.");
        TestAssert.Equal(1, processingResult.AutoDetectionDiagnostic?.ProbeDiagnostics.Count ?? 0, "Auto-detection diagnostic should include the probe summary.");
        TestAssert.True(processingResult.AutoDetectionDiagnostic?.ProbeDiagnostics[0].WasSelected ?? false, "Probe diagnostic should mark the selected decryptor.");
    }
}
