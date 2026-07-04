using System.IO;
using System.Linq;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Tests.Saving;

internal static partial class SaveWorkspaceHandlersTests
{
    public static async Task GetWorkspaceSavePreviewHandler_ShouldResolveExistingArtifactInputs_FromWorkspaceCache()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string alphaArtifactPath = @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle";

        var workspaceId = CreateWorkspaceId();
        var alphaBundleId = new BundleId("bundle-alpha");
        var zetaBundleId = new BundleId("bundle-zeta");
        var alphaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", alphaBundleId, "Alpha.ab");
        var zetaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-zeta", zetaBundleId, "zeta.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(workspaceKey, alphaIndexEntry.BundleCacheKey, alphaArtifactPath, alphaIndexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: zetaBundleId,
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true),
                    CreateChangeItem(
                        bundleId: alphaBundleId,
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true)
                ])
        };
        var handler = new GetWorkspaceSavePreviewHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([alphaIndexEntry, zetaIndexEntry]),
                artifactStore));

        var result = await handler.HandleAsync(new GetWorkspaceSavePreviewQuery(
            workspaceId,
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Save preview should succeed while resolving existing artifact inputs.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(1, preview.ResolvedArtifactInputCount, "Preview should count resolved read-only artifact inputs.");
        TestAssert.Equal(1, preview.UnresolvedArtifactInputCount, "Preview should count unresolved artifact inputs.");

        var alphaPlan = preview.BundlePlans[0];
        TestAssert.Equal("Alpha.ab", alphaPlan.BundleDisplayName, "Alpha should remain first by display name.");
        TestAssert.Equal(alphaIndexEntry.BundleCacheKey, alphaPlan.BundleCacheKey, "Plan should carry bundle cache key for later write-pack input graph.");
        TestAssert.True(alphaPlan.HasStandardizedArtifact, "Existing materialized artifact should be marked as resolved.");
        TestAssert.Equal(alphaArtifactPath, alphaPlan.ArtifactBundlePath, "Plan should expose the standardized bundle path.");
        TestAssert.False(string.IsNullOrWhiteSpace(alphaPlan.BundleSessionCacheKey), "Plan should expose loaded-session identity.");
        TestAssert.Equal("LZMA", alphaPlan.OriginalCompression, "Plan should expose cached original compression for preserve-original export.");
        TestAssert.Equal(null, alphaPlan.ArtifactDiagnosticCode, "Resolved artifact should not expose a diagnostic code.");

        var zetaPlan = preview.BundlePlans[1];
        TestAssert.Equal(zetaIndexEntry.BundleCacheKey, zetaPlan.BundleCacheKey, "Unresolved plan should still carry bundle cache key when the index exists.");
        TestAssert.False(zetaPlan.HasStandardizedArtifact, "Missing artifact manifest should remain unresolved.");
        TestAssert.Equal("Save.Artifact.MissingManifest", zetaPlan.ArtifactDiagnosticCode, "Missing manifest should be diagnosable without materializing a new artifact.");
        TestAssert.Equal(1, artifactStore.UpdateLastAccessedCallCount, "Only resolved reusable artifacts should update last-accessed metadata.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldReportArtifactInputCounts_WhenContextIsProvided()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var alphaBundleId = new BundleId("bundle-alpha");
        var zetaBundleId = new BundleId("bundle-zeta");
        var alphaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", alphaBundleId, "Alpha.ab");
        var zetaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-zeta", zetaBundleId, "zeta.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                alphaIndexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                alphaIndexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: alphaBundleId,
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true),
                    CreateChangeItem(
                        bundleId: zetaBundleId,
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true)
                ])
        };
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([alphaIndexEntry, zetaIndexEntry]),
                artifactStore));

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Save skeleton should succeed and report artifact input readiness.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected save execution result.");
        TestAssert.Equal(2, execution.PlannedBundleCount, "Execution should preserve planned bundle count.");
        TestAssert.Equal(1, execution.ResolvedArtifactInputCount, "Execution should count bundles with existing standardized artifact input.");
        TestAssert.Equal(1, execution.UnresolvedArtifactInputCount, "Execution should count bundles that still need future plan-stage materialization.");
        TestAssert.Equal(1, artifactStore.UpdateLastAccessedCallCount, "Save input resolution should refresh reusable artifact access time.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldExportSelectedBundles_AndSaveManifest()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var alphaBundleId = new BundleId("bundle-alpha");
        var zetaBundleId = new BundleId("bundle-zeta");
        var alphaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", alphaBundleId, "Alpha.ab");
        var zetaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-zeta", zetaBundleId, "zeta.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                alphaIndexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                alphaIndexEntry.SourceFingerprint),
            CreateArtifactManifest(
                workspaceKey,
                zetaIndexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-zeta\normalized.bundle",
                zetaIndexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: alphaBundleId,
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "HeroTexture",
                        isModified: true,
                        fileReplacementSourceFileName: "hero.png"),
                    CreateChangeItem(
                        bundleId: zetaBundleId,
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true)
                ])
        };
        var exporter = new RecordingBundleExporter();
        var manifestStore = new RecordingBundleExportManifestStore();
        var processorExecutor = new RecordingResourceProcessorExecutor();
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([alphaIndexEntry, zetaIndexEntry]),
                artifactStore),
            new ReadyWorkspaceSaveOutputProbe(),
            exporter,
            manifestStore,
            new TestResourceProcessorCatalogStore(
            [
                CreateResourceProcessor("resource.processor.texture.png", "Texture2D")
            ]),
            processorExecutor);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            SelectedBundleIds: [alphaBundleId],
            ProcessorSelections: [new WorkspaceExportProcessorSelectionDto("Texture2D", "resource.processor.texture.png")],
            BundleWriteConcurrency: 2));

        TestAssert.True(result.IsSuccess, "Real save execution should succeed when exporter and manifest store are available.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected save execution result.");
        TestAssert.Equal(WorkspaceSaveExecutionStatusDto.Succeeded, execution.Status, "Single selected bundle should succeed.");
        TestAssert.Equal(1, execution.PlannedBundleCount, "Execution should only include selected bundles.");
        TestAssert.Equal(1, execution.WrittenBundleCount, "Selected bundle should be written.");
        TestAssert.Equal(@"D:\Exports\_abt-bundle-export-manifest.json", execution.ExportManifestPath, "Manifest path should be returned.");
        TestAssert.Equal(alphaBundleId, TestAssert.Single(exporter.Requests).BundleId, "Exporter should receive only the selected bundle.");
        TestAssert.Equal(WorkspaceBundleExportCompressionModeDto.PreserveOriginal, exporter.Requests[0].CompressionMode, "Exporter should receive the default preserve-original compression mode.");
        TestAssert.Equal("LZMA", exporter.Requests[0].OriginalCompression, "Exporter should receive cached original compression from the save plan.");
        TestAssert.Equal("resource.processor.texture.png", TestAssert.Single(exporter.Requests[0].ProcessorSelections).ProcessorId, "Processor selections should be forwarded to exporter.");
        TestAssert.Equal("HeroTexture", TestAssert.Single(processorExecutor.WorkItems).AssetName, "File-backed Texture2D changes should create resource processor work items.");
        TestAssert.Equal("resource.processor.texture.png", TestAssert.Single(exporter.Requests[0].ProcessorResults).ProcessorId, "Processor execution results should be forwarded to exporter.");
        TestAssert.Equal(1, manifestStore.SaveCallCount, "Successful export should save an AB export manifest.");
        TestAssert.Equal(alphaBundleId, TestAssert.Single(manifestStore.LastRequest!.BundleResults).BundleId, "Manifest should include written bundle results.");
        var bundleResult = TestAssert.Single(execution.BundleResults);
        TestAssert.Equal("abc123-md5", bundleResult.WrittenFileMd5, "Execution should preserve exported AB MD5.");
        TestAssert.Equal("abc123-sha256", bundleResult.WrittenFileSha256, "Execution should preserve exported AB SHA-256.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldBlockExistingOutput_WhenOverwriteIsDisabled()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-alpha");
        var indexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", bundleId, "Alpha.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                indexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                indexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: bundleId,
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "HeroTexture",
                        isModified: true)
                ])
        };
        var exporter = new RecordingBundleExporter();
        var outputProbe = new BlockingWorkspaceSaveOutputProbe("Alpha.ab");
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([indexEntry]),
                artifactStore),
            outputProbe,
            exporter,
            new RecordingBundleExportManifestStore());

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            OutputConflictPolicy: WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists));

        TestAssert.True(result.IsFailure, "Save execution should fail before writing when overwrite is disabled and output exists.");
        TestAssert.Empty(exporter.Requests, "Blocked output should not enter the bundle exporter.");
        var error = TestAssert.Single(result.Errors);
        TestAssert.Equal("Bundle.Save.OutputBlocked", error.Code, "Blocked output should return the structured output conflict error.");
        TestAssert.Equal("Save.Output.FileAlreadyExists", error.Parameters.GetValueOrDefault("DiagnosticCode"), "Existing-file diagnostic should be preserved.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldForwardObjectWriteItems_FromOperationRecords()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-alpha");
        var assetId = new AssetId("asset-text");
        var indexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", bundleId, "Alpha.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                indexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                indexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: bundleId,
                        assetId: assetId,
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true,
                        fileReplacementSourceFileName: "config.txt")
                ])
        };
        registry.AddAssetDetails(CreateAssetDetails(workspaceId, bundleId, assetId, "ConfigText", AssetType.TextAsset, "CAB-main", 42));
        var changeSet = WorkspaceChangeSetDto.Empty(workspaceKey) with
        {
            Operations =
            [
                new WorkspaceOperationRecordDto(
                    OperationId: "replace-file-1",
                    Sequence: 1,
                    OperationKind: WorkspaceOperationKind.ReplaceFromFile,
                    Status: WorkspaceOperationStatus.Applied,
                    WorkspaceKey: workspaceKey,
                    Target: new WorkspaceOperationTargetDto(
                        BundleId: bundleId.Value,
                        AssetId: assetId.Value,
                        AssetName: "ConfigText",
                        AssetType: AssetType.TextAsset.ToString(),
                        AssetTypeDisplayName: "TextAsset",
                        SerializedFileName: "CAB-main",
                        PathId: 42),
                    Source: new WorkspaceOperationSourceDto(
                        SourceKind: "file",
                        SourceFilePath: @"C:\Imports\config.txt",
                        SourceFileName: "config.txt",
                        SourceAssetType: AssetType.TextAsset.ToString()),
                    PreviousState: null,
                    Result: new WorkspaceOperationResultDto(ReplacementSourceKind: "file"),
                    CreatedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero),
                    AppliedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero),
                    CompletedAtUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero))
            ]
        };
        var exporter = new RecordingBundleExporter();
        var processorExecutor = new RecordingResourceProcessorExecutor();
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([indexEntry]),
                artifactStore,
                new TestWorkspaceChangeStore(changeSet)),
            new ReadyWorkspaceSaveOutputProbe(),
            exporter,
            new RecordingBundleExportManifestStore(),
            new TestResourceProcessorCatalogStore(
            [
                CreateResourceProcessor("resource.processor.text", "atlas")
            ]),
            processorExecutor);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Save execution should succeed with object write item payloads.");
        var request = TestAssert.Single(exporter.Requests);
        var writeItem = TestAssert.Single(request.AssetWriteItems);
        TestAssert.Equal(bundleId, writeItem.BundleId, "Write item should keep target bundle identity.");
        TestAssert.Equal(assetId, writeItem.AssetId, "Write item should keep target asset identity.");
        TestAssert.Equal(AssetType.TextAsset, writeItem.AssetType, "Write item should keep target asset type.");
        TestAssert.Equal("CAB-main", writeItem.SerializedFileName, "Write item should carry serialized file name for AssetsTools lookup.");
        TestAssert.Equal(42L, writeItem.PathId, "Write item should carry path id for AssetsTools lookup.");
        TestAssert.Equal(@"C:\Imports\config.txt", writeItem.SourceFilePath, "Write item should carry file replacement payload path.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldSkipBundleWrite_WhenResourceProcessorFails()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var bundleId = new BundleId("bundle-alpha");
        var indexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", bundleId, "Alpha.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                indexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                indexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: bundleId,
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "HeroTexture",
                        isModified: true,
                        fileReplacementSourceFileName: "hero.png")
                ])
        };
        var exporter = new RecordingBundleExporter();
        var processorExecutor = new RecordingResourceProcessorExecutor(failExecution: true);
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([indexEntry]),
                artifactStore),
            new ReadyWorkspaceSaveOutputProbe(),
            exporter,
            new RecordingBundleExportManifestStore(),
            new TestResourceProcessorCatalogStore(
            [
                CreateResourceProcessor("resource.processor.texture.png", "Texture2D")
            ]),
            processorExecutor);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            ProcessorSelections: [new WorkspaceExportProcessorSelectionDto("Texture2D", "resource.processor.texture.png")]));

        TestAssert.True(result.IsSuccess, "Processor failure should be represented as a failed bundle result.");
        TestAssert.Empty(exporter.Requests, "A bundle with processor failures should not enter the bundle writer.");
        var bundleResult = TestAssert.Single(result.Value!.BundleResults);
        TestAssert.Equal(WorkspaceSaveBundleExecutionStatusDto.Failed, bundleResult.Status, "Processor failure should fail the owning bundle.");
        TestAssert.Equal("Bundle.Save.ResourceProcessor.ExecutionFailed", bundleResult.DiagnosticCode, "Bundle diagnostic should point at the processor failure.");
        var summary = TestAssert.Single(bundleResult.ProcessorResults!);
        TestAssert.Equal(WorkspaceResourceProcessorStatusDto.Failed, summary.Status, "Processor summary should preserve failed status.");
        TestAssert.Equal(1, summary.FailedCount, "Failed processor item should be counted.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldWriteManifest_ForPartialBundleSuccess()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";

        var workspaceId = CreateWorkspaceId();
        var alphaBundleId = new BundleId("bundle-alpha");
        var zetaBundleId = new BundleId("bundle-zeta");
        var alphaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-alpha", alphaBundleId, "Alpha.ab");
        var zetaIndexEntry = CreateBundleIndexEntry(workspaceKey, "bundle-cache-zeta", zetaBundleId, "zeta.ab");
        var artifactStore = new TestBundleArtifactStore(
        [
            CreateArtifactManifest(
                workspaceKey,
                alphaIndexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-alpha\normalized.bundle",
                alphaIndexEntry.SourceFingerprint),
            CreateArtifactManifest(
                workspaceKey,
                zetaIndexEntry.BundleCacheKey,
                @"D:\WorkspaceData\cache\bundles\workspace-save-key\bundle-cache-zeta\normalized.bundle",
                zetaIndexEntry.SourceFingerprint)
        ]);
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: alphaBundleId,
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true),
                    CreateChangeItem(
                        bundleId: zetaBundleId,
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true)
                ])
        };
        var exporter = new RecordingBundleExporter(zetaBundleId);
        var manifestStore = new RecordingBundleExportManifestStore();
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([alphaIndexEntry, zetaIndexEntry]),
                artifactStore),
            new ReadyWorkspaceSaveOutputProbe(),
            exporter,
            manifestStore);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            BundleWriteConcurrency: 2));

        TestAssert.True(result.IsSuccess, "Partial export should return a structured execution result.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected save execution result.");
        TestAssert.Equal(WorkspaceSaveExecutionStatusDto.PartiallySucceeded, execution.Status, "One failed bundle should mark the execution partial.");
        TestAssert.Equal(1, execution.WrittenBundleCount, "Only successful bundle should count as written.");
        TestAssert.Equal(1, execution.FailedBundleCount, "Failed bundle count should be preserved.");
        TestAssert.Equal(1, manifestStore.SaveCallCount, "Partial success should still save an AB export manifest.");
        TestAssert.Equal(2, manifestStore.LastRequest!.BundleResults.Count, "Manifest request should keep both succeeded and failed bundle results.");
        TestAssert.Equal(alphaBundleId, manifestStore.LastRequest.BundleResults[0].BundleId, "Manifest should preserve selected plan order.");
        TestAssert.Equal(zetaBundleId, manifestStore.LastRequest.BundleResults[1].BundleId, "Manifest should preserve failed bundle position.");
        TestAssert.Equal(
            WorkspaceSaveBundleExecutionStatusDto.Failed,
            manifestStore.LastRequest.BundleResults[1].Status,
            "Failed bundle result should be available to manifest writer.");
    }

    private static BundleIndexCacheEntryDto CreateBundleIndexEntry(
        string workspaceKey,
        string bundleCacheKey,
        BundleId bundleId,
        string displayName)
    {
        var sourceFingerprint = new WorkspaceFileFingerprintDto(
            SourcePath: Path.Combine(@"D:\Source", displayName),
            FileSizeInBytes: 4096,
            LastWriteTimeUtc: new DateTimeOffset(2026, 5, 17, 8, 0, 0, TimeSpan.Zero),
            ContentHash: $"{bundleCacheKey}-hash");

        return new BundleIndexCacheEntryDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: bundleCacheKey,
            BundleId: bundleId.Value,
            DisplayName: displayName,
            LogicalPath: displayName,
            SourceFingerprint: sourceFingerprint,
            CachedAtUtc: new DateTimeOffset(2026, 5, 17, 8, 30, 0, TimeSpan.Zero),
            ParserVersion: "test-parser/1",
            ToolVersion: "test-tool/1",
            Dependencies: Array.Empty<string>(),
            Assets: Array.Empty<BundleIndexAssetCacheEntryDto>(),
            DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            DecryptorVersion: "1.0.0",
            OriginalCompression: "LZMA");
    }

    private static BundleArtifactManifestDto CreateArtifactManifest(
        string workspaceKey,
        string bundleCacheKey,
        string resolvedBundlePath,
        WorkspaceFileFingerprintDto sourceFingerprint)
    {
        return new BundleArtifactManifestDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: bundleCacheKey,
            ResolvedBundlePath: resolvedBundlePath,
            IsMaterialized: true,
            SourceFingerprint: sourceFingerprint,
            BundleFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: resolvedBundlePath,
                FileSizeInBytes: 4096,
                LastWriteTimeUtc: new DateTimeOffset(2026, 5, 17, 9, 0, 0, TimeSpan.Zero),
                ContentHash: $"{bundleCacheKey}-artifact-hash"),
            DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            DecryptorVersion: "1.0.0",
            NormalizerVersion: "bundle-artifact/1",
            CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 9, 30, 0, TimeSpan.Zero));
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;
        private readonly IBundleArtifactStore _bundleArtifactStore;

        private readonly IWorkspaceChangeStore? _workspaceChangeStore;

        public TestWorkspaceDataStoreFactory(
            IBundleIndexCacheStore bundleIndexCacheStore,
            IBundleArtifactStore bundleArtifactStore,
            IWorkspaceChangeStore? workspaceChangeStore = null)
        {
            _bundleIndexCacheStore = bundleIndexCacheStore;
            _bundleArtifactStore = bundleArtifactStore;
            _workspaceChangeStore = workspaceChangeStore;
        }

        public IBundleArtifactStore CreateBundleArtifactStore(string? rootDirectoryPath = null) => _bundleArtifactStore;

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) => _bundleIndexCacheStore;

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) => throw new NotSupportedException();

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) =>
            _workspaceChangeStore ?? throw new NotSupportedException();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleIndexCacheStore : IBundleIndexCacheStore
    {
        private readonly List<BundleIndexCacheEntryDto> _entries;

        public TestBundleIndexCacheStore(IReadOnlyList<BundleIndexCacheEntryDto> entries)
        {
            _entries = entries.ToList();
        }

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) =>
            _entries.FirstOrDefault(entry =>
                string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
                string.Equals(entry.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) =>
            _entries
                .Where(entry => string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal))
                .ToArray();

        public void Save(BundleIndexCacheEntryDto cacheEntry) => throw new NotSupportedException();

        public void Delete(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class TestBundleArtifactStore : IBundleArtifactStore
    {
        private readonly List<BundleArtifactManifestDto> _manifests;

        public TestBundleArtifactStore(IReadOnlyList<BundleArtifactManifestDto> manifests)
        {
            _manifests = manifests.ToList();
        }

        public int UpdateLastAccessedCallCount { get; private set; }

        public BundleArtifactManifestDto? Load(string workspaceKey, string bundleCacheKey) =>
            _manifests.FirstOrDefault(manifest =>
                string.Equals(manifest.WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
                string.Equals(manifest.BundleCacheKey, bundleCacheKey, StringComparison.OrdinalIgnoreCase));

        public string? ResolveBundlePath(BundleArtifactManifestDto manifest) =>
            manifest.IsMaterialized && !string.IsNullOrWhiteSpace(manifest.ResolvedBundlePath)
                ? manifest.ResolvedBundlePath
                : null;

        public BundleArtifactManifestDto Save(BundleArtifactSaveRequestDto request) => throw new NotSupportedException();

        public BundleArtifactManifestDto UpdateLastAccessed(
            BundleArtifactManifestDto manifest,
            DateTimeOffset lastAccessedAtUtc)
        {
            UpdateLastAccessedCallCount++;
            var updatedManifest = manifest with { LastAccessedAtUtc = lastAccessedAtUtc };
            var index = _manifests.FindIndex(existing =>
                string.Equals(existing.WorkspaceKey, manifest.WorkspaceKey, StringComparison.Ordinal) &&
                string.Equals(existing.BundleCacheKey, manifest.BundleCacheKey, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _manifests[index] = updatedManifest;
            }

            return updatedManifest;
        }

        public BundleArtifactCacheUsageDto GetUsage(string? workspaceKey = null) => throw new NotSupportedException();

        public BundleArtifactPruneResultDto Prune(BundleArtifactPruneRequestDto request) => throw new NotSupportedException();

        public void Delete(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class ReadyWorkspaceSaveOutputProbe : IWorkspaceSaveOutputProbe
    {
        public WorkspaceSaveOutputProbeResult Probe(WorkspaceSaveOutputProbeRequest request) =>
            WorkspaceSaveOutputProbeResult.Ready();
    }

    private sealed class BlockingWorkspaceSaveOutputProbe : IWorkspaceSaveOutputProbe
    {
        private readonly string _blockedFileName;

        public BlockingWorkspaceSaveOutputProbe(string blockedFileName)
        {
            _blockedFileName = blockedFileName;
        }

        public WorkspaceSaveOutputProbeResult Probe(WorkspaceSaveOutputProbeRequest request) =>
            string.Equals(Path.GetFileName(request.OutputFilePath), _blockedFileName, StringComparison.OrdinalIgnoreCase)
                ? WorkspaceSaveOutputProbeResult.Blocked(
                    WorkspaceSaveOutputReadiness.FileAlreadyExists,
                    "Save.Output.FileAlreadyExists")
                : WorkspaceSaveOutputProbeResult.Ready();
    }

    private sealed class TestWorkspaceChangeStore : IWorkspaceChangeStore
    {
        private WorkspaceChangeSetDto? _changeSet;

        public TestWorkspaceChangeStore(WorkspaceChangeSetDto? changeSet)
        {
            _changeSet = changeSet;
        }

        public WorkspaceChangeSetDto? Load(string workspaceKey) => _changeSet;

        public void Save(WorkspaceChangeSetDto changeSet)
        {
            _changeSet = changeSet;
        }

        public void Delete(string workspaceKey)
        {
            _changeSet = null;
        }
    }

    private sealed class RecordingBundleExporter : IWorkspaceBundleExporter
    {
        private readonly BundleId? _failedBundleId;

        public RecordingBundleExporter(BundleId? failedBundleId = null)
        {
            _failedBundleId = failedBundleId;
        }

        public List<WorkspaceBundleExportRequestDto> Requests { get; } = [];

        public Task<Result<WorkspaceBundleExportResultDto>> ExportAsync(
            WorkspaceBundleExportRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (_failedBundleId == request.BundleId)
            {
                return Task.FromResult(Result<WorkspaceBundleExportResultDto>.Failure(
                    ApplicationErrors.BundleExportWriteFailed(request.OutputFilePath, "TestFailure")));
            }

            return Task.FromResult(Result<WorkspaceBundleExportResultDto>.Success(
                new WorkspaceBundleExportResultDto(
                    BundleId: request.BundleId,
                    BundleDisplayName: request.BundleDisplayName,
                    OutputFilePath: request.OutputFilePath,
                    SourceArtifactPath: request.SourceArtifactPath,
                    BundleCacheKey: request.BundleCacheKey,
                    WrittenFileSizeInBytes: 4096,
                    WrittenFileHashAlgorithm: "SHA256",
                    WrittenFileHash: "abc123-sha256",
                    WrittenFileLastWriteTimeUtc: new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero),
                    WrittenFileMd5: "abc123-md5",
                    WrittenFileSha256: "abc123-sha256",
                    ProcessorResults: request.ProcessorResults
                        .GroupBy(static result => new { result.ResourceKind, result.ProcessorId })
                        .Select(static group => new WorkspaceBundleProcessorExecutionSummaryDto(
                            group.Key.ResourceKind,
                            group.Key.ProcessorId,
                            WorkspaceResourceProcessorStatusDto.Succeeded,
                            InputCount: group.Count(),
                            ProcessedCount: group.Count(),
                            FailedCount: 0,
                            ResultPaths: group
                                .Select(static result => result.ResultPath)
                                .Where(static path => !string.IsNullOrWhiteSpace(path))
                                .Select(static path => path!)
                                .ToArray(),
                            Diagnostics: Array.Empty<WorkspaceResourceProcessorDiagnosticDto>()))
                        .ToArray(),
                    CompressionMode: request.CompressionMode,
                    OriginalCompression: "LZMA",
                    WrittenCompression: "LZ4Fast")));
        }
    }

    private static ResourceProcessorCatalogEntryDto CreateResourceProcessor(
        string processorId,
        string resourceKind)
    {
        return new ResourceProcessorCatalogEntryDto(
            processorId,
            processorId,
            Version: "1.0.0",
            Description: string.Empty,
            ResourceProcessorKind.ExternalProcess,
            [resourceKind],
            ResourceProcessorStatus.Available,
            IsBuiltIn: false,
            IsReadOnly: false,
            ManifestFilePath: @"D:\Processors\processor.json",
            ExecutablePath: @"D:\Processors\processor.exe",
            WorkingDirectoryPath: null,
            TimeoutMs: 15000,
            ProcessArguments: ["--input", "{inputPath}", "--output", "{outputPath}", "--result", "{resultPath}"]);
    }

    private sealed class TestResourceProcessorCatalogStore : IResourceProcessorCatalogStore
    {
        private readonly IReadOnlyList<ResourceProcessorCatalogEntryDto> _entries;

        public TestResourceProcessorCatalogStore(IReadOnlyList<ResourceProcessorCatalogEntryDto> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<ResourceProcessorCatalogEntryDto> Load() => _entries;

        public void SaveUserManagedProcessor(ResourceProcessorDefinitionDto definition, string? previousProcessorId = null) =>
            throw new NotSupportedException();

        public void DeleteUserManagedProcessor(string processorId) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingResourceProcessorExecutor : IWorkspaceResourceProcessorExecutor
    {
        private readonly bool _failExecution;

        public RecordingResourceProcessorExecutor(bool failExecution = false)
        {
            _failExecution = failExecution;
        }

        public List<WorkspaceResourceProcessorWorkItemDto> WorkItems { get; } = [];

        public Task<Result<WorkspaceResourceProcessorExecutionResultDto>> ExecuteAsync(
            WorkspaceResourceProcessorWorkItemDto workItem,
            CancellationToken cancellationToken = default)
        {
            WorkItems.Add(workItem);
            if (_failExecution)
            {
                return Task.FromResult(Result<WorkspaceResourceProcessorExecutionResultDto>.Failure(
                    ApplicationErrors.ResourceProcessorExecutionFailed(workItem.Processor.Id, "TestFailure")));
            }

            return Task.FromResult(Result<WorkspaceResourceProcessorExecutionResultDto>.Success(
                new WorkspaceResourceProcessorExecutionResultDto(
                    BundleId: workItem.BundleId,
                    AssetId: workItem.AssetId,
                    AssetName: workItem.AssetName,
                    ResourceKind: workItem.ResourceKind,
                    ProcessorId: workItem.Processor.Id,
                    Status: WorkspaceResourceProcessorStatusDto.Succeeded,
                    InputPath: workItem.InputPayloadPath,
                    OutputPath: Path.Combine(workItem.StagingDirectoryPath, "output"),
                    ResultPath: Path.Combine(workItem.StagingDirectoryPath, "result.json"),
                    Diagnostics: Array.Empty<WorkspaceResourceProcessorDiagnosticDto>(),
                    ContentType: "image/png")));
        }
    }

    private sealed class RecordingBundleExportManifestStore : IWorkspaceBundleExportManifestStore
    {
        public int SaveCallCount { get; private set; }

        public WorkspaceBundleExportManifestSaveRequestDto? LastRequest { get; private set; }

        public Task<Result<WorkspaceBundleExportManifestSaveResultDto>> SaveAsync(
            WorkspaceBundleExportManifestSaveRequestDto request,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            LastRequest = request;
            var manifestPath = Path.Combine(request.OutputDirectory, "_abt-bundle-export-manifest.json");
            return Task.FromResult(Result<WorkspaceBundleExportManifestSaveResultDto>.Success(
                new WorkspaceBundleExportManifestSaveResultDto(
                    manifestPath,
                    new WorkspaceBundleExportManifestDto(
                        SchemaVersion: 1,
                        WorkspaceId: request.WorkspaceId.Value,
                        WorkspaceKey: request.WorkspaceKey,
                        WorkspaceDisplayName: request.WorkspaceDisplayName,
                        OutputDirectory: request.OutputDirectory,
                        ExportedAtUtc: request.ExportedAtUtc,
                        Bundles: Array.Empty<WorkspaceBundleExportManifestItemDto>(),
                        ProcessorSelections: Array.Empty<WorkspaceBundleExportManifestProcessorSelectionDto>()))));
        }
    }
}
