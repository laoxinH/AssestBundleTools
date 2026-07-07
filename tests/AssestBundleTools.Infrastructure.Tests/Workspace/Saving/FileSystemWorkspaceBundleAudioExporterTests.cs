using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static partial class FileSystemWorkspaceBundleExporterTests
{
    private static readonly IReadOnlyList<string> AudioSampleBundleCandidates =
    [
        @"D:\project\测试ab\char_003_kalts.ab",
        @"D:\project\测试ab\char_1044_hsgma2_2.ab",
        @"D:\project\测试ab\char_450_necras_summer#20.ab",
        RealSampleBundlePath,
        SpriteSampleBundlePath
    ];

    public static async Task ExportAsync_ShouldRewriteAudioClipResource_WhenRealSampleBundleIsAvailable()
    {
        var sampleBundlePath = AudioSampleBundleCandidates.FirstOrDefault(File.Exists);
        if (sampleBundlePath is null)
        {
            Console.WriteLine("[SKIP] Audio sample bundle not found.");
            return;
        }

        if (!Environment.Is64BitProcess)
        {
            Console.WriteLine("[SKIP] Real sample bundle export requires a 64-bit process.");
            return;
        }

        using var scope = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(scope.DirectoryPath, "cache", Path.GetFileName(sampleBundlePath));
        var outputPath = Path.Combine(scope.DirectoryPath, "exports", Path.GetFileName(sampleBundlePath));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.Copy(sampleBundlePath, sourcePath);

        var probe = ProbeFirstWritableAudioClip(sourcePath);
        if (probe is null)
        {
            Console.WriteLine($"[SKIP] Sample bundle contains no writable AudioClip resource: {sampleBundlePath}");
            return;
        }

        var payloadPath = Path.Combine(scope.DirectoryPath, "audio.fsb");
        var replacementPayload = CreateFsb5Payload();
        await File.WriteAllBytesAsync(payloadPath, replacementPayload);

        var exporter = new FileSystemWorkspaceBundleExporter();
        var result = await exporter.ExportAsync(new WorkspaceBundleExportRequestDto(
            WorkspaceId: new WorkspaceId("workspace"),
            WorkspaceDisplayName: "Workspace",
            BundleId: new BundleId("bundle"),
            BundleDisplayName: Path.GetFileName(sourcePath),
            OutputFilePath: outputPath,
            SourceArtifactPath: sourcePath,
            BundleCacheKey: "bundle-cache-key",
            ChangeItems: Array.Empty<WorkspaceChangeItemDto>(),
            AssetWriteItems:
            [
                new WorkspaceBundleAssetWriteItemDto(
                    BundleId: new BundleId("bundle"),
                    AssetId: new AssetId("asset-audio"),
                    AssetName: probe.AssetName,
                    AssetType: AssetType.AudioClip,
                    AssetTypeDisplayName: nameof(AssetType.AudioClip),
                    SerializedFileName: probe.SerializedFileName,
                    PathId: probe.PathId,
                    SourceKind: "file",
                    SourceFilePath: payloadPath,
                    ProcessorOutputPath: payloadPath,
                    ProcessorId: "resource.processor.audio.fmod-fsb5",
                    ResourceKind: "audio")
            ],
            ProcessorSelections: Array.Empty<WorkspaceExportProcessorSelectionDto>(),
            ProcessorResults: Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
            OperationId: "operation"));

        if (result.IsFailure)
        {
            Console.WriteLine($"[INFO] AudioClip write failed detail: {result.Errors[0].Parameters.GetValueOrDefault("Detail")}");
        }

        TestAssert.True(result.IsSuccess, "Object-level export should succeed for writable AudioClip resource payloads.");
        var exportedAudio = ReadAudioClipResource(outputPath, probe.SerializedFileName, probe.PathId);
        TestAssert.True(exportedAudio is not null, "Exported AudioClip resource should be readable.");
        TestAssert.Equal(replacementPayload.LongLength, exportedAudio!.Size, "Exported AudioClip m_Resource size should match replacement FSB payload.");
        TestAssert.True(
            exportedAudio.Offset >= probe.OriginalResourceSize,
            "Exported AudioClip m_Resource offset should point to appended resource data.");
        TestAssert.True(
            replacementPayload.SequenceEqual(exportedAudio.Payload),
            "Exported AudioClip .resS payload should contain the replacement FSB bytes at the updated offset.");
        TestAssert.Equal(0, exportedAudio.LoadType, "Exported AudioClip should use DecompressOnLoad for appended FSB payloads.");
        TestAssert.Equal(1, exportedAudio.CompressionFormat, "Exported AudioClip should keep Vorbis compression format metadata.");
    }

    private static AudioClipProbe? ProbeFirstWritableAudioClip(string bundlePath)
    {
        var assetsManager = CreateAssetsManager();
        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            foreach (var serializedFile in LoadSerializedFiles(assetsManager, bundleInstance))
            {
                foreach (var assetInfo in serializedFile.file.GetAssetsOfType(AssetClassID.AudioClip))
                {
                    var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
                    if (baseField is null ||
                        ReadAudioClipResourceMetadata(baseField) is not { } metadata ||
                        !HasBundleResourceFile(bundleInstance.file, metadata.Source))
                    {
                        continue;
                    }

                    return new AudioClipProbe(
                        serializedFile.name,
                        assetInfo.PathId,
                        AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name")) ??
                        $"AudioClip {assetInfo.PathId}",
                        metadata.Size);
                }
            }

            return null;
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static AudioClipResourceProbe? ReadAudioClipResource(
        string bundlePath,
        string serializedFileName,
        long pathId)
    {
        var assetsManager = CreateAssetsManager();
        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            var serializedFile = LoadSerializedFiles(assetsManager, bundleInstance)
                .FirstOrDefault(file => string.Equals(file.name, serializedFileName, StringComparison.OrdinalIgnoreCase));
            if (serializedFile is null ||
                !AssetsToolsFieldReader.TryFindAssetInfoByPathId(serializedFile, pathId, out var assetInfo))
            {
                return null;
            }

            var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
            if (baseField is null || ReadAudioClipResourceMetadata(baseField) is not { } metadata)
            {
                return null;
            }

            var entryName = ResolveBundleEntryName(metadata.Source);
            var fileIndex = string.IsNullOrWhiteSpace(entryName)
                ? -1
                : bundleInstance.file.GetFileIndex(entryName);
            if (fileIndex < 0 ||
                metadata.Offset < 0 ||
                metadata.Size <= 0 ||
                metadata.Size > int.MaxValue)
            {
                return null;
            }

            bundleInstance.file.GetFileRange(fileIndex, out var fileOffset, out var fileLength);
            if (metadata.Offset + metadata.Size > fileLength)
            {
                return null;
            }

            bundleInstance.file.DataReader.Position = fileOffset + metadata.Offset;
            var payload = bundleInstance.file.DataReader.ReadBytes((int)metadata.Size);
            return new AudioClipResourceProbe(
                metadata.Source,
                metadata.Offset,
                metadata.Size,
                payload,
                AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_LoadType")),
                AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_CompressionFormat")));
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static AudioClipResourceMetadata? ReadAudioClipResourceMetadata(AssetTypeValueField baseField)
    {
        var resourceField = AssetsToolsFieldReader.TryGetField(baseField, "m_Resource");
        var source = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(resourceField, "m_Source"));
        var offset = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(resourceField, "m_Offset"));
        var size = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(resourceField, "m_Size"));
        return string.IsNullOrWhiteSpace(source) || !offset.HasValue || !size.HasValue || size.Value <= 0
            ? null
            : new AudioClipResourceMetadata(source, offset.Value, size.Value);
    }

    private static bool HasBundleResourceFile(
        AssetBundleFile bundleFile,
        string resourceSource)
    {
        var entryName = ResolveBundleEntryName(resourceSource);
        return !string.IsNullOrWhiteSpace(entryName) && bundleFile.GetFileIndex(entryName) >= 0;
    }

    private static string? ResolveBundleEntryName(string? resourceSource)
    {
        if (string.IsNullOrWhiteSpace(resourceSource))
        {
            return null;
        }

        var normalizedSource = resourceSource.Trim().Replace('\\', '/');
        var lastSlashIndex = normalizedSource.LastIndexOf('/');
        return lastSlashIndex < 0 || lastSlashIndex >= normalizedSource.Length - 1
            ? Path.GetFileName(normalizedSource)
            : normalizedSource[(lastSlashIndex + 1)..];
    }

    private static byte[] CreateFsb5Payload()
    {
        var payload = new byte[32];
        payload[0] = (byte)'F';
        payload[1] = (byte)'S';
        payload[2] = (byte)'B';
        payload[3] = (byte)'5';
        for (var index = 4; index < payload.Length; index++)
        {
            payload[index] = (byte)(index + 17);
        }

        return payload;
    }

    private sealed record AudioClipProbe(
        string SerializedFileName,
        long PathId,
        string AssetName,
        long OriginalResourceSize);

    private sealed record AudioClipResourceMetadata(
        string Source,
        long Offset,
        long Size);

    private sealed record AudioClipResourceProbe(
        string Source,
        long Offset,
        long Size,
        byte[] Payload,
        int? LoadType,
        int? CompressionFormat);
}
