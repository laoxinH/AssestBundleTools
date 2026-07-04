using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

internal static class JsonPreviewCacheStoreTests
{
    public static void Save_ShouldRoundTripDescriptorAndPayload()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-001",
            bundleCacheKey: "bundle-cache-001",
            relativePayloadPath: "payloads/audio/preview-001.bin");
        var expectedContent = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        var expectedImageFocusContent = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        store.Save(new PreviewCachePayloadDto(descriptor, expectedContent, expectedImageFocusContent));
        var restoredPayload = store.Load("workspace-a", "preview-001");

        var payload = TestAssert.NotNull(restoredPayload, "Saved preview payload should be readable.");
        TestAssert.Equal("workspace-a", payload.Descriptor.WorkspaceKey, "Workspace key should round-trip.");
        TestAssert.Equal("preview-001", payload.Descriptor.PreviewCacheKey, "Preview cache key should round-trip.");
        TestAssert.Equal("bundle-cache-001", payload.Descriptor.BundleCacheKey, "Bundle cache key should round-trip.");
        TestAssert.Equal("AudioClip", payload.Descriptor.PreviewKind, "Preview kind should round-trip.");
        TestAssert.Equal("audio/wav", payload.Descriptor.ContentType, "Content type should round-trip.");
        TestAssert.Equal("vgmstream", payload.Descriptor.AudioTranscoderId, "Transcoder id should round-trip.");
        TestAssert.Equal("1.0.0", payload.Descriptor.AudioTranscoderVersion, "Transcoder version should round-trip.");
        var imageOverlay = TestAssert.NotNull(payload.Descriptor.ImageOverlay, "Image overlay should round-trip.");
        TestAssert.Equal(256, imageOverlay.CanvasPixelWidth, "Image overlay canvas width should round-trip.");
        TestAssert.Equal(128, imageOverlay.CanvasPixelHeight, "Image overlay canvas height should round-trip.");
        TestAssert.Equal(24d, imageOverlay.HighlightRegion.X, "Image overlay x should round-trip.");
        TestAssert.Equal(36d, imageOverlay.HighlightRegion.Y, "Image overlay y should round-trip.");
        TestAssert.Equal(80d, imageOverlay.HighlightRegion.Width, "Image overlay width should round-trip.");
        TestAssert.Equal(48d, imageOverlay.HighlightRegion.Height, "Image overlay height should round-trip.");
        var pivotPoint = TestAssert.NotNull(imageOverlay.PivotPoint, "Image overlay pivot should round-trip.");
        TestAssert.Equal(64d, pivotPoint.X, "Image overlay pivot x should round-trip.");
        TestAssert.Equal(72d, pivotPoint.Y, "Image overlay pivot y should round-trip.");
        var border = TestAssert.NotNull(imageOverlay.NineSliceBorder, "Image overlay border should round-trip.");
        TestAssert.Equal(4d, border.Left, "Image overlay border left should round-trip.");
        TestAssert.Equal(5d, border.Top, "Image overlay border top should round-trip.");
        TestAssert.Equal(6d, border.Right, "Image overlay border right should round-trip.");
        TestAssert.Equal(7d, border.Bottom, "Image overlay border bottom should round-trip.");
        var imageFocus = TestAssert.NotNull(payload.Descriptor.ImageFocus, "Image focus descriptor should round-trip.");
        TestAssert.Equal("image/png", imageFocus.ContentType, "Image focus content type should round-trip.");
        TestAssert.Equal("preview-001\\focus.png", imageFocus.RelativePayloadPath, "Image focus payload path should round-trip.");
        TestAssert.Equal(80, imageFocus.PixelWidth, "Image focus width should round-trip.");
        TestAssert.Equal(48, imageFocus.PixelHeight, "Image focus height should round-trip.");
        var restoredImageFocusContent = TestAssert.NotNull(payload.ImageFocusContent, "Image focus payload should round-trip.");
        TestAssert.Equal(4, payload.Content.Length, "Preview payload length should round-trip.");
        AssertBytesEqual(expectedContent, payload.Content, "Preview payload bytes should round-trip.");
        AssertBytesEqual(expectedImageFocusContent, restoredImageFocusContent, "Image focus payload bytes should round-trip.");

        var restoredDescriptors = store.LoadDescriptors("workspace-a");
        var restoredDescriptor = TestAssert.Single(restoredDescriptors);
        TestAssert.Equal("preview-001", restoredDescriptor.PreviewCacheKey, "Descriptor listing should include the saved preview.");

        var payloadFilePath = store.ResolvePayloadFilePath(restoredDescriptor);
        TestAssert.True(!string.IsNullOrWhiteSpace(payloadFilePath), "Payload file path should be resolvable for saved descriptors.");
        TestAssert.True(File.Exists(payloadFilePath), "Resolved payload file should exist on disk.");
    }

    public static void Delete_ShouldRemoveDescriptorPayload_AndPruneEmptyPayloadDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-delete",
            bundleCacheKey: "bundle-cache-delete",
            relativePayloadPath: "payloads/nested/preview-delete.bin");

        store.Save(new PreviewCachePayloadDto(descriptor, new byte[] { 0xAA, 0xBB }));
        var payloadFilePath = store.ResolvePayloadFilePath(descriptor);
        var payloadDirectoryPath = Path.GetDirectoryName(payloadFilePath!);

        store.Delete("workspace-a", "preview-delete");

        TestAssert.Null(store.Load("workspace-a", "preview-delete"), "Deleted preview payload should no longer be readable.");
        TestAssert.Equal(0, store.LoadDescriptors("workspace-a").Count, "Deleted preview descriptor should be removed from listings.");
        TestAssert.True(!File.Exists(payloadFilePath), "Payload file should be removed together with descriptor.");
        TestAssert.True(!File.Exists(Path.Combine(payloadDirectoryPath!, "focus.png")), "Image focus payload file should be removed together with descriptor.");
        TestAssert.False(Directory.Exists(payloadDirectoryPath), "Empty payload directory should be pruned after deletion.");
    }

    public static void LoadTextChunk_ShouldReadNormalizedTextRange_FromPayloadFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        const string sourceText = "line-1\r\nline-2\rline-3\nline-4";
        const string normalizedText = "line-1\nline-2\nline-3\nline-4";
        var display = AssetPreviewTextDisplayPolicy.CreateDisplay(normalizedText);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-text-chunk",
            bundleCacheKey: "bundle-cache-text",
            relativePayloadPath: "payloads/text/preview-text-chunk.txt") with
        {
            PreviewKind = AssetPreviewKind.Text.ToString(),
            ContentType = "text/plain; charset=utf-8",
            PreviewInfoItems = AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                null,
                display.Metadata)
        };

        store.Save(new PreviewCachePayloadDto(
            descriptor,
            System.Text.Encoding.UTF8.GetBytes(sourceText)));

        var chunk = store.LoadTextChunk(
            "workspace-a",
            "preview-text-chunk",
            offset: 7,
            length: 6);

        var result = TestAssert.NotNull(chunk, "Text chunk should be readable from the stored preview payload.");
        TestAssert.Equal("line-2", result.Text, "Chunk text should use normalized newline character offsets.");
        TestAssert.Equal(7, result.Offset, "Chunk should report the requested normalized offset.");
        TestAssert.Equal(6, result.Length, "Chunk should report the returned character count.");
        TestAssert.Equal(normalizedText.Length, result.TotalLength, "Chunk should use text display metadata for total normalized length.");
        TestAssert.Equal(System.Text.Encoding.UTF8.GetByteCount(sourceText), result.ContentLength, "Chunk should expose raw payload file length.");
    }

    public static void LoadTextLines_ShouldReadNormalizedLineRange_FromPayloadFile()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        const string sourceText = "line-0\r\nline-1\rline-2\nline-3\n";
        const string normalizedText = "line-0\nline-1\nline-2\nline-3\n";
        var display = AssetPreviewTextDisplayPolicy.CreateDisplay(normalizedText);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-text-lines",
            bundleCacheKey: "bundle-cache-text",
            relativePayloadPath: "payloads/text/preview-text-lines.txt") with
        {
            PreviewKind = AssetPreviewKind.Text.ToString(),
            ContentType = "text/plain; charset=utf-8",
            PreviewInfoItems = AssetPreviewTextDisplayPolicy.AppendDisplayInfoItems(
                null,
                display.Metadata)
        };

        store.Save(new PreviewCachePayloadDto(
            descriptor,
            System.Text.Encoding.UTF8.GetBytes(sourceText)));

        var lineRange = store.LoadTextLines(
            "workspace-a",
            "preview-text-lines",
            startLineIndex: 1,
            lineCount: 3);

        var result = TestAssert.NotNull(lineRange, "Text lines should be readable from the stored preview payload.");
        TestAssert.Equal(3, result.Lines.Count, "Line range should return requested lines.");
        TestAssert.Equal("line-1", result.Lines[0], "Line range should normalize CRLF before slicing.");
        TestAssert.Equal("line-2", result.Lines[1], "Line range should normalize CR before slicing.");
        TestAssert.Equal("line-3", result.Lines[2], "Line range should preserve LF lines.");
        TestAssert.Equal(1, result.StartLineIndex, "Line range should report requested start line.");
        TestAssert.Equal(3, result.LineCount, "Line range should report returned line count.");
        TestAssert.Equal(5, result.TotalLineCount, "Line range should count trailing empty line when payload ends with newline.");
        TestAssert.Equal(System.Text.Encoding.UTF8.GetByteCount(sourceText), result.ContentLength, "Line range should expose raw payload file length.");
    }

    public static void GetUsage_ShouldReturnPreviewEntriesAndTotalSize()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-usage",
            bundleCacheKey: "bundle-cache-usage",
            relativePayloadPath: "payloads/images/preview-usage.bin");

        store.Save(new PreviewCachePayloadDto(
            descriptor,
            new byte[] { 0x01, 0x02, 0x03 },
            new byte[] { 0x04, 0x05 }));

        var usage = store.GetUsage("workspace-a");

        TestAssert.Equal(1, usage.EntryCount, "Usage should include saved preview descriptor.");
        TestAssert.True(usage.TotalSizeInBytes >= 5, "Usage should include payload bytes.");
        var entry = TestAssert.Single(usage.Entries);
        TestAssert.Equal("preview-usage", entry.PreviewCacheKey, "Usage entry should expose preview cache key.");
        TestAssert.Equal("bundle-cache-usage", entry.BundleCacheKey, "Usage entry should expose bundle cache key.");
        TestAssert.True(entry.SizeInBytes >= 5, "Entry size should include payload and descriptor bytes.");
    }

    public static void Prune_ShouldDeleteOldestPreviewEntries_WhenCountLimitIsExceeded()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        var oldDescriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-old",
            bundleCacheKey: "bundle-cache-old",
            relativePayloadPath: "payloads/images/preview-old.bin") with
        {
            GeneratedAtUtc = new DateTimeOffset(2026, 5, 17, 8, 0, 0, TimeSpan.Zero)
        };
        var newDescriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-new",
            bundleCacheKey: "bundle-cache-new",
            relativePayloadPath: "payloads/images/preview-new.bin") with
        {
            GeneratedAtUtc = new DateTimeOffset(2026, 5, 17, 9, 0, 0, TimeSpan.Zero)
        };

        store.Save(new PreviewCachePayloadDto(oldDescriptor, new byte[] { 0x10 }));
        store.Save(new PreviewCachePayloadDto(newDescriptor, new byte[] { 0x20 }));

        var result = store.Prune(new PreviewCachePruneRequestDto(
            WorkspaceKey: "workspace-a",
            MaximumSizeInBytes: null,
            MaximumEntryCount: 1));

        TestAssert.Equal(2, result.EntryCountBefore, "Prune result should report pre-prune count.");
        TestAssert.Equal(1, result.EntryCountAfter, "Prune result should report post-prune count.");
        var prunedEntry = TestAssert.Single(result.PrunedEntries);
        TestAssert.Equal("preview-old", prunedEntry.PreviewCacheKey, "Oldest preview should be pruned first.");
        TestAssert.Null(store.Load("workspace-a", "preview-old"), "Pruned preview should be removed.");
        TestAssert.NotNull(store.Load("workspace-a", "preview-new"), "Newest preview should remain.");
    }

    public static void Load_ShouldReturnNull_WhenDescriptorExistsButPayloadFileIsMissing()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-missing-payload",
            bundleCacheKey: "bundle-cache-missing-payload",
            relativePayloadPath: "payloads/audio/preview-missing-payload.bin");

        store.Save(new PreviewCachePayloadDto(descriptor, new byte[] { 0x11, 0x22, 0x33 }));
        var payloadFilePath = store.ResolvePayloadFilePath(descriptor);
        File.Delete(payloadFilePath!);

        var restoredPayload = store.Load("workspace-a", "preview-missing-payload");

        TestAssert.Null(restoredPayload, "Missing payload file should be treated as cache miss.");
    }

    public static void Load_ShouldReturnNull_WhenStoredDescriptorPayloadPathIsInvalid()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-invalid-payload-path",
            bundleCacheKey: "bundle-cache-invalid-payload-path",
            relativePayloadPath: "payloads/audio/preview-invalid-payload-path.bin");

        store.Save(new PreviewCachePayloadDto(descriptor, new byte[] { 0x44, 0x55 }));
        OverwriteDescriptorFile(
            temporaryDirectory.DirectoryPath,
            descriptor with
            {
                RelativePayloadPath = "..\\escape.bin"
            });

        var restoredPayload = store.Load("workspace-a", "preview-invalid-payload-path");

        TestAssert.Null(restoredPayload, "Invalid payload path inside stored descriptor should be ignored as corrupted cache.");
    }

    public static void ResolvePayloadFilePath_ShouldReturnNull_WhenDescriptorFieldsAreInvalid()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-invalid-resolve",
            bundleCacheKey: "bundle-cache-invalid-resolve",
            relativePayloadPath: "payloads/audio/preview-invalid-resolve.bin") with
        {
            RelativePayloadPath = "C:\\absolute\\preview.bin"
        };

        var payloadFilePath = store.ResolvePayloadFilePath(descriptor);

        TestAssert.Null(payloadFilePath, "Invalid descriptor fields should not resolve to a payload file path.");
    }

    public static void Delete_ShouldRemoveDescriptor_WhenStoredDescriptorPayloadPathIsInvalid()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonPreviewCacheStore(temporaryDirectory.DirectoryPath);
        var descriptor = CreateDescriptor(
            workspaceKey: "workspace-a",
            previewCacheKey: "preview-invalid-delete",
            bundleCacheKey: "bundle-cache-invalid-delete",
            relativePayloadPath: "payloads/audio/preview-invalid-delete.bin");

        store.Save(new PreviewCachePayloadDto(descriptor, new byte[] { 0x66, 0x77 }));
        OverwriteDescriptorFile(
            temporaryDirectory.DirectoryPath,
            descriptor with
            {
                RelativePayloadPath = "..\\escape.bin"
            });

        store.Delete("workspace-a", "preview-invalid-delete");

        TestAssert.Equal(0, store.LoadDescriptors("workspace-a").Count, "Deleting a corrupted preview descriptor should still clear the descriptor entry.");
    }

    private static PreviewCacheDescriptorDto CreateDescriptor(
        string workspaceKey,
        string previewCacheKey,
        string bundleCacheKey,
        string relativePayloadPath)
    {
        return new PreviewCacheDescriptorDto(
            WorkspaceKey: workspaceKey,
            PreviewCacheKey: previewCacheKey,
            BundleCacheKey: bundleCacheKey,
            AssetId: "asset-001",
            PreviewKind: "AudioClip",
            ContentType: "audio/wav",
            RelativePayloadPath: relativePayloadPath,
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: @"C:\Bundles\audio.ab",
                FileSizeInBytes: 4096,
                LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 10, 0, 0, TimeSpan.Zero),
                ContentHash: "hash-audio"),
            GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 10, 5, 0, TimeSpan.Zero),
            GeneratorVersion: "generator-1.0.0",
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    Key: AssetPreviewInfoKey.AudioContainer,
                    ValueKind: AssetPreviewInfoValueKind.Text,
                    TextValue: "wav")
            ],
            PreviewMessage: new AssetPreviewMessageDto(
                Key: AssetPreviewMessageKey.MetadataOnly,
                Arguments: ["metadata-only"]),
            PreferredDisplaySizeInBytes: 2048,
            AudioTranscoderId: "vgmstream",
            AudioTranscoderVersion: "1.0.0",
            ImageOverlay: new AssetPreviewImageOverlayDto(
                CanvasPixelWidth: 256,
                CanvasPixelHeight: 128,
                HighlightRegion: new AssetPreviewImageRegionDto(
                    X: 24,
                    Y: 36,
                    Width: 80,
                    Height: 48),
                PivotPoint: new AssetPreviewImagePointDto(64, 72),
                NineSliceBorder: new AssetPreviewImageBorderDto(4, 5, 6, 7)),
            ImageFocus: new AssetPreviewImageFocusDto(
                ContentType: "image/png",
                BinaryContent: null,
                RelativePayloadPath: Path.Combine(previewCacheKey, "focus.png"),
                PixelWidth: 80,
                PixelHeight: 48));
    }

    private static void AssertBytesEqual(byte[] expected, byte[] actual, string message)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void OverwriteDescriptorFile(
        string rootDirectoryPath,
        PreviewCacheDescriptorDto descriptor)
    {
        var descriptorDirectoryPath = Path.Combine(
            rootDirectoryPath,
            "cache",
            "previews",
            descriptor.WorkspaceKey);
        Directory.CreateDirectory(descriptorDirectoryPath);

        var descriptorFilePath = Path.Combine(
            descriptorDirectoryPath,
            $"{descriptor.PreviewCacheKey}.json");
        File.WriteAllText(descriptorFilePath, JsonSerializer.Serialize(descriptor));
    }
}
