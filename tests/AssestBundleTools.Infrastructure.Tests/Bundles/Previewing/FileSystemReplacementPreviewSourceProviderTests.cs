using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Previewing;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static class FileSystemReplacementPreviewSourceProviderTests
{
    public static async Task LoadFilePreviewAsync_ShouldReturnNormalizedText_ForTextAssetAtlas()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var atlasPath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.atlas");
        await File.WriteAllTextAsync(
            atlasPath,
            "hero.png\r\nsize: 512, 256\r\nformat: RGBA8888\r\n",
            Encoding.UTF8);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.LoadFilePreviewAsync(
            CreateAssetDetails(AssetType.TextAsset, "HeroAtlas", "Assets/Hero/hero.atlas"),
            atlasPath);

        TestAssert.True(result.IsSuccess, "Readable atlas files should produce a preview.");
        var preview = TestAssert.NotNull(result.Value, "Successful preview should include content.");
        TestAssert.Equal(AssetPreviewKind.Text, preview.Kind, "Atlas replacement preview should stay textual.");
        TestAssert.Equal("text/plain; charset=utf-8", preview.ContentType, "TextAsset replacement preview should expose UTF-8 text.");
        TestAssert.Equal(
            "hero.png\nsize: 512, 256\nformat: RGBA8888\n",
            Encoding.UTF8.GetString(preview.Content),
            "TextAsset replacement preview should normalize line endings.");
    }

    public static async Task LoadFilePreviewAsync_ShouldReturnBinary_ForBinarySkelTextAsset()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var skeletonPath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.skel");
        var skeletonBytes = new byte[] { 0x00, 0x01, 0x7F, 0x02 };
        await File.WriteAllBytesAsync(skeletonPath, skeletonBytes);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.LoadFilePreviewAsync(
            CreateAssetDetails(AssetType.TextAsset, "HeroSkeleton", "Assets/Hero/hero.skel"),
            skeletonPath);

        TestAssert.True(result.IsSuccess, "Binary skeleton files should still produce a preview payload.");
        var preview = TestAssert.NotNull(result.Value, "Successful preview should include content.");
        TestAssert.Equal(AssetPreviewKind.Binary, preview.Kind, "Binary skeleton replacement preview should preserve raw bytes.");
        TestAssert.Equal("application/octet-stream", preview.ContentType, "Binary replacement preview should use the octet-stream content type.");
        TestAssert.True(
            preview.Content.SequenceEqual(skeletonBytes),
            "Binary skeleton replacement preview should not rewrite the payload.");
        var infoItem = TestAssert.Single(preview.PreviewInfoItems ?? Array.Empty<AssetPreviewInfoItemDto>());
        TestAssert.Equal(AssetPreviewInfoKey.SerializedObjectSize, infoItem.Key, "Binary previews should report the raw payload size.");
        TestAssert.Equal(4L, infoItem.Int64Value ?? 0L, "Binary preview size should match the source file length.");
    }

    public static async Task LoadFilePreviewAsync_ShouldReturnImagePayload_ForTextureReplacement()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var imagePath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.png");
        var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        await File.WriteAllBytesAsync(imagePath, imageBytes);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.LoadFilePreviewAsync(
            CreateAssetDetails(AssetType.Texture2D, "HeroTexture", "Assets/Hero/hero.png"),
            imagePath);

        TestAssert.True(result.IsSuccess, "Texture replacement files should produce an image preview.");
        var preview = TestAssert.NotNull(result.Value, "Successful preview should include content.");
        TestAssert.Equal(AssetPreviewKind.Image, preview.Kind, "Texture replacement preview should stay an image.");
        TestAssert.Equal("image/png", preview.ContentType, "PNG replacement preview should preserve the image content type.");
        TestAssert.True(
            preview.Content.SequenceEqual(imageBytes),
            "Texture replacement preview should pass through the source image payload.");
    }

    private static AssetDetailsDto CreateAssetDetails(
        AssetType assetType,
        string name,
        string assetPath)
    {
        return new AssetDetailsDto(
            WorkspaceId: new WorkspaceId("workspace-preview-source"),
            BundleId: new BundleId("bundle-spine"),
            AssetId: new AssetId($"asset-{name.ToLowerInvariant()}"),
            Name: name,
            AssetPath: assetPath,
            ContainerPath: Path.GetDirectoryName(assetPath) ?? string.Empty,
            AssetType: assetType,
            SizeInBytes: 0,
            PreferredDisplaySizeInBytes: null,
            PathId: 1,
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: assetType.ToString());
    }
}
