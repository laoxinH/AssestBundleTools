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

    public static async Task ExportAsync_ShouldRewriteAudioClipResource_WhenReplacementMatchesOriginalContainer()
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
        if (!TryPrepareAudioSample(scope, out var sourcePath, out var outputPath, out var probe))
        {
            Console.WriteLine("[SKIP] Audio sample bundles contain no writable AudioClip resource with a known container.");
            return;
        }

        var payloadPath = Path.Combine(scope.DirectoryPath, ResolvePayloadFileName(probe.OriginalContainer));
        var replacementPayload = CreatePayloadForContainer(probe.OriginalContainer);
        await File.WriteAllBytesAsync(payloadPath, replacementPayload);

        var exporter = new FileSystemWorkspaceBundleExporter();
        var result = await exporter.ExportAsync(CreateAudioExportRequest(
            sourcePath,
            outputPath,
            probe,
            payloadPath,
            WorkspaceBundleExportAudioWriteModeDto.MatchOriginalContainer));

        if (result.IsFailure)
        {
            Console.WriteLine($"[INFO] AudioClip write failed detail: {result.Errors[0].Parameters.GetValueOrDefault("Detail")}");
        }

        TestAssert.True(result.IsSuccess, "Object-level export should succeed for writable AudioClip resource payloads.");
        var exportedAudio = ReadAudioClipResource(outputPath, probe.SerializedFileName, probe.PathId);
        TestAssert.True(exportedAudio is not null, "Exported AudioClip resource should be readable.");
        TestAssert.Equal(replacementPayload.LongLength, exportedAudio!.Size, "Exported AudioClip m_Resource size should match replacement payload.");
        TestAssert.True(
            exportedAudio.Offset >= probe.OriginalResourceSize,
            "Exported AudioClip m_Resource offset should point to appended resource data.");
        TestAssert.True(
            replacementPayload.SequenceEqual(exportedAudio.Payload),
            "Exported AudioClip .resS payload should contain the replacement bytes at the updated offset.");
        TestAssert.Equal(probe.OriginalLoadType, exportedAudio.LoadType, "Matched-container writes should preserve original load type metadata.");
        TestAssert.Equal(probe.OriginalCompressionFormat, exportedAudio.CompressionFormat, "Matched-container writes should preserve original compression metadata.");
    }

    public static async Task ExportAsync_ShouldRejectAudioClipResource_WhenReplacementContainerMismatchesOriginal()
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
        if (!TryPrepareAudioSample(scope, out var sourcePath, out var outputPath, out var probe))
        {
            Console.WriteLine("[SKIP] Audio sample bundles contain no writable AudioClip resource with a known container.");
            return;
        }

        var payloadPath = Path.Combine(scope.DirectoryPath, "mismatch.audio");
        var replacementPayload = CreateMismatchedPayloadForContainer(probe.OriginalContainer);
        await File.WriteAllBytesAsync(payloadPath, replacementPayload);

        var exporter = new FileSystemWorkspaceBundleExporter();
        var result = await exporter.ExportAsync(CreateAudioExportRequest(
            sourcePath,
            outputPath,
            probe,
            payloadPath,
            WorkspaceBundleExportAudioWriteModeDto.MatchOriginalContainer));

        TestAssert.True(result.IsFailure, "Default AudioClip export should reject replacement payloads that do not match the original container.");
        var detail = result.Errors[0].Parameters.GetValueOrDefault("Detail")?.ToString() ?? string.Empty;
        TestAssert.True(
            detail.Contains("Save.BundleWrite.AudioPayloadContainerMismatch", StringComparison.Ordinal),
            "Audio container mismatch should keep a stable diagnostic detail.");
        TestAssert.False(File.Exists(outputPath), "Rejected AudioClip writes should not create an exported bundle.");
    }

    public static async Task ExportAsync_ShouldForceRewriteAudioClipResource_WhenReplacementContainerMismatchesOriginal()
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
        if (!TryPrepareAudioSample(scope, out var sourcePath, out var outputPath, out var probe))
        {
            Console.WriteLine("[SKIP] Audio sample bundles contain no writable AudioClip resource with a known container.");
            return;
        }

        var payloadPath = Path.Combine(scope.DirectoryPath, "forced.audio");
        var replacementPayload = CreateMismatchedPayloadForContainer(probe.OriginalContainer);
        await File.WriteAllBytesAsync(payloadPath, replacementPayload);

        var exporter = new FileSystemWorkspaceBundleExporter();
        var result = await exporter.ExportAsync(CreateAudioExportRequest(
            sourcePath,
            outputPath,
            probe,
            payloadPath,
            WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer));

        TestAssert.True(result.IsSuccess, "Forced AudioClip export should allow known replacement containers even when they differ from the original.");
        var exportedAudio = ReadAudioClipResource(outputPath, probe.SerializedFileName, probe.PathId);
        TestAssert.True(exportedAudio is not null, "Forced AudioClip export should persist a readable resource reference.");
        TestAssert.Equal(replacementPayload.LongLength, exportedAudio!.Size, "Forced AudioClip m_Resource size should match replacement payload.");
        TestAssert.True(
            replacementPayload.SequenceEqual(exportedAudio.Payload),
            "Forced AudioClip .resS payload should contain the replacement bytes at the updated offset.");
        TestAssert.Equal(0, exportedAudio.LoadType, "Forced AudioClip writes should use the conservative DecompressOnLoad hint.");
        TestAssert.Equal(1, exportedAudio.CompressionFormat, "Forced AudioClip writes should keep the existing Vorbis metadata hint.");
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
                        !TryReadBundleResourcePayload(bundleInstance.file, metadata, out var payload))
                    {
                        continue;
                    }

                    var originalContainer = DetectAudioContainer(payload);
                    if (originalContainer == AudioPayloadContainer.Unknown)
                    {
                        continue;
                    }

                    return new AudioClipProbe(
                        serializedFile.name,
                        assetInfo.PathId,
                        AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name")) ??
                        $"AudioClip {assetInfo.PathId}",
                        metadata.Size,
                        originalContainer,
                        AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_LoadType")),
                        AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_CompressionFormat")));
                }
            }

            return null;
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static bool TryPrepareAudioSample(
        TemporaryDirectoryScope scope,
        out string sourcePath,
        out string outputPath,
        out AudioClipProbe probe)
    {
        foreach (var sampleBundlePath in AudioSampleBundleCandidates.Where(File.Exists))
        {
            var candidateScopeDirectory = Path.Combine(scope.DirectoryPath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(candidateScopeDirectory);
            (sourcePath, outputPath) = CopyAudioSampleToDirectory(candidateScopeDirectory, sampleBundlePath);
            var candidateProbe = ProbeFirstWritableAudioClip(sourcePath);
            if (candidateProbe is null)
            {
                continue;
            }

            probe = candidateProbe;
            return true;
        }

        sourcePath = string.Empty;
        outputPath = string.Empty;
        probe = null!;
        return false;
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

    private static WorkspaceBundleExportRequestDto CreateAudioExportRequest(
        string sourcePath,
        string outputPath,
        AudioClipProbe probe,
        string payloadPath,
        WorkspaceBundleExportAudioWriteModeDto audioWriteMode) =>
        new(
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
                    ProcessorId: "resource.processor.audio",
                    ResourceKind: "audio")
            ],
            ProcessorSelections: Array.Empty<WorkspaceExportProcessorSelectionDto>(),
            ProcessorResults: Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
            OperationId: "operation",
            AudioWriteMode: audioWriteMode);

    private static (string SourcePath, string OutputPath) CopyAudioSampleToDirectory(
        string scopeDirectory,
        string sampleBundlePath)
    {
        var sourcePath = Path.Combine(scopeDirectory, "cache", Path.GetFileName(sampleBundlePath));
        var outputPath = Path.Combine(scopeDirectory, "exports", Path.GetFileName(sampleBundlePath));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.Copy(sampleBundlePath, sourcePath);
        return (sourcePath, outputPath);
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

    private static bool TryReadBundleResourcePayload(
        AssetBundleFile bundleFile,
        AudioClipResourceMetadata metadata,
        out byte[] payload)
    {
        payload = [];

        var entryName = ResolveBundleEntryName(metadata.Source);
        var fileIndex = string.IsNullOrWhiteSpace(entryName)
            ? -1
            : bundleFile.GetFileIndex(entryName);
        if (fileIndex < 0 ||
            metadata.Offset < 0 ||
            metadata.Size <= 0 ||
            metadata.Size > int.MaxValue)
        {
            return false;
        }

        bundleFile.GetFileRange(fileIndex, out var fileOffset, out var fileLength);
        if (metadata.Offset + metadata.Size > fileLength)
        {
            return false;
        }

        bundleFile.DataReader.Position = fileOffset + metadata.Offset;
        payload = bundleFile.DataReader.ReadBytes((int)metadata.Size);
        return payload.Length > 0;
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

    private static byte[] CreateWavPayload()
    {
        var payload = new byte[48];
        WriteAscii(payload, 0, "RIFF");
        BitConverter.GetBytes(payload.Length - 8).CopyTo(payload, 4);
        WriteAscii(payload, 8, "WAVE");
        WriteAscii(payload, 12, "fmt ");
        BitConverter.GetBytes(16).CopyTo(payload, 16);
        BitConverter.GetBytes((short)1).CopyTo(payload, 20);
        BitConverter.GetBytes((short)1).CopyTo(payload, 22);
        BitConverter.GetBytes(8000).CopyTo(payload, 24);
        BitConverter.GetBytes(16000).CopyTo(payload, 28);
        BitConverter.GetBytes((short)2).CopyTo(payload, 32);
        BitConverter.GetBytes((short)16).CopyTo(payload, 34);
        WriteAscii(payload, 36, "data");
        BitConverter.GetBytes(4).CopyTo(payload, 40);
        payload[44] = 0x01;
        payload[45] = 0x02;
        payload[46] = 0x03;
        payload[47] = 0x04;
        return payload;
    }

    private static byte[] CreateOggPayload()
    {
        var payload = new byte[32];
        WriteAscii(payload, 0, "OggS");
        for (var index = 4; index < payload.Length; index++)
        {
            payload[index] = (byte)(index + 31);
        }

        return payload;
    }

    private static byte[] CreatePayloadForContainer(AudioPayloadContainer container) =>
        container switch
        {
            AudioPayloadContainer.Wav => CreateWavPayload(),
            AudioPayloadContainer.Fsb5 => CreateFsb5Payload(),
            AudioPayloadContainer.Ogg => CreateOggPayload(),
            _ => CreateFsb5Payload()
        };

    private static byte[] CreateMismatchedPayloadForContainer(AudioPayloadContainer container) =>
        container == AudioPayloadContainer.Fsb5
            ? CreateWavPayload()
            : CreateFsb5Payload();

    private static string ResolvePayloadFileName(AudioPayloadContainer container) =>
        container switch
        {
            AudioPayloadContainer.Wav => "audio.wav",
            AudioPayloadContainer.Ogg => "audio.ogg",
            AudioPayloadContainer.Fsb5 => "audio.fsb",
            _ => "audio.bin"
        };

    private static AudioPayloadContainer DetectAudioContainer(byte[] content)
    {
        if (content.Length >= 12 &&
            StartsWithAscii(content, "RIFF") &&
            content[8] == (byte)'W' &&
            content[9] == (byte)'A' &&
            content[10] == (byte)'V' &&
            content[11] == (byte)'E')
        {
            return AudioPayloadContainer.Wav;
        }

        if (StartsWithAscii(content, "OggS"))
        {
            return AudioPayloadContainer.Ogg;
        }

        if (StartsWithAscii(content, "FSB5"))
        {
            return AudioPayloadContainer.Fsb5;
        }

        return AudioPayloadContainer.Unknown;
    }

    private static void WriteAscii(byte[] target, int offset, string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            target[offset + index] = (byte)value[index];
        }
    }

    private static bool StartsWithAscii(byte[] content, string value)
    {
        if (content.Length < value.Length)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (content[index] != (byte)value[index])
            {
                return false;
            }
        }

        return true;
    }

    private sealed record AudioClipProbe(
        string SerializedFileName,
        long PathId,
        string AssetName,
        long OriginalResourceSize,
        AudioPayloadContainer OriginalContainer,
        int? OriginalLoadType,
        int? OriginalCompressionFormat);

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

    private enum AudioPayloadContainer
    {
        Unknown = 0,
        Wav = 1,
        Ogg = 2,
        Fsb5 = 3
    }
}
