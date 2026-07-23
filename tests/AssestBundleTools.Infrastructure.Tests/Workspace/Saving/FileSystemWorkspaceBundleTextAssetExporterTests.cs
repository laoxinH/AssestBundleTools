using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static partial class FileSystemWorkspaceBundleExporterTests
{
    public static async Task ExportAsync_ShouldPreserveBinaryTextAssetPayloadBytes_WhenRealSampleBundleIsAvailable()
    {
        if (!File.Exists(RealSampleBundlePath))
        {
            Console.WriteLine($"[SKIP] Sample bundle not found: {RealSampleBundlePath}");
            return;
        }

        if (!Environment.Is64BitProcess)
        {
            Console.WriteLine("[SKIP] Real sample bundle export requires a 64-bit process.");
            return;
        }

        using var scope = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(scope.DirectoryPath, "cache", Path.GetFileName(RealSampleBundlePath));
        var outputPath = Path.Combine(scope.DirectoryPath, "exports", Path.GetFileName(RealSampleBundlePath));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.Copy(RealSampleBundlePath, sourcePath);

        var probe = ProbeFirstWritableTextAsset(sourcePath);
        if (probe is null)
        {
            Console.WriteLine($"[SKIP] Sample bundle contains no readable TextAsset: {RealSampleBundlePath}");
            return;
        }

        var payloadPath = Path.Combine(scope.DirectoryPath, "skeleton.skel");
        byte[] replacementPayload =
        [
            0x1c, 0xff, 0x00, 0x80, 0xc3, 0x28, 0xfe, 0x7f,
            0x41, 0x42, 0x00, 0xf5, 0x90, 0x80, 0x80, 0x43
        ];
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
            ChangeItems: Array.Empty<AssestBundleTools.Application.Workspace.Dtos.WorkspaceChangeItemDto>(),
            AssetWriteItems:
            [
                new WorkspaceBundleAssetWriteItemDto(
                    BundleId: new BundleId("bundle"),
                    AssetId: new AssetId("asset-binary-text"),
                    AssetName: probe.AssetName,
                    AssetType: AssetType.TextAsset,
                    AssetTypeDisplayName: nameof(AssetType.TextAsset),
                    SerializedFileName: probe.SerializedFileName,
                    PathId: probe.PathId,
                    SourceKind: "file",
                    SourceFilePath: payloadPath)
            ],
            ProcessorSelections: Array.Empty<WorkspaceExportProcessorSelectionDto>(),
            ProcessorResults: Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
            OperationId: "operation"));

        TestAssert.True(result.IsSuccess, "Object-level export should accept a binary TextAsset replacement payload.");
        TestAssert.True(
            replacementPayload.SequenceEqual(ReadTextAssetBytes(outputPath, probe.SerializedFileName, probe.PathId) ?? []),
            "Exported AB should preserve every binary TextAsset byte, including invalid UTF-8 and NUL bytes.");
    }

    private static byte[]? ReadTextAssetBytes(
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
            return AssetsToolsFieldReader.ReadByteArray(AssetsToolsFieldReader.TryGetField(baseField, "m_Script"));
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }
}
