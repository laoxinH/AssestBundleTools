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

    public static async Task LoadFileTextChunkAsync_ShouldReadNormalizedTextRange_ForTextAssetFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var atlasPath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.atlas");
        const string sourceText = "hero.png\r\nsize: 512, 256\r\nformat: RGBA8888\r\n";
        await File.WriteAllTextAsync(atlasPath, sourceText, Encoding.UTF8);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.LoadFileTextChunkAsync(
            atlasPath,
            offset: 9,
            length: 14);

        TestAssert.True(result.IsSuccess, "Readable atlas files should support text chunk loading.");
        var chunk = TestAssert.NotNull(result.Value, "Successful chunk loading should include a text chunk.");
        TestAssert.Equal("size: 512, 256", chunk.Text, "Chunk text should use normalized line endings before slicing.");
        TestAssert.Equal(9, chunk.Offset, "Chunk offset should report the normalized text offset.");
        TestAssert.Equal(14, chunk.Length, "Chunk length should report the returned normalized character count.");
        TestAssert.Equal(
            sourceText.Replace("\r\n", "\n", StringComparison.Ordinal).Length,
            chunk.TotalLength,
            "Chunk total length should report normalized full text length.");
        TestAssert.Equal(new FileInfo(atlasPath).Length, chunk.ContentLength, "Chunk content length should report source file bytes.");
        TestAssert.Equal(atlasPath, chunk.SourcePath, "Chunk should keep the normalized source path.");
    }

    public static async Task LoadFileTextLinesAsync_ShouldReadNormalizedLineRange_ForTextAssetFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var atlasPath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.atlas");
        const string sourceText = "line-0\r\nline-1\rline-2\nline-3\n";
        await File.WriteAllTextAsync(atlasPath, sourceText, Encoding.UTF8);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.LoadFileTextLinesAsync(
            atlasPath,
            startLineIndex: 2,
            lineCount: 8);

        TestAssert.True(result.IsSuccess, "Readable atlas files should support text line loading.");
        var lines = TestAssert.NotNull(result.Value, "Successful line loading should include a line range.");
        TestAssert.Equal(3, lines.Lines.Count, "Line range should clamp to available lines.");
        TestAssert.Equal("line-2", lines.Lines[0], "Line range should normalize CR before slicing.");
        TestAssert.Equal("line-3", lines.Lines[1], "Line range should preserve LF lines.");
        TestAssert.Equal(string.Empty, lines.Lines[2], "Line range should preserve trailing empty line.");
        TestAssert.Equal(2, lines.StartLineIndex, "Line range should report requested start line.");
        TestAssert.Equal(3, lines.LineCount, "Line range should report returned line count.");
        TestAssert.Equal(5, lines.TotalLineCount, "Line range should count trailing empty line when source ends with newline.");
        TestAssert.Equal(new FileInfo(atlasPath).Length, lines.ContentLength, "Line range should report source file bytes.");
        TestAssert.Equal(atlasPath, lines.SourcePath, "Line range should keep the normalized source path.");
    }

    public static async Task SearchFileTextAsync_ShouldFindBoundedMatches_ForTextAssetFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var atlasPath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.atlas");
        const string sourceText = "page.png\r\nregion target\rTARGET region\ntail target\n";
        await File.WriteAllTextAsync(atlasPath, sourceText, Encoding.UTF8);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.SearchFileTextAsync(
            atlasPath,
            searchText: "target",
            startLineIndex: 1,
            maxMatchCount: 2,
            matchCase: false);

        TestAssert.True(result.IsSuccess, "Readable atlas files should support text search.");
        var search = TestAssert.NotNull(result.Value, "Successful search should include a result.");
        TestAssert.Equal(2, search.Matches.Count, "Search should return the requested match budget.");
        TestAssert.Equal(1, search.Matches[0].LineIndex, "Search should report normalized line indices.");
        TestAssert.Equal("region target", search.Matches[0].LineText, "Search should normalize line endings before matching.");
        TestAssert.Equal(2, search.Matches[1].LineIndex, "Case-insensitive search should match uppercase text.");
        TestAssert.True(search.HasMore, "Search should report more matches beyond the requested budget.");
        TestAssert.Equal(4, search.TotalScannedLineCount, "Search should scan until an extra match proves overflow.");
        TestAssert.Equal(new FileInfo(atlasPath).Length, search.ContentLength, "Search should report source file bytes.");
        TestAssert.Equal(atlasPath, search.SourcePath, "Search should keep the normalized source path.");
    }

    public static async Task LoadFileTextChunkAsync_ShouldRejectBinarySkelTextAssetFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var skeletonPath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.skel");
        await File.WriteAllBytesAsync(skeletonPath, [0x00, 0x01, 0x7F, 0x02]);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.LoadFileTextChunkAsync(
            skeletonPath,
            offset: 0,
            length: 16);

        TestAssert.True(result.IsFailure, "Binary skeleton files should not be exposed as text chunks.");
    }

    public static async Task SearchFileTextAsync_ShouldRejectBinarySkelTextAssetFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var skeletonPath = Path.Combine(temporaryDirectory.DirectoryPath, "hero.skel");
        await File.WriteAllBytesAsync(skeletonPath, [0x00, 0x01, 0x7F, 0x02]);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.SearchFileTextAsync(
            skeletonPath,
            searchText: "target",
            startLineIndex: 0,
            maxMatchCount: 4,
            matchCase: false);

        TestAssert.True(result.IsFailure, "Binary skeleton files should not be exposed as text search payloads.");
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

    public static async Task LoadFilePreviewAsync_ShouldReturnAudioPayload_ForAudioClipReplacement()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var audioPath = Path.Combine(temporaryDirectory.DirectoryPath, "voice.wav");
        var audioBytes = new byte[]
        {
            0x52, 0x49, 0x46, 0x46,
            0x2C, 0x00, 0x00, 0x00,
            0x57, 0x41, 0x56, 0x45,
            0x66, 0x6D, 0x74, 0x20,
            0x10, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x02, 0x00,
            0x44, 0xAC, 0x00, 0x00,
            0x10, 0xB1, 0x02, 0x00,
            0x04, 0x00,
            0x10, 0x00,
            0x64, 0x61, 0x74, 0x61,
            0x08, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };
        await File.WriteAllBytesAsync(audioPath, audioBytes);

        var provider = new FileSystemReplacementPreviewSourceProvider();

        var result = await provider.LoadFilePreviewAsync(
            CreateAssetDetails(AssetType.AudioClip, "Voice", "Assets/Audio/voice"),
            audioPath);

        TestAssert.True(result.IsSuccess, "AudioClip replacement files should produce an audio preview.");
        var preview = TestAssert.NotNull(result.Value, "Successful preview should include content.");
        TestAssert.Equal(AssetPreviewKind.Audio, preview.Kind, "AudioClip replacement preview should stay audio.");
        TestAssert.Equal("audio/wav", preview.ContentType, "WAV replacement preview should expose an audio content type.");
        TestAssert.True(
            preview.Content.SequenceEqual(audioBytes),
            "AudioClip replacement preview should pass through the source audio payload.");
        TestAssert.Equal(audioBytes.Length, preview.PreferredDisplaySizeInBytes ?? 0, "Audio preview size should match the source file length.");
        var previewInfoItems = preview.PreviewInfoItems ?? Array.Empty<AssetPreviewInfoItemDto>();
        TestAssert.True(
            previewInfoItems.Any(item => item.Key == AssetPreviewInfoKey.AudioContainer && item.TextValue == "WAV"),
            "Audio replacement preview should expose a container badge source.");
        TestAssert.True(
            previewInfoItems.Any(item => item.Key == AssetPreviewInfoKey.AudioPayloadSize && item.ValueKind == AssetPreviewInfoValueKind.ByteSize),
            "Audio replacement preview should expose payload size as a byte size.");
        TestAssert.True(
            previewInfoItems.Any(item => item.Key == AssetPreviewInfoKey.AudioChannels && item.Int64Value == 2),
            "Audio replacement preview should expose channel metadata when a WAV header is available.");
        TestAssert.True(
            previewInfoItems.Any(item => item.Key == AssetPreviewInfoKey.AudioFrequency && item.Int64Value == 44100),
            "Audio replacement preview should expose frequency metadata when a WAV header is available.");
        TestAssert.True(
            previewInfoItems.Any(item => item.Key == AssetPreviewInfoKey.AudioBitsPerSample && item.Int64Value == 16),
            "Audio replacement preview should expose bit depth metadata when a WAV header is available.");
        TestAssert.True(
            previewInfoItems.Any(item => item.Key == AssetPreviewInfoKey.AudioDuration && item.NumberValue is > 0),
            "Audio replacement preview should expose duration metadata when a WAV header is available.");
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
