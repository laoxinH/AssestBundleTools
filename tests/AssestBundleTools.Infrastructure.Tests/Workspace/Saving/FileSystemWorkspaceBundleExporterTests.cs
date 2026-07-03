using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static class FileSystemWorkspaceBundleExporterTests
{
    private const string RealSampleBundlePath = @"D:\project\测试ab\char_2014_nian_cfa#1_20260503_231150.ab";

    public static async Task ExportAsync_ShouldFail_WhenNoObjectWriteItemsAreProvided()
    {
        using var scope = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(scope.DirectoryPath, "cache", "source.ab");
        var outputPath = Path.Combine(scope.DirectoryPath, "exports", "source.ab");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        await File.WriteAllBytesAsync(sourcePath, Encoding.UTF8.GetBytes("bundle-artifact"));

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
            AssetWriteItems: Array.Empty<WorkspaceBundleAssetWriteItemDto>(),
            ProcessorSelections: Array.Empty<WorkspaceExportProcessorSelectionDto>(),
            ProcessorResults: Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
            OperationId: "operation"));

        TestAssert.True(result.IsFailure, "Object-level export should reject requests without write items.");
        TestAssert.False(File.Exists(outputPath), "Exporter should not create an output AB for empty writes.");
        TestAssert.Equal(
            "Bundle.Save.WriteFailed",
            result.Errors[0].Code,
            "Empty object write requests should return a structured write failure.");
    }

    public static async Task ExportAsync_ShouldRewriteTextAssetObject_WhenRealSampleBundleIsAvailable()
    {
        if (!File.Exists(RealSampleBundlePath))
        {
            Console.WriteLine($"[SKIP] Sample bundle not found: {RealSampleBundlePath}");
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

        var payloadPath = Path.Combine(scope.DirectoryPath, "payload.txt");
        var replacementText = $"abt-object-writer-smoke-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(payloadPath, replacementText, Encoding.UTF8);

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
                    AssetId: new AssetId("asset-text"),
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

        TestAssert.True(result.IsSuccess, "Object-level export should succeed for a writable TextAsset in the real sample bundle.");
        TestAssert.Equal(
            replacementText,
            ReadTextAsset(outputPath, probe.SerializedFileName, probe.PathId),
            "Exported AB should persist the replacement TextAsset content after bundle repack.");
    }

    private static TextAssetProbe? ProbeFirstWritableTextAsset(string bundlePath)
    {
        var assetsManager = CreateAssetsManager();
        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            foreach (var serializedFile in LoadSerializedFiles(assetsManager, bundleInstance))
            {
                foreach (var assetInfo in serializedFile.file.GetAssetsOfType(AssetClassID.TextAsset))
                {
                    var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
                    if (AssetsToolsFieldReader.TryGetField(baseField, "m_Script") is null)
                    {
                        continue;
                    }

                    return new TextAssetProbe(
                        serializedFile.name,
                        assetInfo.PathId,
                        AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name")) ??
                        $"TextAsset {assetInfo.PathId}");
                }
            }

            return null;
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static string? ReadTextAsset(
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
            return AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Script"));
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static IReadOnlyList<AssetsFileInstance> LoadSerializedFiles(
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance)
    {
        var serializedFiles = new List<AssetsFileInstance>();
        var directoryInfos = bundleInstance.file.BlockAndDirInfo.DirectoryInfos;
        for (var index = 0; index < directoryInfos.Count; index++)
        {
            if (bundleInstance.file.IsAssetsFile(index))
            {
                serializedFiles.Add(assetsManager.LoadAssetsFileFromBundle(bundleInstance, index, loadDeps: false));
            }
        }

        return serializedFiles;
    }

    private static AssetsManager CreateAssetsManager() =>
        new()
        {
            UseQuickLookup = true,
            UseTemplateFieldCache = true,
            UseMonoTemplateFieldCache = true
        };

    private sealed record TextAssetProbe(
        string SerializedFileName,
        long PathId,
        string AssetName);
}
