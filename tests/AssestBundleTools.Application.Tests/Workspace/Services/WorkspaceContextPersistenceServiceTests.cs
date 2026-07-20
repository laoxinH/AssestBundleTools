using AssestBundleTools.Application.Tests.TestDoubles;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Services;

internal static class WorkspaceContextPersistenceServiceTests
{
    public static Task SaveWorkspaceBrowserState_ShouldPersistAssetEditorWindowSnapshots()
    {
        const string workspaceKey = "workspace-a";
        var existingMetadata = CreateMetadata(
            workspaceKey,
            openAssetEditorWindows: CreateEditorWindows("old-window"));
        var metadataStore = new TestWorkspaceMetadataStore(existingMetadata);
        var service = CreateService(metadataStore);
        var editorWindows = CreateEditorWindows("window-1");
        var exportSettings = CreateExportSettings(
            exportToWorkspaceDirectory: true,
            overwriteExistingFiles: true);

        service.SaveWorkspaceBrowserState(new WorkspaceBrowserStateSaveRequestDto(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            DisplayName: "Example Workspace",
            SourcePath: @"C:\Workspace",
            AssetBundleDecryptorId: null,
            BrowserState: new WorkspaceBrowserStateSnapshotDto(
                BundleSearchKeyword: "bundle",
                AssetSearchKeyword: "asset",
                AssetTypeFilter: "Texture2D",
                ShowModifiedOnly: true,
                ShowPreviewableOnly: true,
                OpenBundleTabs: Array.Empty<WorkspaceMetadataBundleTabDto>(),
                ActiveBundleTabId: null,
                LastBundleId: "bundle-a",
                LastAssetId: "asset-a",
                OpenAssetEditorWindows: editorWindows,
                ExportSettings: exportSettings)));

        var savedMetadata = metadataStore.LastSavedMetadata ?? throw new InvalidOperationException("Expected saved metadata.");
        TestAssert.Equal(editorWindows, savedMetadata.OpenAssetEditorWindows, "Asset editor windows should be persisted from browser state.");
        AssertExportSettings(exportSettings, savedMetadata.ExportSettings, "Export settings should be persisted from browser state.");
        TestAssert.Equal(@"D:\Exports", savedMetadata.LastOutputDirectory, "Non-browser metadata fields should be preserved.");
        TestAssert.Equal("decryptor-a", savedMetadata.AssetBundleDecryptorId, "Existing decryptor should be preserved when request omits it.");

        var restoredBrowserState = service.LoadWorkspaceBrowserState(workspaceKey, @"D:\WorkspaceData")
            ?? throw new InvalidOperationException("Expected restored browser state.");
        TestAssert.Equal(
            editorWindows,
            restoredBrowserState.OpenAssetEditorWindows,
            "Asset editor windows should map back into browser state snapshots.");
        AssertExportSettings(
            exportSettings,
            restoredBrowserState.ExportSettings,
            "Export settings should map back into browser state snapshots.");
        return Task.CompletedTask;
    }

    public static Task SaveWorkspaceActivationMetadata_ShouldPreserveExistingAssetEditorWindowSnapshots()
    {
        const string workspaceKey = "workspace-a";
        var editorWindows = CreateEditorWindows("window-1");
        var exportSettings = CreateExportSettings(
            exportToWorkspaceDirectory: true,
            overwriteExistingFiles: true);
        var metadataStore = new TestWorkspaceMetadataStore(CreateMetadata(
            workspaceKey,
            editorWindows,
            exportSettings));
        var service = CreateService(metadataStore);

        service.SaveWorkspaceActivationMetadata(new WorkspaceActivationMetadataSaveRequestDto(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            DisplayName: "Example Workspace",
            SourcePath: @"C:\Workspace",
            OpenedAtUtc: new DateTimeOffset(2026, 5, 6, 8, 0, 0, TimeSpan.Zero),
            AssetBundleDecryptorId: null));

        var savedMetadata = metadataStore.LastSavedMetadata ?? throw new InvalidOperationException("Expected saved metadata.");
        TestAssert.Equal(
            editorWindows,
            savedMetadata.OpenAssetEditorWindows,
            "Activation metadata save should preserve existing asset editor windows.");
        AssertExportSettings(
            exportSettings,
            savedMetadata.ExportSettings,
            "Activation metadata save should preserve existing export settings.");
        return Task.CompletedTask;
    }

