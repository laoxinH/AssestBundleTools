using System.Text;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Extraction;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Extraction;

internal static class FileSystemAssetExtractionFileStoreTests
{
    public static async Task WriteAsync_ShouldWriteContent_UnderWorkspaceDataRoot_ForRelativeOutputPath()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var store = new FileSystemAssetExtractionFileStore();

            var result = await store.WriteAsync(new AssetExtractionWriteRequestDto(
                WorkspaceDataRootPath: root,
                SuggestedOutputPath: @"Exports\textures:bundle\Hero?Texture.png",
                Content: Encoding.UTF8.GetBytes("payload")));

            TestAssert.True(result.IsSuccess, "Write should succeed.");
            TestAssert.True(File.Exists(result.Value!.OutputPath), "Output file should exist.");
            TestAssert.True(result.Value.OutputPath.StartsWith(root, StringComparison.OrdinalIgnoreCase), "Relative output should stay under the workspace root.");
            TestAssert.Equal("payload", File.ReadAllText(result.Value.OutputPath), "Written file content should match the payload.");
            TestAssert.True(!result.Value.OutputPath.Contains('?'), "Invalid file-name characters should be sanitized.");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    public static async Task WriteAsync_ShouldCopySourcePayload_WhenContentIsNotProvided()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var sourcePath = Path.Combine(root, "source.bin");
            await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4]);
            var store = new FileSystemAssetExtractionFileStore();

            var result = await store.WriteAsync(new AssetExtractionWriteRequestDto(
                WorkspaceDataRootPath: root,
                SuggestedOutputPath: @"Exports\payload.bin",
                Content: null,
                SourcePayloadFilePath: sourcePath));

            TestAssert.True(result.IsSuccess, "Copy write should succeed.");
            TestAssert.Equal(4L, result.Value!.OutputSizeInBytes, "Copied file size should be reported.");
            TestAssert.True(File.ReadAllBytes(result.Value.OutputPath).SequenceEqual(new byte[] { 1, 2, 3, 4 }), "Copied file bytes should match the source payload.");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "AssestBundleTools.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.GetFullPath(root);
    }

    private static void DeleteTemporaryRoot(string root)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AssestBundleTools.Tests"));
        if (!normalizedRoot.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(normalizedRoot))
        {
            return;
        }

        Directory.Delete(normalizedRoot, recursive: true);
    }
}
