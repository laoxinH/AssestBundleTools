using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Previewing;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static class AssetsToolsAssetPreviewGeneratorTests
{
    public static async Task GenerateAsync_ShouldReturnGenerationFailureDetail_WhenUnexpectedExceptionOccurs()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var invalidBundlePath = Path.Combine(temporaryDirectory.DirectoryPath, "invalid.bundle");
        await File.WriteAllBytesAsync(invalidBundlePath, [0x01, 0x02, 0x03, 0x04]);

        var generator = new AssetsToolsAssetPreviewGenerator(new AudioPayloadTranscodingService());

        var result = await generator.GenerateAsync(new AssetPreviewBuildRequestDto(
            ResolvedBundlePath: invalidBundlePath,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: temporaryDirectory.DirectoryPath,
            AssetId: "asset.001",
            AssetName: "BrokenPreview",
            AssetPath: "BrokenPreview",
            ContainerPath: null,
            AssetType: AssetType.Texture2D,
            AssetTypeDisplayName: "Texture2D",
            SizeInBytes: 4,
            SerializedFileName: "invalid.assets",
            PathId: 1));
        GC.Collect();
        GC.WaitForPendingFinalizers();

        TestAssert.True(result.IsFailure, "Invalid bundle payload should fail preview generation.");
        var error = TestAssert.Single(result.Errors);
        TestAssert.Equal("Bundle.Preview.GenerationFailed", error.Code, "Failure should use the preview generation error code.");
        var detail = error.Parameters.TryGetValue("Detail", out var value)
            ? value?.ToString()
            : null;
        TestAssert.True(!string.IsNullOrWhiteSpace(detail), "Unexpected preview generation failure should carry diagnostic detail.");
        TestAssert.True(
            detail!.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("AssetsTools", StringComparison.OrdinalIgnoreCase),
            "Diagnostic detail should include exception information.");
    }
}