    public static Task SaveWorkspaceBrowserState_ShouldPreserveExistingExportSettings_WhenSnapshotOmitsExportSettings()
    {
        const string workspaceKey = "workspace-a";
        var exportSettings = CreateExportSettings(
            exportToWorkspaceDirectory: true,
            overwriteExistingFiles: false);
        var metadataStore = new TestWorkspaceMetadataStore(CreateMetadata(
            workspaceKey,
            openAssetEditorWindows: null,
            exportSettings: exportSettings));
        var service = CreateService(metadataStore);

        service.SaveWorkspaceBrowserState(new WorkspaceBrowserStateSaveRequestDto(
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            DisplayName: "Example Workspace",
            SourcePath: @"C:\Workspace",
            AssetBundleDecryptorId: null,
            BrowserState: new WorkspaceBrowserStateSnapshotDto(
                BundleSearchKeyword: string.Empty,
                AssetSearchKeyword: string.Empty,
                AssetTypeFilter: null,
                ShowModifiedOnly: false,
                ShowPreviewableOnly: false,
                OpenBundleTabs: Array.Empty<WorkspaceMetadataBundleTabDto>(),
                ActiveBundleTabId: null,
                LastBundleId: null,
                LastAssetId: null)));

        var savedMetadata = metadataStore.LastSavedMetadata ?? throw new InvalidOperationException("Expected saved metadata.");
        AssertExportSettings(
            exportSettings,
            savedMetadata.ExportSettings,
            "Browser state save should preserve existing export settings when a snapshot does not carry them.");
        return Task.CompletedTask;
    }

    private static WorkspaceContextPersistenceService CreateService(TestWorkspaceMetadataStore metadataStore)
    {
        return new WorkspaceContextPersistenceService(
            new TestWorkspaceCatalogStore(WorkspaceCatalogSnapshotDto.Empty),
            new TestWorkspaceMetadataStoreFactory(metadataStore),
            new TestWorkspaceImportFileSystemService());
    }

