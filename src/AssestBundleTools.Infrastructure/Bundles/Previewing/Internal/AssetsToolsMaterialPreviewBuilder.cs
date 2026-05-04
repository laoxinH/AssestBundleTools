using System.Globalization;
using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsMaterialPreviewBuilder
{
    private const string MaterialPreviewContentType = "application/vnd.assestbundletools.material-preview+json";
    private const int MaterialPreviewSchemaVersion = 1;
    private const int MaxTexturePropertyCount = 24;
    private const int MaxFloatPropertyCount = 32;
    private const int MaxIntegerPropertyCount = 32;
    private const int MaxColorPropertyCount = 24;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildMaterialPreview(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField materialField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        long serializedObjectSizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        ArgumentNullException.ThrowIfNull(assetsManager);
        ArgumentNullException.ThrowIfNull(serializedFiles);
        ArgumentNullException.ThrowIfNull(currentSerializedFile);
        ArgumentNullException.ThrowIfNull(materialField);
        ArgumentNullException.ThrowIfNull(internalSerializedFileLookup);

        var material = new AssetPreviewMaterialDto(
            SchemaVersion: MaterialPreviewSchemaVersion,
            Shader: ReadShader(assetsManager, serializedFiles, currentSerializedFile, materialField, internalSerializedFileLookup),
            TextureProperties: ReadTextureProperties(currentSerializedFile, materialField, internalSerializedFileLookup, out var textureListTruncated),
            FloatProperties: ReadFloatProperties(materialField, out var floatListTruncated),
            IntegerProperties: ReadIntegerProperties(materialField, out var integerListTruncated),
            ColorProperties: ReadColorProperties(materialField, out var colorListTruncated),
            IsTexturePropertyListTruncated: textureListTruncated,
            IsFloatPropertyListTruncated: floatListTruncated,
            IsIntegerPropertyListTruncated: integerListTruncated,
            IsColorPropertyListTruncated: colorListTruncated);

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Material,
            ContentType: MaterialPreviewContentType,
            Content: JsonSerializer.SerializeToUtf8Bytes(material, JsonSerializerOptions),
            PreviewInfoItems: BuildPreviewInfoItems(materialField, serializedObjectSizeInBytes),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static AssetPreviewMaterialShaderDto? ReadShader(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField materialField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        var shaderField = AssetsToolsFieldReader.TryGetField(materialField, "m_Shader");
        var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(shaderField, "m_FileID")) ?? 0;
        var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(shaderField, "m_PathID"));
        if (pathId is null || pathId.Value == 0)
        {
            return null;
        }

        var targetReference = TryResolveSerializedFileReference(
            currentSerializedFile,
            fileId,
            internalSerializedFileLookup,
            out var resolvedReference)
            ? resolvedReference
            : new AssetsToolsSerializedFileReference(
                SerializedFileName: currentSerializedFile.name,
                ExternalReferencePath: null,
                IsExternal: false);
        var shaderName = TryResolveAssetName(
            assetsManager,
            serializedFiles,
            targetReference.SerializedFileName,
            pathId.Value);

        return new AssetPreviewMaterialShaderDto(
            FileId: fileId,
            PathId: pathId.Value,
            Name: shaderName,
            SerializedFileName: targetReference.SerializedFileName,
            ExternalReferencePath: targetReference.ExternalReferencePath,
            IsExternal: targetReference.IsExternal);
    }

    private static IReadOnlyList<AssetPreviewMaterialTexturePropertyDto> ReadTextureProperties(
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField materialField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out bool isTruncated)
    {
        var texEnvArray = AssetsToolsFieldReader.TryGetField(
            materialField,
            "m_SavedProperties",
            "m_TexEnvs",
            "Array");
        if (texEnvArray?.Children is not { Count: > 0 } children)
        {
            isTruncated = false;
            return Array.Empty<AssetPreviewMaterialTexturePropertyDto>();
        }

        isTruncated = children.Count > MaxTexturePropertyCount;
        var rows = new List<AssetPreviewMaterialTexturePropertyDto>(Math.Min(children.Count, MaxTexturePropertyCount));
        for (var index = 0; index < children.Count && rows.Count < MaxTexturePropertyCount; index++)
        {
            var entry = children[index];
            var name = ReadEntryName(entry, index);
            var valueField = TryGetEntryValue(entry);
            var texturePointer = AssetsToolsFieldReader.TryGetField(valueField, "m_Texture") ?? valueField;
            var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(texturePointer, "m_FileID")) ?? 0;
            var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(texturePointer, "m_PathID"));
            var scale = ReadVector2(AssetsToolsFieldReader.TryGetField(valueField, "m_Scale"));
            var offset = ReadVector2(AssetsToolsFieldReader.TryGetField(valueField, "m_Offset"));

            AssetsToolsSerializedFileReference? targetReference = null;
            if (pathId is { } texturePathId &&
                texturePathId != 0 &&
                TryResolveSerializedFileReference(currentSerializedFile, fileId, internalSerializedFileLookup, out var resolvedReference))
            {
                targetReference = resolvedReference;
            }

            rows.Add(new AssetPreviewMaterialTexturePropertyDto(
                Name: name,
                FileId: fileId,
                PathId: pathId is 0 ? null : pathId,
                SerializedFileName: targetReference?.SerializedFileName,
                ExternalReferencePath: targetReference?.ExternalReferencePath,
                IsExternal: targetReference?.IsExternal ?? false,
                ScaleX: scale?.X,
                ScaleY: scale?.Y,
                OffsetX: offset?.X,
                OffsetY: offset?.Y));
        }

        return rows;
    }

    private static IReadOnlyList<AssetPreviewMaterialFloatPropertyDto> ReadFloatProperties(
        AssetTypeValueField materialField,
        out bool isTruncated)
    {
        var floatArray = AssetsToolsFieldReader.TryGetField(
            materialField,
            "m_SavedProperties",
            "m_Floats",
            "Array");
        if (floatArray?.Children is not { Count: > 0 } children)
        {
            isTruncated = false;
            return Array.Empty<AssetPreviewMaterialFloatPropertyDto>();
        }

        isTruncated = children.Count > MaxFloatPropertyCount;
        var rows = new List<AssetPreviewMaterialFloatPropertyDto>(Math.Min(children.Count, MaxFloatPropertyCount));
        for (var index = 0; index < children.Count && rows.Count < MaxFloatPropertyCount; index++)
        {
            var value = AssetsToolsFieldReader.ReadFloat(TryGetEntryValue(children[index]));
            if (value.HasValue)
            {
                rows.Add(new AssetPreviewMaterialFloatPropertyDto(ReadEntryName(children[index], index), value.Value));
            }
        }

        return rows;
    }

    private static IReadOnlyList<AssetPreviewMaterialIntegerPropertyDto> ReadIntegerProperties(
        AssetTypeValueField materialField,
        out bool isTruncated)
    {
        var intArray = AssetsToolsFieldReader.TryGetField(
            materialField,
            "m_SavedProperties",
            "m_Ints",
            "Array");
        if (intArray?.Children is not { Count: > 0 } children)
        {
            isTruncated = false;
            return Array.Empty<AssetPreviewMaterialIntegerPropertyDto>();
        }

        isTruncated = children.Count > MaxIntegerPropertyCount;
        var rows = new List<AssetPreviewMaterialIntegerPropertyDto>(Math.Min(children.Count, MaxIntegerPropertyCount));
        for (var index = 0; index < children.Count && rows.Count < MaxIntegerPropertyCount; index++)
        {
            var value = AssetsToolsFieldReader.ReadInt64(TryGetEntryValue(children[index]));
            if (value.HasValue)
            {
                rows.Add(new AssetPreviewMaterialIntegerPropertyDto(ReadEntryName(children[index], index), value.Value));
            }
        }

        return rows;
    }

    private static IReadOnlyList<AssetPreviewMaterialColorPropertyDto> ReadColorProperties(
        AssetTypeValueField materialField,
        out bool isTruncated)
    {
        var colorArray = AssetsToolsFieldReader.TryGetField(
            materialField,
            "m_SavedProperties",
            "m_Colors",
            "Array");
        if (colorArray?.Children is not { Count: > 0 } children)
        {
            isTruncated = false;
            return Array.Empty<AssetPreviewMaterialColorPropertyDto>();
        }

        isTruncated = children.Count > MaxColorPropertyCount;
        var rows = new List<AssetPreviewMaterialColorPropertyDto>(Math.Min(children.Count, MaxColorPropertyCount));
        for (var index = 0; index < children.Count && rows.Count < MaxColorPropertyCount; index++)
        {
            var color = ReadColor(TryGetEntryValue(children[index]));
            if (color is not null)
            {
                rows.Add(new AssetPreviewMaterialColorPropertyDto(
                    ReadEntryName(children[index], index),
                    color.R,
                    color.G,
                    color.B,
                    color.A));
            }
        }

        return rows;
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AssetTypeValueField materialField,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MaterialTexturePropertyCount, CountArray(materialField, "m_SavedProperties", "m_TexEnvs"));
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MaterialFloatPropertyCount, CountArray(materialField, "m_SavedProperties", "m_Floats"));
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MaterialColorPropertyCount, CountArray(materialField, "m_SavedProperties", "m_Colors"));
        return items;
    }

    private static string ReadEntryName(AssetTypeValueField entry, int index)
    {
        var name = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "first")) ??
                   AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "key"));
        return string.IsNullOrWhiteSpace(name)
            ? string.Create(CultureInfo.InvariantCulture, $"Property #{index + 1}")
            : name;
    }

    private static AssetTypeValueField? TryGetEntryValue(AssetTypeValueField? entry)
    {
        return AssetsToolsFieldReader.TryGetField(entry, "second") ??
               AssetsToolsFieldReader.TryGetField(entry, "value");
    }

    private static MaterialVector2? ReadVector2(AssetTypeValueField? vectorField)
    {
        var x = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "x"));
        var y = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "y"));
        return x.HasValue && y.HasValue
            ? new MaterialVector2(x.Value, y.Value)
            : null;
    }

    private static MaterialColor? ReadColor(AssetTypeValueField? colorField)
    {
        var r = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(colorField, "r"));
        var g = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(colorField, "g"));
        var b = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(colorField, "b"));
        var a = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(colorField, "a"));
        return r.HasValue && g.HasValue && b.HasValue && a.HasValue
            ? new MaterialColor(r.Value, g.Value, b.Value, a.Value)
            : null;
    }

    private static bool TryResolveSerializedFileReference(
        AssetsFileInstance currentSerializedFile,
        long fileId,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out AssetsToolsSerializedFileReference targetReference)
    {
        try
        {
            return AssetsToolsBundleDependencyResolver.TryResolveTargetSerializedFileReference(
                currentSerializedFile,
                fileId,
                internalSerializedFileLookup,
                out targetReference);
        }
        catch (OverflowException)
        {
            targetReference = default;
            return false;
        }
    }

    private static string? TryResolveAssetName(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        string serializedFileName,
        long pathId)
    {
        var targetSerializedFile = serializedFiles.FirstOrDefault(file =>
            string.Equals(file.name, serializedFileName, StringComparison.OrdinalIgnoreCase));
        if (targetSerializedFile is null ||
            !AssetsToolsFieldReader.TryFindAssetInfoByPathId(targetSerializedFile, pathId, out var assetInfo))
        {
            return null;
        }

        var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, targetSerializedFile, assetInfo);
        return AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name"));
    }

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }

    private sealed record MaterialVector2(double X, double Y);

    private sealed record MaterialColor(double R, double G, double B, double A);
}
