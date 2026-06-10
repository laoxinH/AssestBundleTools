using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Extraction;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Extraction;

internal static class FileSystemWorkspaceExportManifestStoreTests
{
    public static async Task SaveAsync_ShouldWriteReadableManifest_WithSizeAndMd5()
    {
        using var scope = new TemporaryDirectoryScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        var outputPath = Path.Combine(scope.DirectoryPath, "bundle.a.ab", "Texture2D", "Hero.png");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var payload = Encoding.UTF8.GetBytes("hero-texture");
        await File.WriteAllBytesAsync(outputPath, payload);
        var expectedHash = Convert.ToHexString(MD5.HashData(payload)).ToLowerInvariant();
        var store = new FileSystemWorkspaceExportManifestStore();

        var saveResult = await store.SaveAsync(new WorkspaceExportManifestSaveRequestDto(
            WorkspaceKey: "workspace-key",
            ExportedAtUtc: DateTimeOffset.Parse("2026-06-10T01:02:03Z"),
            OutputRoot: scope.DirectoryPath,
            Items: new[]
            {
                new WorkspaceExportManifestSourceItemDto(
                    OutputPath: outputPath,
                    BundleId: new BundleId("bundle.a"),
                    BundleDisplayName: "bundle.a.ab",
                    AssetId: new AssetId("asset.texture"),
                    PathId: 42,
                    AssetType: "Texture2D",
                    AssetTypeDisplayName: null,
                    OriginalName: "Hero",
                    DisambiguatedName: "Hero",
                    ContainerPath: "Assets/Hero",
                    OutputRole: WorkspaceExportManifestOutputRole.Asset)
            }));

        TestAssert.True(saveResult.IsSuccess, "Manifest save should succeed.");
        TestAssert.True(File.Exists(saveResult.Value!.ManifestPath), "Manifest file should exist.");
        TestAssert.Equal(
            Path.Combine(scope.DirectoryPath, FileSystemWorkspaceExportManifestStore.ManifestFileName),
            saveResult.Value.ManifestPath,
            "Manifest should be written at the batch output root.");

        var loadResult = await store.LoadAsync(saveResult.Value.ManifestPath);
        TestAssert.True(loadResult.IsSuccess, "Saved manifest should be readable.");
        var manifest = loadResult.Value!;
        TestAssert.Equal(1, manifest.SchemaVersion, "Manifest schema should be versioned.");
        TestAssert.Equal("workspace-key", manifest.WorkspaceKey, "Manifest should keep workspace identity.");
        TestAssert.Equal(scope.DirectoryPath, manifest.OutputRoot, "Manifest should keep output root.");
        var item = TestAssert.Single(manifest.Items);
        TestAssert.Equal("bundle.a.ab/Texture2D/Hero.png", item.RelativePath, "Manifest should store root-relative paths with slash separators.");
        TestAssert.Equal("bundle.a", item.BundleId, "Manifest should keep bundle identity.");
        TestAssert.Equal("asset.texture", item.AssetId, "Manifest should keep asset identity.");
        TestAssert.Equal(42L, item.PathId, "Manifest should keep path id.");
        TestAssert.Equal(WorkspaceExportManifestOutputRole.Asset, item.OutputRole, "Manifest should keep output role.");
        TestAssert.Equal(payload.Length, item.ExportedFileSizeInBytes, "Manifest should store file size.");
        TestAssert.Equal("MD5", item.ExportedFileHashAlgorithm, "Manifest should declare hash algorithm.");
        TestAssert.Equal(expectedHash, item.ExportedFileHash, "Manifest should store MD5 hash.");
    }
}
