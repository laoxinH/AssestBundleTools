using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
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

        store.Save(new PreviewCachePayloadDto(descriptor, expectedContent));
        var restoredPayload = store.Load("workspace-a", "preview-001");

        var payload = TestAssert.NotNull(restoredPayload, "Saved preview payload should be readable.");
        TestAssert.Equal("workspace-a", payload.Descriptor.WorkspaceKey, "Workspace key should round-trip.");
        TestAssert.Equal("preview-001", payload.Descriptor.PreviewCacheKey, "Preview cache key should round-trip.");
        TestAssert.Equal("bundle-cache-001", payload.Descriptor.BundleCacheKey, "Bundle cache key should round-trip.");
        TestAssert.Equal("AudioClip", payload.Descriptor.PreviewKind, "Preview kind should round-trip.");
        TestAssert.Equal("audio/wav", payload.Descriptor.ContentType, "Content type should round-trip.");
        TestAssert.Equal("vgmstream", payload.Descriptor.AudioTranscoderId, "Transcoder id should round-trip.");
        TestAssert.Equal("1.0.0", payload.Descriptor.AudioTranscoderVersion, "Transcoder version should round-trip.");
        TestAssert.Equal(4, payload.Content.Length, "Preview payload length should round-trip.");
        AssertBytesEqual(expectedContent, payload.Content, "Preview payload bytes should round-trip.");

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
        TestAssert.False(Directory.Exists(payloadDirectoryPath), "Empty payload directory should be pruned after deletion.");
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
            AudioTranscoderVersion: "1.0.0");
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
