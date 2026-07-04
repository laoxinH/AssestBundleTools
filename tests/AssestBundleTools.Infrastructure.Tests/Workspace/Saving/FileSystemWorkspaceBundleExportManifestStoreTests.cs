using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static class FileSystemWorkspaceBundleExportManifestStoreTests
{
    public static async Task SaveAsync_ShouldWriteReadableBundleExportManifest_WithHashesAndProcessors()
    {
        using var scope = new TemporaryDirectoryScope();
        var outputPath = Path.Combine(scope.DirectoryPath, "bundle.ab");
        var store = new FileSystemWorkspaceBundleExportManifestStore();

        var result = await store.SaveAsync(new WorkspaceBundleExportManifestSaveRequestDto(
            WorkspaceId: new WorkspaceId("workspace"),
            WorkspaceKey: "workspace-key",
            WorkspaceDisplayName: "Workspace",
            OutputDirectory: scope.DirectoryPath,
            ExportedAtUtc: DateTimeOffset.Parse("2026-07-03T08:00:00Z"),
            BundleResults: new[]
            {
                new WorkspaceSaveBundleExecutionResultDto(
                    BundleId: new BundleId("bundle"),
                    BundleDisplayName: "bundle.ab",
                    OutputFilePath: outputPath,
                    Status: WorkspaceSaveBundleExecutionStatusDto.Succeeded,
                    ChangeCount: 2,
                    ModifiedAssetCount: 2,
                    WorkspaceReplacementCount: 1,
                    FileReplacementCount: 1,
                    SourceArtifactPath: Path.Combine(scope.DirectoryPath, "cache", "bundle.ab"),
                    BundleCacheKey: "bundle-cache-key",
                    WrittenFileSizeInBytes: 1024,
                    WrittenFileHashAlgorithm: "SHA256",
                    WrittenFileHash: "sha256",
                    WrittenFileLastWriteTimeUtc: DateTimeOffset.Parse("2026-07-03T08:01:00Z"),
                    WrittenFileMd5: "md5",
                    WrittenFileSha256: "sha256",
                    CompressionMode: WorkspaceBundleExportCompressionModeDto.FastLz4,
                    OriginalCompression: "LZMA",
                    WrittenCompression: "LZ4Fast")
            },
            ProcessorSelections: new[]
            {
                new WorkspaceExportProcessorSelectionDto("texture2d", "resource.processor.texture.cli"),
                new WorkspaceExportProcessorSelectionDto("skel", null)
            }));

        TestAssert.True(result.IsSuccess, "Bundle export manifest save should succeed.");
        TestAssert.True(File.Exists(result.Value!.ManifestPath), "Bundle export manifest should be written.");
        TestAssert.Equal(
            Path.Combine(scope.DirectoryPath, FileSystemWorkspaceBundleExportManifestStore.ManifestFileName),
            result.Value.ManifestPath,
            "Manifest should be written at the AB export root.");

        var manifest = result.Value.Manifest;
        TestAssert.Equal(1, manifest.SchemaVersion, "Manifest schema should be versioned.");
        TestAssert.Equal("workspace", manifest.WorkspaceId, "Manifest should keep workspace id.");
        TestAssert.Equal("workspace-key", manifest.WorkspaceKey, "Manifest should keep workspace key.");
        TestAssert.Equal(scope.DirectoryPath, manifest.OutputDirectory, "Manifest should keep output directory.");
        var bundle = TestAssert.Single(manifest.Bundles);
        TestAssert.Equal("bundle", bundle.BundleId, "Manifest should keep bundle id.");
        TestAssert.Equal("bundle.ab", bundle.RelativePath, "Manifest should use root-relative slash paths.");
        TestAssert.Equal(1024L, bundle.WrittenFileSizeInBytes, "Manifest should keep output size.");
        TestAssert.Equal("md5", bundle.WrittenFileMd5, "Manifest should keep MD5.");
        TestAssert.Equal("sha256", bundle.WrittenFileSha256, "Manifest should keep SHA-256.");
        TestAssert.Equal("SHA256", bundle.WrittenFileHashAlgorithm, "Manifest should keep primary hash algorithm.");
        TestAssert.Equal("sha256", bundle.WrittenFileHash, "Manifest should keep primary hash.");
        TestAssert.Equal("FastLz4", bundle.CompressionMode, "Manifest should keep requested compression mode.");
        TestAssert.Equal("LZMA", bundle.OriginalCompression, "Manifest should keep original compression.");
        TestAssert.Equal("LZ4Fast", bundle.WrittenCompression, "Manifest should keep written compression.");
        TestAssert.Equal(2, manifest.ProcessorSelections.Count, "Manifest should keep processor selection count.");
        TestAssert.Equal("texture2d", manifest.ProcessorSelections[0].ResourceKind, "Manifest should keep selected resource kind.");
        TestAssert.Equal("resource.processor.texture.cli", manifest.ProcessorSelections[0].ProcessorId, "Manifest should keep selected processor id.");
        TestAssert.Equal("skel", manifest.ProcessorSelections[1].ResourceKind, "Manifest should keep disabled processor resource kind.");
        TestAssert.Null(manifest.ProcessorSelections[1].ProcessorId, "Manifest should keep disabled processor as null.");
    }
}
