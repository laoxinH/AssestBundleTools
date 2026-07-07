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
    private const string SpriteSampleBundlePath = @"D:\project\测试ab\带sprite.ab";

    public static async Task ExportAsync_ShouldRewriteSpriteGeometry_WhenRealSampleBundleIsAvailable()
    {
        if (!File.Exists(SpriteSampleBundlePath))
        {
            Console.WriteLine($"[SKIP] Sprite sample bundle not found: {SpriteSampleBundlePath}");
            return;
        }

        if (!Environment.Is64BitProcess)
        {
            Console.WriteLine("[SKIP] Real sample bundle export requires a 64-bit process.");
            return;
        }

        using var scope = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(scope.DirectoryPath, "cache", Path.GetFileName(SpriteSampleBundlePath));
        var outputPath = Path.Combine(scope.DirectoryPath, "exports", Path.GetFileName(SpriteSampleBundlePath));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.Copy(SpriteSampleBundlePath, sourcePath);

        var probe = ProbeFirstWritableSprite(sourcePath);
        if (probe is null)
        {
            Console.WriteLine($"[SKIP] Sample bundle contains no writable Sprite geometry: {SpriteSampleBundlePath}");
            return;
        }

        var geometry = new WorkspaceSpriteEditGeometryDto(
            CanvasWidth: Math.Max(128, probe.CanvasWidth),
            CanvasHeight: Math.Max(128, probe.CanvasHeight),
            RegionX: 8,
            RegionY: 12,
            RegionWidth: 64,
            RegionHeight: 32,
            PivotX: 40,
            PivotY: 28);
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
                    AssetId: new AssetId("asset-sprite"),
                    AssetName: probe.AssetName,
                    AssetType: AssetType.Sprite,
                    AssetTypeDisplayName: nameof(AssetType.Sprite),
                    SerializedFileName: probe.SerializedFileName,
                    PathId: probe.PathId,
                    SourceKind: "sprite-edit",
                    SpriteEditGeometry: geometry)
            ],
            ProcessorSelections: Array.Empty<WorkspaceExportProcessorSelectionDto>(),
            ProcessorResults: Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
            OperationId: "operation"));

        if (result.IsFailure)
        {
            Console.WriteLine($"[INFO] Sprite geometry write failed detail: {result.Errors[0].Parameters.GetValueOrDefault("Detail")}");
        }

        TestAssert.True(result.IsSuccess, "Object-level export should succeed for writable Sprite geometry in the real sample bundle.");
        var exportedSprite = ReadSpriteGeometry(outputPath, probe.SerializedFileName, probe.PathId);
        TestAssert.True(exportedSprite is not null, "Exported Sprite geometry should be readable.");
        AssertClose(geometry.RegionX, exportedSprite!.TextureRectX, "Exported Sprite textureRect x should match the edited top-left source x.");
        AssertClose(
            geometry.CanvasHeight - geometry.RegionY - geometry.RegionHeight,
            exportedSprite.TextureRectY,
            "Exported Sprite textureRect y should be converted back to Unity bottom-left coordinates.");
        AssertClose(geometry.RegionWidth, exportedSprite.TextureRectWidth, "Exported Sprite textureRect width should match the edited crop width.");
        AssertClose(geometry.RegionHeight, exportedSprite.TextureRectHeight, "Exported Sprite textureRect height should match the edited crop height.");
        TestAssert.True(
            IsExpectedSpritePivot(exportedSprite, geometry),
            "Exported Sprite pivot should preserve the edited absolute pivot in the target Sprite's pivot representation.");
    }

    private static SpriteProbe? ProbeFirstWritableSprite(string bundlePath)
    {
        var assetsManager = CreateAssetsManager();
        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            foreach (var serializedFile in LoadSerializedFiles(assetsManager, bundleInstance))
            {
                foreach (var assetInfo in serializedFile.file.GetAssetsOfType(AssetClassID.Sprite))
                {
                    var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
                    if (baseField is null ||
                        ReadSpriteGeometry(baseField) is not { } geometry)
                    {
                        continue;
                    }

                    return new SpriteProbe(
                        serializedFile.name,
                        assetInfo.PathId,
                        AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name")) ??
                        $"Sprite {assetInfo.PathId}",
                        Math.Max(1, (int)Math.Ceiling(Math.Max(geometry.CanvasWidth, geometry.TextureRectX + geometry.TextureRectWidth))),
                        Math.Max(1, (int)Math.Ceiling(Math.Max(geometry.CanvasHeight, geometry.TextureRectY + geometry.TextureRectHeight))));
                }
            }

            return null;
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static SpriteGeometryProbe? ReadSpriteGeometry(
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
            return baseField is null
                ? null
                : ReadSpriteGeometry(baseField);
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static SpriteGeometryProbe? ReadSpriteGeometry(AssetTypeValueField spriteField)
    {
        var textureRectField = AssetsToolsFieldReader.TryGetField(spriteField, "m_RD", "textureRect") ??
                               AssetsToolsFieldReader.TryGetField(spriteField, "m_Rect");
        var x = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(textureRectField, "x"));
        var y = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(textureRectField, "y"));
        var width = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(textureRectField, "width"));
        var height = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(textureRectField, "height"));
        var pivotField = AssetsToolsFieldReader.TryGetField(spriteField, "m_Pivot");
        var pivotX = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(pivotField, "x"));
        var pivotY = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(pivotField, "y"));
        if (!x.HasValue ||
            !y.HasValue ||
            !width.HasValue ||
            !height.HasValue ||
            !pivotX.HasValue ||
            !pivotY.HasValue)
        {
            return null;
        }

        return new SpriteGeometryProbe(
            x.Value,
            y.Value,
            width.Value,
            height.Value,
            pivotX.Value,
            pivotY.Value,
            Math.Max(width.Value, x.Value + width.Value),
            Math.Max(height.Value, y.Value + height.Value));
    }

    private static bool IsExpectedSpritePivot(
        SpriteGeometryProbe exportedSprite,
        WorkspaceSpriteEditGeometryDto geometry)
    {
        var localPivotX = geometry.PivotX - geometry.RegionX;
        var localPivotY = geometry.CanvasHeight - geometry.PivotY - (geometry.CanvasHeight - geometry.RegionY - geometry.RegionHeight);
        return AreClose(localPivotX, exportedSprite.PivotX) &&
               AreClose(localPivotY, exportedSprite.PivotY) ||
               AreClose(localPivotX / geometry.RegionWidth, exportedSprite.PivotX) &&
               AreClose(localPivotY / geometry.RegionHeight, exportedSprite.PivotY);
    }

    private static void AssertClose(double expected, double actual, string message)
    {
        if (!AreClose(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected: {expected:0.###}. Actual: {actual:0.###}.");
        }
    }

    private static bool AreClose(double expected, double actual) =>
        Math.Abs(expected - actual) <= 0.001d;

    private sealed record SpriteProbe(
        string SerializedFileName,
        long PathId,
        string AssetName,
        int CanvasWidth,
        int CanvasHeight);

    private sealed record SpriteGeometryProbe(
        double TextureRectX,
        double TextureRectY,
        double TextureRectWidth,
        double TextureRectHeight,
        double PivotX,
        double PivotY,
        double CanvasWidth,
        double CanvasHeight);
}