    private static WorkspaceMetadataDto CreateMetadata(
        string workspaceKey,
        IReadOnlyList<WorkspaceAssetEditorWindowStateDto>? openAssetEditorWindows,
        WorkspaceExportSettingsSnapshotDto? exportSettings = null)
    {
        return new WorkspaceMetadataDto(
            WorkspaceKey: workspaceKey,
            DisplayName: "Example Workspace",
            SourcePath: @"C:\Workspace",
            CreatedAtUtc: new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero),
            LastOpenedAtUtc: new DateTimeOffset(2026, 5, 5, 8, 0, 0, TimeSpan.Zero),
            LastBundleId: "bundle-a",
            LastAssetId: "asset-a",
            LastOutputDirectory: @"D:\Exports",
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: null,
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs: Array.Empty<WorkspaceMetadataBundleTabDto>(),
            ActiveBundleTabId: null,
            ImportedSources: Array.Empty<WorkspaceImportedSourceDto>(),
            AssetBundleDecryptorId: "decryptor-a",
            ExcludedBundleSourcePaths: Array.Empty<string>(),
            OpenAssetEditorWindows: openAssetEditorWindows,
            ExportSettings: exportSettings);
    }

    private static WorkspaceExportSettingsSnapshotDto CreateExportSettings(
        bool exportToWorkspaceDirectory,
        bool overwriteExistingFiles)
    {
        return new WorkspaceExportSettingsSnapshotDto(
            ExportToWorkspaceDirectory: exportToWorkspaceDirectory,
            OverwriteExistingFiles: overwriteExistingFiles,
            CompressionMode: WorkspaceBundleExportCompressionModeDto.Lz4,
            AudioWriteMode: WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer,
            ProcessorSettings:
            [
                new WorkspaceExportProcessorSettingSnapshotDto("audio", true, "resource.processor.audio.fmod-fsb5"),
                new WorkspaceExportProcessorSettingSnapshotDto("texture2d", false, null)
            ],
            UseSourceCodec: false,
            PostExportPluginId: "post-export.zip");
    }

    private static void AssertExportSettings(
        WorkspaceExportSettingsSnapshotDto expected,
        WorkspaceExportSettingsSnapshotDto? actual,
        string message)
    {
        var restored = TestAssert.NotNull(actual, message);
        TestAssert.Equal(expected.ExportToWorkspaceDirectory, restored.ExportToWorkspaceDirectory, message);
        TestAssert.Equal(expected.OverwriteExistingFiles, restored.OverwriteExistingFiles, message);
        TestAssert.Equal(expected.CompressionMode, restored.CompressionMode, message);
        TestAssert.Equal(expected.AudioWriteMode, restored.AudioWriteMode, message);
        TestAssert.Equal(expected.UseSourceCodec, restored.UseSourceCodec, message);
        TestAssert.Equal(expected.PostExportPluginId, restored.PostExportPluginId, message);
        TestAssert.Equal(expected.ProcessorSettings?.Count ?? 0, restored.ProcessorSettings?.Count ?? 0, message);
        TestAssert.Equal(
            expected.ProcessorSettings![0].ProcessorId,
            restored.ProcessorSettings![0].ProcessorId,
            message);
        TestAssert.Equal(
            expected.ProcessorSettings[1].IsEnabled,
            restored.ProcessorSettings[1].IsEnabled,
            message);
    }

    private static IReadOnlyList<WorkspaceAssetEditorWindowStateDto> CreateEditorWindows(string assetName)
    {
        return
        [
            new WorkspaceAssetEditorWindowStateDto(
                WindowStateId: 7,
                Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                OpenAssetTabs:
                [
                    new WorkspaceAssetEditorWindowAssetTabDto(
                        BundleId: "bundle-a",
                        AssetId: "asset-a",
                        AssetName: assetName,
                        SelectedInspectorSection: "details",
                        PreviewImageBackgroundMode: "Solid",
                        SpritePreviewDisplayMode: "CropResult",
                        MeshPreviewDisplayMode: "Uv",
                        MeshPreviewTextureOverlaySelected: false,
                        MeshPreviewGridSelected: true,
                        MeshPreviewZoom: 1.25d,
                        MeshPreviewRotationX: 12d,
                        MeshPreviewRotationY: 24d,
                        PreviewImageZoom: 1.5d,
                        PreviewImagePanX: 10d,
                        PreviewImagePanY: -5d)
                ],
                ActiveBundleId: "bundle-a",
                ActiveAssetId: "asset-a")
        ];
    }

    private sealed class TestWorkspaceMetadataStore : IWorkspaceMetadataStore
    {
        public TestWorkspaceMetadataStore(WorkspaceMetadataDto? metadata)
        {
            Metadata = metadata;
        }

        public WorkspaceMetadataDto? Metadata { get; private set; }

        public WorkspaceMetadataDto? LastSavedMetadata { get; private set; }

        public WorkspaceMetadataDto? Load(string workspaceKey) => Metadata;

        public void Save(WorkspaceMetadataDto metadata)
        {
            Metadata = metadata;
            LastSavedMetadata = metadata;
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
        public WorkspaceImportPathKind ResolvePathKind(string path) => WorkspaceImportPathKind.Directory;

        public WorkspaceFileFingerprintDto CreateFingerprint(string sourcePath) =>
            throw new NotSupportedException();
    }
}
