using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Commands;

internal static class RegisterWorkspaceImportHandlerTests
{
    public static async Task HandleAsync_ShouldNormalizeDistinctPaths_UpdateExistingSource_AndClearReimportedExcludedPath()
    {
        const string workspaceKey = "workspace-a";
        const string workspaceDataRootPath = @"D:\WorkspaceData";
        const string directoryPath = @"C:\Bundles\Characters";
        const string heroBundlePath = @"C:\Bundles\Characters\hero.ab";
        const string worldBundlePath = @"C:\Bundles\Environment\world.ab";
        const string staleExcludedPath = @"C:\Bundles\Legacy\ignored.ab";
        var existingCreatedAtUtc = new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero);
        var existingOpenedAtUtc = new DateTimeOffset(2026, 4, 27, 9, 30, 0, TimeSpan.Zero);
        var existingAssetEditorWindows = new[]
        {
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 3,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto("bundle-a", "asset-a", "asset-a")
                ],
                ActiveBundleId: "bundle-a",
                ActiveAssetId: "asset-a")
        };
        var metadataStore = new TestWorkspaceMetadataStore(new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Existing Workspace",
            SourcePath: @"C:\ExistingWorkspace",
            CreatedAtUtc: existingCreatedAtUtc,
            LastOpenedAtUtc: existingOpenedAtUtc,
            LastBundleId: null,
            LastAssetId: null,
            LastOutputDirectory: @"D:\Exports",
            BundleSearchKeyword: "hero",
            AssetSearchKeyword: "texture",
            AssetTypeFilter: "Texture2D",
            ShowModifiedOnly: true,
            ShowPreviewableOnly: false,
            OpenBundleTabs: Array.Empty<WorkspaceMetadataBundleTabDto>(),
            ActiveBundleTabId: null,
            ImportedSources:
            [
                new WorkspaceImportedSourceDto(
                    Path: worldBundlePath,
                    ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
                    PathKind: WorkspaceImportPathKind.File,
                    RecordedAtUtc: new DateTimeOffset(2026, 4, 26, 10, 0, 0, TimeSpan.Zero))
            ],
            AssetBundleDecryptorId: "decryptor-a",
            ExcludedBundleSourcePaths:
            [
                heroBundlePath,
                staleExcludedPath
            ],
            OpenAssetEditorWindows: existingAssetEditorWindows));
        var fileSystemService = new TestWorkspaceImportFileSystemService(new Dictionary<string, WorkspaceImportPathKind>(StringComparer.OrdinalIgnoreCase)
        {
            [directoryPath] = WorkspaceImportPathKind.Directory,
            [heroBundlePath] = WorkspaceImportPathKind.File,
            [worldBundlePath] = WorkspaceImportPathKind.File
        });
        var handler = new RegisterWorkspaceImportHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            fileSystemService);

        var result = await handler.HandleAsync(new RegisterWorkspaceImportCommand(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: workspaceDataRootPath,
            WorkspaceDisplayName: null,
            WorkspaceSourcePath: @"C:\IgnoredByExistingMetadata",
            ImportOrigin: WorkspaceImportOriginKind.DragDrop,
            Paths:
            [
                $"  {directoryPath}  ",
                heroBundlePath,
                heroBundlePath.ToUpperInvariant(),
                worldBundlePath
            ]));

        TestAssert.True(result.IsSuccess, "Registering valid import paths should succeed.");
        var registrationResult = result.Value ?? throw new InvalidOperationException("Expected registration result.");
        TestAssert.Equal(workspaceKey, registrationResult.WorkspaceKey, "Workspace key should round-trip.");
        TestAssert.Equal("Existing Workspace", registrationResult.WorkspaceDisplayName, "Existing display name should be preserved when command omits it.");
        TestAssert.Equal(3, registrationResult.RequestedPathCount, "Distinct normalized paths should be counted once.");
        TestAssert.Equal(2, registrationResult.AddedPathCount, "New directory and bundle paths should count as additions.");
        TestAssert.Equal(1, registrationResult.UpdatedPathCount, "Existing bundle path should count as update.");
        TestAssert.Equal(2, registrationResult.FileCount, "File path count should match file-system classification.");
        TestAssert.Equal(1, registrationResult.DirectoryCount, "Directory path count should match file-system classification.");
        TestAssert.Equal(3, registrationResult.TotalRegisteredSourceCount, "All merged import sources should be preserved.");

        var savedMetadata = metadataStore.LastSavedMetadata ?? throw new InvalidOperationException("Expected saved metadata.");
        TestAssert.Equal("Existing Workspace", savedMetadata.DisplayName, "Saved metadata should keep existing display name.");
        TestAssert.Equal(@"C:\ExistingWorkspace", savedMetadata.SourcePath, "Existing workspace source path should win over incoming fallback path.");
        TestAssert.Equal(existingCreatedAtUtc, savedMetadata.CreatedAtUtc, "Created time should be preserved from existing metadata.");
        TestAssert.Equal(existingOpenedAtUtc, savedMetadata.LastOpenedAtUtc, "Last opened time should be preserved from existing metadata.");
        TestAssert.Equal(3, savedMetadata.ImportedSources!.Count, "Merged source list should contain three distinct entries.");
        TestAssert.True(
            savedMetadata.ImportedSources.Any(source =>
                string.Equals(source.Path, directoryPath, StringComparison.OrdinalIgnoreCase) &&
                source.PathKind == WorkspaceImportPathKind.Directory &&
                source.ImportOrigin == WorkspaceImportOriginKind.DragDrop),
            "New directory source should be saved with the resolved path kind and import origin.");
        TestAssert.True(
            savedMetadata.ImportedSources.Any(source =>
                string.Equals(source.Path, heroBundlePath, StringComparison.OrdinalIgnoreCase) &&
                source.PathKind == WorkspaceImportPathKind.File &&
                source.ImportOrigin == WorkspaceImportOriginKind.DragDrop),
            "New bundle source should be saved as file import.");
        TestAssert.True(
            savedMetadata.ImportedSources.Any(source =>
                string.Equals(source.Path, worldBundlePath, StringComparison.OrdinalIgnoreCase) &&
                source.PathKind == WorkspaceImportPathKind.File &&
                source.ImportOrigin == WorkspaceImportOriginKind.DragDrop),
            "Existing source should be updated to the latest import origin and timestamp.");
        TestAssert.Equal(1, savedMetadata.ExcludedBundleSourcePaths!.Count, "Re-imported bundle paths should be removed from the exclusion list.");
        TestAssert.Equal(staleExcludedPath, savedMetadata.ExcludedBundleSourcePaths[0], "Only unrelated excluded paths should remain.");
        TestAssert.Equal(
            existingAssetEditorWindows,
            savedMetadata.OpenAssetEditorWindows,
            "Import registration should preserve open asset editor windows.");
        TestAssert.Equal(1, metadataStore.SaveCallCount, "Successful registration should save metadata once.");
        TestAssert.Equal(3, fileSystemService.ResolvePathKindCallCount, "Path kind resolution should run once per distinct normalized path.");
    }

    public static async Task HandleAsync_ShouldReturnFailure_WhenNormalizedPathsAreEmpty()
    {
        var metadataStore = new TestWorkspaceMetadataStore(metadata: null);
        var fileSystemService = new TestWorkspaceImportFileSystemService(new Dictionary<string, WorkspaceImportPathKind>(StringComparer.OrdinalIgnoreCase));
        var handler = new RegisterWorkspaceImportHandler(
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            fileSystemService);

        var result = await handler.HandleAsync(new RegisterWorkspaceImportCommand(
            WorkspaceKey: "workspace-a",
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            WorkspaceDisplayName: "Workspace A",
            WorkspaceSourcePath: @"C:\WorkspaceA",
            ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
            Paths:
            [
                " ",
                "\t",
                string.Empty
            ]));

        TestAssert.True(result.IsFailure, "Empty import paths should fail validation.");
        TestAssert.Equal(
            "Workspace.Import.PathsRequired",
            TestAssert.Single(result.Errors).Code,
            "Failure should use the expected error code.");
        TestAssert.Equal(0, metadataStore.SaveCallCount, "Validation failure should not save metadata.");
        TestAssert.Equal(0, fileSystemService.ResolvePathKindCallCount, "Validation failure should not inspect path kinds.");
    }

    private sealed class TestWorkspaceMetadataStore : IWorkspaceMetadataStore
    {
        public TestWorkspaceMetadataStore(WorkspaceMetadataDto? metadata)
        {
            Metadata = metadata;
        }

        public WorkspaceMetadataDto? Metadata { get; private set; }

        public WorkspaceMetadataDto? LastSavedMetadata { get; private set; }

        public int SaveCallCount { get; private set; }

        public WorkspaceMetadataDto? Load(string workspaceKey) => Metadata;

        public void Save(WorkspaceMetadataDto metadata)
        {
            Metadata = metadata;
            LastSavedMetadata = metadata;
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

    private sealed class TestWorkspaceImportFileSystemService : IWorkspaceImportFileSystemService
    {
        private readonly IReadOnlyDictionary<string, WorkspaceImportPathKind> _pathKinds;

        public TestWorkspaceImportFileSystemService(IReadOnlyDictionary<string, WorkspaceImportPathKind> pathKinds)
        {
            _pathKinds = pathKinds;
        }

        public int ResolvePathKindCallCount { get; private set; }

        public WorkspaceImportPathKind ResolvePathKind(string path)
        {
            ResolvePathKindCallCount++;
            return _pathKinds.TryGetValue(path, out var pathKind)
                ? pathKind
                : WorkspaceImportPathKind.Unknown;
        }

        public WorkspaceFileFingerprintDto CreateFingerprint(string sourcePath)
        {
            throw new NotSupportedException();
        }
    }
}
