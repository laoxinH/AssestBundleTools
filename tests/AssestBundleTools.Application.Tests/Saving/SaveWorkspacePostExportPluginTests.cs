using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Tests.Saving;

internal static partial class SaveWorkspaceHandlersTests
{
    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldLaunchPostExportPlugin_WithWrittenManifest()
    {
        const string workspaceKey = "workspace-save-key";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string outputDirectory = @"D:\Exports";
        const string pluginId = "post-export.zip";

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
                        bundleId,
                        new AssetId("asset-1"),
                        "Alpha.ab",
                        "HeroTexture",
                        isModified: true)
                ])
        };
        var launcher = new RecordingPostExportPluginLauncher();
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([indexEntry]),
                artifactStore),
            new ReadyWorkspaceSaveOutputProbe(),
            new RecordingBundleExporter(),
            new RecordingBundleExportManifestStore(),
            postExportPluginCatalogStore: new TestPostExportPluginCatalogStore([CreatePostExportPlugin(pluginId)]),
            postExportPluginLauncher: launcher);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: outputDirectory,
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            PostExportPluginId: pluginId));

        TestAssert.True(result.IsSuccess, "Bundle export should stay successful after the post-export handoff starts.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected save execution result.");
        var request = TestAssert.Single(launcher.Requests);
        TestAssert.Equal(
            Path.Combine(outputDirectory, "_abt-bundle-export-manifest.json"),
            request.ManifestPath,
            "The launcher should receive the exact written export manifest path.");
        TestAssert.Equal(outputDirectory, request.OutputDirectory, "The launcher should receive the export output directory.");
        TestAssert.Equal(workspaceId, request.WorkspaceId, "The launcher should receive workspace identity.");
        TestAssert.Equal(workspaceKey, request.WorkspaceKey, "The launcher should receive workspace persistence key.");
        TestAssert.Equal(pluginId, request.Plugin.Id, "The selected catalog plugin should be launched.");
        TestAssert.Equal(
            WorkspacePostExportPluginLaunchStatusDto.Launched,
            execution.PostExportPluginLaunchResult?.Status,
            "The result should report only that process launch succeeded.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldPreserveSuccessfulExport_WhenPostExportPluginLaunchFails()
    {
        const string workspaceKey = "workspace-save-key";
        const string pluginId = "post-export.zip";
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
                        bundleId,
                        new AssetId("asset-1"),
                        "Alpha.ab",
                        "HeroTexture",
                        isModified: true)
                ])
        };
        var launcher = new RecordingPostExportPluginLauncher(failLaunch: true);
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([indexEntry]),
                artifactStore),
            new ReadyWorkspaceSaveOutputProbe(),
            new RecordingBundleExporter(),
            new RecordingBundleExportManifestStore(),
            postExportPluginCatalogStore: new TestPostExportPluginCatalogStore([CreatePostExportPlugin(pluginId)]),
            postExportPluginLauncher: launcher);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputDirectory: @"D:\Exports",
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            PostExportPluginId: pluginId));

        TestAssert.True(result.IsSuccess, "A launch failure must not roll back already-written bundles or their manifest.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected save execution result.");
        TestAssert.Equal(WorkspaceSaveExecutionStatusDto.Succeeded, execution.Status, "Bundle status should remain successful.");
        TestAssert.Equal(1, execution.WrittenBundleCount, "The written bundle count should be preserved.");
        TestAssert.True(!string.IsNullOrWhiteSpace(execution.ExportManifestPath), "The completed manifest path should be preserved.");
        TestAssert.Equal(
            WorkspacePostExportPluginLaunchStatusDto.LaunchFailed,
            execution.PostExportPluginLaunchResult?.Status,
            "The handoff should expose a launch-only warning status.");
    }

    private static PostExportPluginCatalogEntryDto CreatePostExportPlugin(string pluginId) =>
        new(
            pluginId,
            "ZIP Packager",
            "1.0.0",
            "Packages exported bundles from the manifest.",
            ResourceProcessorStatus.Available,
            IsReadOnly: false,
            ManifestFilePath: @"D:\Plugins\post-export.json",
            ExecutablePath: @"D:\Plugins\post-export.exe",
            WorkingDirectoryPath: null,
            ProcessArguments: ["--manifest", "{manifestPath}"]);

    private sealed class TestPostExportPluginCatalogStore : IPostExportPluginCatalogStore
    {
        private readonly IReadOnlyList<PostExportPluginCatalogEntryDto> _entries;

        public TestPostExportPluginCatalogStore(IReadOnlyList<PostExportPluginCatalogEntryDto> entries)
        {
            _entries = entries;
        }

        public IReadOnlyList<PostExportPluginCatalogEntryDto> Load() => _entries;

        public void SaveUserManagedPlugin(PostExportPluginDefinitionDto definition, string? previousPluginId = null) =>
            throw new NotSupportedException();

        public void DeleteUserManagedPlugin(string pluginId) => throw new NotSupportedException();
    }

    private sealed class RecordingPostExportPluginLauncher : IWorkspacePostExportPluginLauncher
    {
        private readonly bool _failLaunch;

        public RecordingPostExportPluginLauncher(bool failLaunch = false)
        {
            _failLaunch = failLaunch;
        }

        public List<WorkspacePostExportPluginLaunchRequestDto> Requests { get; } = [];

        public Task<Result<WorkspacePostExportPluginLaunchResultDto>> LaunchAsync(
            WorkspacePostExportPluginLaunchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return _failLaunch
                ? Task.FromResult(Result<WorkspacePostExportPluginLaunchResultDto>.Failure(
                    ApplicationErrors.PostExportPluginLaunchFailed(request.Plugin.Id, "TestFailure")))
                : Task.FromResult(Result<WorkspacePostExportPluginLaunchResultDto>.Success(
                    new WorkspacePostExportPluginLaunchResultDto(
                        request.Plugin.Id,
                        request.Plugin.DisplayName,
                        WorkspacePostExportPluginLaunchStatusDto.Launched)));
        }
    }
}
