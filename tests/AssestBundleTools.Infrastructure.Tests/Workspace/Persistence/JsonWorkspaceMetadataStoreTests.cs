using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

internal static class JsonWorkspaceMetadataStoreTests
{
    public static void Save_ShouldRoundTripWorkspaceMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceMetadataStore(temporaryDirectory.DirectoryPath);
        var createdAtUtc = new DateTimeOffset(2026, 4, 26, 8, 0, 0, TimeSpan.Zero);
        var openedAtUtc = new DateTimeOffset(2026, 4, 28, 9, 30, 0, TimeSpan.Zero);
        var metadata = new WorkspaceMetadataDto(
            WorkspaceKey: "workspace-a",
            DisplayName: "Example Workspace",
            SourcePath: @"C:\Example",
            CreatedAtUtc: createdAtUtc,
            LastOpenedAtUtc: openedAtUtc,
            LastBundleId: "bundle-001",
            LastAssetId: "asset-009",
            LastOutputDirectory: @"D:\Exports",
            BundleSearchKeyword: "characters",
            AssetSearchKeyword: "hero",
            AssetTypeFilter: "Unknown:Shader",
            ShowModifiedOnly: true,
            ShowPreviewableOnly: false,
            OpenBundleTabs:
            [
                new WorkspaceMetadataBundleTabDto(
                    BundleId: "bundle-001",
                    LogicalPath: "environment/characters",
                    IsPreview: false,
                    SelectedAssetId: "asset-009",
                    SearchKeyword: "hero",
                    AssetTypeFilter: "Unknown:Shader",
                    ShowModifiedOnly: true,
                    ShowPreviewableOnly: false,
                    OpenAssetTabs:
                    [
                        new WorkspaceMetadataAssetTabDto(
                            AssetId: "asset-009",
                            AssetName: "hero.png",
                            SelectedInspectorSection: "details",
                            PreviewImageBackgroundMode: "Solid",
                            SpritePreviewDisplayMode: "CropResult",
                            MeshPreviewDisplayMode: "Uv",
                            MeshPreviewTextureOverlaySelected: false,
                            MeshPreviewGridSelected: false,
                            MeshPreviewZoom: 1.5d,
                            MeshPreviewRotationX: 24d,
                            MeshPreviewRotationY: -72d,
                            PreviewImageZoom: 1.75d,
                            PreviewImagePanX: 18d,
                            PreviewImagePanY: -12d,
                            EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                                "draft-main",
                                "text",
                                "source-v1",
                                "Main tab draft"))
                    ],
                    ActiveAssetTabId: "asset-009")
            ],
            ActiveBundleTabId: "bundle-001",
            ImportedSources:
            [
                new WorkspaceImportedSourceDto(
                    Path: @"C:\Bundles\characters",
                    ImportOrigin: WorkspaceImportOriginKind.BundleDirectory,
                    PathKind: WorkspaceImportPathKind.Directory,
                    RecordedAtUtc: openedAtUtc)
            ],
            AssetBundleDecryptorId: "decryptor-a",
            ExcludedBundleSourcePaths:
            [
                @"C:\Bundles\characters\ignored.ab"
            ],
            OpenAssetEditorWindows:
            [
                new WorkspaceAssetEditorWindowStateDto(
                    WindowStateId: 5,
                    Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
                    OpenAssetTabs:
                    [
                        new WorkspaceAssetEditorWindowAssetTabDto(
                            BundleId: "bundle-001",
                            AssetId: "asset-009",
                            AssetName: "hero.png",
                            SelectedInspectorSection: "dependencies",
                            PreviewImageBackgroundMode: "Solid",
                            SpritePreviewDisplayMode: "CropResult",
                            MeshPreviewDisplayMode: "Uv",
                            MeshPreviewTextureOverlaySelected: false,
                            MeshPreviewGridSelected: true,
                            MeshPreviewZoom: 1.25d,
                            MeshPreviewRotationX: 12d,
                            MeshPreviewRotationY: -24d,
                            PreviewImageZoom: 1.8d,
                            PreviewImagePanX: 14d,
                            PreviewImagePanY: -8d,
                            EditDraftReference: new WorkspaceAssetEditDraftReferenceDto(
                                "draft-window",
                                "material",
                                "source-v2",
                                "Editor window draft"))
                    ],
                    ActiveBundleId: "bundle-001",
                    ActiveAssetId: "asset-009")
            ]);

        store.Save(metadata);
        var loadedMetadata = store.Load("workspace-a");

        var restoredMetadata = TestAssert.NotNull(loadedMetadata, "Saved metadata should be readable.");
        TestAssert.Equal("workspace-a", restoredMetadata.WorkspaceKey, "Workspace key should round-trip.");
        TestAssert.Equal("Example Workspace", restoredMetadata.DisplayName, "Display name should round-trip.");
        TestAssert.Equal(@"C:\Example", restoredMetadata.SourcePath, "Source path should round-trip.");
        TestAssert.Equal(createdAtUtc, restoredMetadata.CreatedAtUtc, "Created time should round-trip.");
        TestAssert.Equal(openedAtUtc, restoredMetadata.LastOpenedAtUtc, "Last opened time should round-trip.");
        TestAssert.Equal("bundle-001", restoredMetadata.LastBundleId, "Last bundle id should round-trip.");
        TestAssert.Equal("asset-009", restoredMetadata.LastAssetId, "Last asset id should round-trip.");
        TestAssert.Equal(@"D:\Exports", restoredMetadata.LastOutputDirectory, "Output directory should round-trip.");
        TestAssert.Equal("characters", restoredMetadata.BundleSearchKeyword, "Bundle keyword should round-trip.");
        TestAssert.Equal("hero", restoredMetadata.AssetSearchKeyword, "Asset keyword should round-trip.");
        TestAssert.Equal("Unknown:Shader", restoredMetadata.AssetTypeFilter, "Dynamic asset type filter should round-trip.");
        TestAssert.True(restoredMetadata.ShowModifiedOnly, "Modified-only flag should round-trip.");
        TestAssert.False(restoredMetadata.ShowPreviewableOnly, "Previewable-only flag should round-trip.");
        TestAssert.Equal("bundle-001", restoredMetadata.ActiveBundleTabId, "Active bundle tab should round-trip.");
        TestAssert.Equal("decryptor-a", restoredMetadata.AssetBundleDecryptorId, "Decryptor id should round-trip.");
        TestAssert.Equal(
            @"C:\Bundles\characters\ignored.ab",
            TestAssert.Single(restoredMetadata.ExcludedBundleSourcePaths!).Trim(),
            "Excluded source path should round-trip.");

        var loadedBundleTab = TestAssert.Single(restoredMetadata.OpenBundleTabs);
        TestAssert.Equal("bundle-001", loadedBundleTab.BundleId, "Bundle tab id should round-trip.");
        TestAssert.Equal("environment/characters", loadedBundleTab.LogicalPath, "Bundle tab path should round-trip.");
        TestAssert.Equal("asset-009", loadedBundleTab.SelectedAssetId, "Selected asset id should round-trip.");
        TestAssert.Equal("hero", loadedBundleTab.SearchKeyword, "Tab search keyword should round-trip.");
        TestAssert.Equal("Unknown:Shader", loadedBundleTab.AssetTypeFilter, "Dynamic bundle tab asset type filter should round-trip.");
        TestAssert.Equal("asset-009", loadedBundleTab.ActiveAssetTabId, "Active asset tab should round-trip.");

        var loadedAssetTab = TestAssert.Single(loadedBundleTab.OpenAssetTabs!);
        TestAssert.Equal("asset-009", loadedAssetTab.AssetId, "Asset tab id should round-trip.");
        TestAssert.Equal("hero.png", loadedAssetTab.AssetName, "Asset tab name should round-trip.");
        TestAssert.Equal("details", loadedAssetTab.SelectedInspectorSection, "Inspector section should round-trip.");
        TestAssert.Equal("Solid", loadedAssetTab.PreviewImageBackgroundMode, "Preview image background should round-trip.");
        TestAssert.Equal("CropResult", loadedAssetTab.SpritePreviewDisplayMode, "Sprite preview mode should round-trip.");
        TestAssert.Equal(1.75d, loadedAssetTab.PreviewImageZoom ?? 0d, "Preview image zoom should round-trip.");
        TestAssert.Equal(18d, loadedAssetTab.PreviewImagePanX ?? 0d, "Preview image pan X should round-trip.");
        TestAssert.Equal(-12d, loadedAssetTab.PreviewImagePanY ?? 0d, "Preview image pan Y should round-trip.");
        TestAssert.Equal("Uv", loadedAssetTab.MeshPreviewDisplayMode, "Mesh preview mode should round-trip.");
        TestAssert.False(loadedAssetTab.MeshPreviewTextureOverlaySelected ?? true, "Mesh texture overlay state should round-trip.");
        TestAssert.False(loadedAssetTab.MeshPreviewGridSelected ?? true, "Mesh grid state should round-trip.");
        TestAssert.Equal(1.5d, loadedAssetTab.MeshPreviewZoom ?? 0d, "Mesh zoom should round-trip.");
        TestAssert.Equal(24d, loadedAssetTab.MeshPreviewRotationX ?? 0d, "Mesh rotation X should round-trip.");
        TestAssert.Equal(-72d, loadedAssetTab.MeshPreviewRotationY ?? 0d, "Mesh rotation Y should round-trip.");
        TestAssert.Equal("draft-main", loadedAssetTab.EditDraftReference?.DraftId, "Asset tab draft id should round-trip.");
        TestAssert.Equal("text", loadedAssetTab.EditDraftReference?.DraftMode, "Asset tab draft mode should round-trip.");
        TestAssert.Equal("source-v1", loadedAssetTab.EditDraftReference?.SourceFingerprint, "Asset tab draft source should round-trip.");
        TestAssert.Equal("Main tab draft", loadedAssetTab.EditDraftReference?.SourceDescription, "Asset tab draft description should round-trip.");

        var importedSource = TestAssert.Single(restoredMetadata.ImportedSources!);
        TestAssert.Equal(@"C:\Bundles\characters", importedSource.Path, "Imported source path should round-trip.");
        TestAssert.Equal(WorkspaceImportOriginKind.BundleDirectory, importedSource.ImportOrigin, "Imported source kind should round-trip.");
        TestAssert.Equal(WorkspaceImportPathKind.Directory, importedSource.PathKind, "Imported source path kind should round-trip.");
        TestAssert.Equal(openedAtUtc, importedSource.RecordedAtUtc, "Imported source timestamp should round-trip.");

        var editorWindow = TestAssert.Single(restoredMetadata.OpenAssetEditorWindows!);
        TestAssert.Equal(5, editorWindow.WindowStateId, "Editor window state id should round-trip.");
        TestAssert.Equal(120, editorWindow.Layout.X, "Editor window X should round-trip.");
        TestAssert.Equal(90, editorWindow.Layout.Y, "Editor window Y should round-trip.");
        TestAssert.Equal(1320, editorWindow.Layout.Width, "Editor window width should round-trip.");
        TestAssert.Equal(820, editorWindow.Layout.Height, "Editor window height should round-trip.");
        TestAssert.Equal("bundle-001", editorWindow.ActiveBundleId, "Editor window active bundle should round-trip.");
        TestAssert.Equal("asset-009", editorWindow.ActiveAssetId, "Editor window active asset should round-trip.");

        var editorAssetTab = TestAssert.Single(editorWindow.OpenAssetTabs);
        TestAssert.Equal("bundle-001", editorAssetTab.BundleId, "Editor asset tab bundle should round-trip.");
        TestAssert.Equal("asset-009", editorAssetTab.AssetId, "Editor asset tab id should round-trip.");
        TestAssert.Equal("hero.png", editorAssetTab.AssetName, "Editor asset tab name should round-trip.");
        TestAssert.Equal("dependencies", editorAssetTab.SelectedInspectorSection, "Editor inspector section should round-trip.");
        TestAssert.Equal("Solid", editorAssetTab.PreviewImageBackgroundMode, "Editor preview background should round-trip.");
        TestAssert.Equal("CropResult", editorAssetTab.SpritePreviewDisplayMode, "Editor sprite mode should round-trip.");
        TestAssert.Equal("Uv", editorAssetTab.MeshPreviewDisplayMode, "Editor mesh mode should round-trip.");
        TestAssert.False(editorAssetTab.MeshPreviewTextureOverlaySelected ?? true, "Editor mesh texture overlay should round-trip.");
        TestAssert.True(editorAssetTab.MeshPreviewGridSelected ?? false, "Editor mesh grid should round-trip.");
        TestAssert.Equal(1.25d, editorAssetTab.MeshPreviewZoom ?? 0d, "Editor mesh zoom should round-trip.");
        TestAssert.Equal(12d, editorAssetTab.MeshPreviewRotationX ?? 0d, "Editor mesh rotation X should round-trip.");
        TestAssert.Equal(-24d, editorAssetTab.MeshPreviewRotationY ?? 0d, "Editor mesh rotation Y should round-trip.");
        TestAssert.Equal(1.8d, editorAssetTab.PreviewImageZoom ?? 0d, "Editor preview zoom should round-trip.");
        TestAssert.Equal(14d, editorAssetTab.PreviewImagePanX ?? 0d, "Editor preview pan X should round-trip.");
        TestAssert.Equal(-8d, editorAssetTab.PreviewImagePanY ?? 0d, "Editor preview pan Y should round-trip.");
        TestAssert.Equal("draft-window", editorAssetTab.EditDraftReference?.DraftId, "Editor draft id should round-trip.");
        TestAssert.Equal("material", editorAssetTab.EditDraftReference?.DraftMode, "Editor draft mode should round-trip.");
        TestAssert.Equal("source-v2", editorAssetTab.EditDraftReference?.SourceFingerprint, "Editor draft source should round-trip.");
        TestAssert.Equal("Editor window draft", editorAssetTab.EditDraftReference?.SourceDescription, "Editor draft description should round-trip.");
    }

    public static void Delete_ShouldRemoveSavedMetadataFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonWorkspaceMetadataStore(temporaryDirectory.DirectoryPath);
        var metadata = new WorkspaceMetadataDto(
            WorkspaceKey: "workspace-b",
            DisplayName: "Delete Me",
            SourcePath: @"C:\DeleteMe",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            LastOpenedAtUtc: DateTimeOffset.UtcNow,
            LastBundleId: null,
            LastAssetId: null,
            LastOutputDirectory: null,
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: null,
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs: Array.Empty<WorkspaceMetadataBundleTabDto>(),
            ActiveBundleTabId: null);

        store.Save(metadata);
        TestAssert.NotNull(store.Load("workspace-b"), "Saved metadata should exist before deletion.");

        store.Delete("workspace-b");

        TestAssert.Null(store.Load("workspace-b"), "Deleted metadata should no longer be readable.");
    }
}
