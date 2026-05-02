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
    public static async Task HandleAsync_ShouldRunSynchronousBundleReadsOnMultipleWorkers_WhenConfigured()
    {
        const string workspaceKey = "workspace-sync-parallel";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        var sourcePaths = new[]
        {
            @"C:\Bundles\hero.ab",
            @"C:\Bundles\villain.ab",
            @"C:\Bundles\items.ab",
            @"C:\Bundles\ui.ab"
        };
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Synchronous Parallel Workspace",
            SourcePath: @"C:\Bundles",
            CreatedAtUtc: new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 5, 1, 10, 5, 0, TimeSpan.Zero),
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
                .Select(path => new WorkspaceImportedSourceDto(
                    Path: path,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero)))
                .ToArray(),
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            ExcludedBundleSourcePaths: Array.Empty<string>()));
        var sourceDiscoveryService = new TestWorkspaceImportSourceDiscoveryService(
            sourcePaths.Select(path => new DiscoveredWorkspaceImportFileDto(path, Path.GetFileName(path))).ToArray());
        var fileSystemService = new TestWorkspaceImportFileSystemService(
            sourcePaths.ToDictionary(
                static path => path,
                static path => new WorkspaceFileFingerprintDto(
                    path,
                    FileSizeInBytes: 2048,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 5, 1, 10, 1, 0, TimeSpan.Zero),
                    ContentHash: $"hash-{Path.GetFileNameWithoutExtension(path)}"),
                StringComparer.OrdinalIgnoreCase));
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = request => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                CreateDecryptionResult(
                    decryptorDescriptor,
                    new WorkspaceFileFingerprintDto(
                        request.SourcePath,
                        FileSizeInBytes: 2048,
                        LastWriteTimeUtc: new DateTimeOffset(2026, 5, 1, 10, 1, 0, TimeSpan.Zero),
                        ContentHash: $"hash-{Path.GetFileNameWithoutExtension(request.SourcePath)}"),
                    request.SourcePath)))
        };
        var activeReads = 0;
        var maxConcurrentReads = 0;
        var reader = new TestAssetBundleReader
        {
            ReadResultFactory = request =>
            {
                var currentReads = Interlocked.Increment(ref activeReads);
                RecordMaximumConcurrency(ref maxConcurrentReads, currentReads);
                try
                {
                    Thread.Sleep(120);
                    return Task.FromResult(Result<AssetBundleReadResultDto>.Success(
                        new AssetBundleReadResultDto(
                            DisplayName: Path.GetFileNameWithoutExtension(request.BundlePath),
                            LogicalPath: request.LogicalPath,
                            ParserVersion: "parser-1",
                            Dependencies: Array.Empty<string>(),
                            Assets: Array.Empty<AssetBundleReadAssetDto>())));
                }
                finally
                {
                    Interlocked.Decrement(ref activeReads);
                }
            }
        };
        var handler = new ProcessWorkspaceImportsHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore(Array.Empty<BundleIndexCacheEntryDto>()),
                new TestPreviewCacheStore(Array.Empty<PreviewCacheDescriptorDto>())),
            sourceDiscoveryService,
            fileSystemService,
            new WorkspaceImportExecutionGate(),
            new FixedWorkspaceImportExecutionOptionsResolver(4),
            new TestAssetBundleSourceInspector(),
            new TestAssetBundleDecryptorRegistry(decryptor),
            reader);

        var result = await handler.HandleAsync(new ProcessWorkspaceImportsCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Synchronous reader import should complete successfully.");
        TestAssert.True(maxConcurrentReads > 1, "Configured workers should overlap even when reader work completes synchronously.");
        TestAssert.Equal(4, result.Value?.PerformanceDiagnostic?.BundleWorkerCount ?? 0, "Diagnostics should record the configured worker count.");
    }
}
