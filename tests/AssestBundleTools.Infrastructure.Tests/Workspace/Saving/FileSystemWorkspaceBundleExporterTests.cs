using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static class FileSystemWorkspaceBundleExporterTests
{
    public static async Task ExportAsync_ShouldCopyArtifactAtomically_AndReportMd5AndSha256()
    {
        using var scope = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(scope.DirectoryPath, "cache", "source.ab");
        var outputPath = Path.Combine(scope.DirectoryPath, "exports", "source.ab");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        var payload = Encoding.UTF8.GetBytes("bundle-artifact");
        await File.WriteAllBytesAsync(sourcePath, payload);

        var exporter = new FileSystemWorkspaceBundleExporter();
        var result = await exporter.ExportAsync(new WorkspaceBundleExportRequestDto(
            WorkspaceId: new WorkspaceId("workspace"),
            WorkspaceDisplayName: "Workspace",
            BundleId: new BundleId("bundle"),
            BundleDisplayName: "source.ab",
            OutputFilePath: outputPath,
            SourceArtifactPath: sourcePath,
            BundleCacheKey: "bundle-cache-key",
            ChangeItems: Array.Empty<AssestBundleTools.Application.Workspace.Dtos.WorkspaceChangeItemDto>(),
            ProcessorSelections: Array.Empty<WorkspaceExportProcessorSelectionDto>(),
            ProcessorResults: Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
            OperationId: "operation"));

        TestAssert.True(result.IsSuccess, "Bundle export should succeed.");
        TestAssert.True(File.Exists(outputPath), "Output AB should be written.");
        var writtenBytes = await File.ReadAllBytesAsync(outputPath);
        TestAssert.Equal(payload.Length, writtenBytes.Length, "Output file should keep artifact length.");
        TestAssert.Equal(
            Convert.ToHexString(MD5.HashData(payload)).ToLowerInvariant(),
            result.Value!.WrittenFileMd5,
            "Exporter should report MD5 for downstream compatibility.");
        TestAssert.Equal(
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(),
            result.Value.WrittenFileSha256,
            "Exporter should report SHA-256 for content identity.");
        TestAssert.Equal("SHA256", result.Value.WrittenFileHashAlgorithm, "Primary hash should remain SHA-256.");
        TestAssert.Equal(result.Value.WrittenFileSha256, result.Value.WrittenFileHash, "Primary hash should mirror SHA-256.");
    }
}
