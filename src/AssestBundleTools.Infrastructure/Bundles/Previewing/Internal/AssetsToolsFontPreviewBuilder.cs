using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsFontPreviewBuilder
{
    private const string FontPreviewContentType = "application/vnd.assestbundletools.font-preview+json";
    private const int FontPreviewSchemaVersion = 1;
    private const int MaxCharacterCount = 64;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildFontPreview(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField fontField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        long serializedObjectSizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        ArgumentNullException.ThrowIfNull(assetsManager);
        ArgumentNullException.ThrowIfNull(serializedFiles);
        ArgumentNullException.ThrowIfNull(currentSerializedFile);
        ArgumentNullException.ThrowIfNull(fontField);
        ArgumentNullException.ThrowIfNull(internalSerializedFileLookup);

        var characters = ReadCharacters(fontField, out var characterListTruncated);
        var font = new AssetPreviewFontDto(
            SchemaVersion: FontPreviewSchemaVersion,
            FontName: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(fontField, "m_Name")),
            FontSize: ReadNumber(fontField, "m_FontSize"),
            LineSpacing: ReadNumber(fontField, "m_LineSpacing"),
            Ascent: ReadNumber(fontField, "m_Ascent"),
            Descent: ReadNumber(fontField, "m_Descent"),
            DefaultSize: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(fontField, "m_DefaultSize")),
            AsciiStartOffset: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(fontField, "m_AsciiStartOffset")),
            Tracking: ReadNumber(fontField, "m_Tracking"),
            CharacterSpacing: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(fontField, "m_CharacterSpacing")),
            CharacterPadding: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(fontField, "m_CharacterPadding")),
            CharacterCount: ReadCharacterCount(fontField),
            FontDataSizeInBytes: ReadFontDataSize(fontField),
            Texture: ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                AssetsToolsFieldReader.TryGetField(fontField, "m_Texture"),
                internalSerializedFileLookup),
            DefaultMaterial: ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                AssetsToolsFieldReader.TryGetField(fontField, "m_DefaultMaterial"),
                internalSerializedFileLookup),
            Characters: characters,
            IsCharacterListTruncated: characterListTruncated);

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Font,
            ContentType: FontPreviewContentType,
            Content: JsonSerializer.SerializeToUtf8Bytes(font, JsonSerializerOptions),
            PreviewInfoItems: BuildPreviewInfoItems(fontField, font, serializedObjectSizeInBytes),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static IReadOnlyList<AssetPreviewFontCharacterDto> ReadCharacters(
        AssetTypeValueField fontField,
        out bool isTruncated)
    {
        var characterArray = AssetsToolsFieldReader.TryGetField(fontField, "m_CharacterRects", "Array") ??
                             AssetsToolsFieldReader.TryGetField(fontField, "m_Characters", "Array");
        if (characterArray?.Children is not { Count: > 0 } children)
        {
            isTruncated = false;
            return Array.Empty<AssetPreviewFontCharacterDto>();
        }

        isTruncated = children.Count > MaxCharacterCount;
        var rows = new List<AssetPreviewFontCharacterDto>(Math.Min(children.Count, MaxCharacterCount));
        for (var index = 0; index < children.Count && rows.Count < MaxCharacterCount; index++)
        {
            var character = children[index];
            var codePoint = ReadCharacterCodePoint(character);
            var uv = ReadRect(AssetsToolsFieldReader.TryGetField(character, "uv") ??
                              AssetsToolsFieldReader.TryGetField(character, "m_Uv"));
            var vertex = ReadRect(AssetsToolsFieldReader.TryGetField(character, "vert") ??
                                  AssetsToolsFieldReader.TryGetField(character, "m_Vert"));

            rows.Add(new AssetPreviewFontCharacterDto(
                CodePoint: codePoint,
                DisplayText: CreateDisplayText(codePoint),
                UvX: uv?.X,
                UvY: uv?.Y,
                UvWidth: uv?.Width,
                UvHeight: uv?.Height,
                VertexX: vertex?.X,
                VertexY: vertex?.Y,
                VertexWidth: vertex?.Width,
                VertexHeight: vertex?.Height,
                Advance: ReadNumber(character, "advance") ??
                         ReadNumber(character, "m_Advance") ??
                         ReadNumber(character, "width") ??
                         ReadNumber(character, "m_Width"),
                IsFlipped: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(character, "flipped")) ??
                           AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(character, "m_Flipped"))));
        }

        return rows;
    }

    private static AssetPreviewFontReferenceDto? ReadReference(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField? referenceField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(referenceField, "m_FileID")) ?? 0;
        var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(referenceField, "m_PathID"));
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

        return new AssetPreviewFontReferenceDto(
            FileId: fileId,
            PathId: pathId.Value,
            Name: TryResolveAssetName(assetsManager, serializedFiles, targetReference.SerializedFileName, pathId.Value),
            SerializedFileName: targetReference.SerializedFileName,
            ExternalReferencePath: targetReference.ExternalReferencePath,
            IsExternal: targetReference.IsExternal);
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AssetTypeValueField fontField,
        AssetPreviewFontDto font,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AssetsToolsImagePreviewSupport.AddDecimalInfo(items, AssetPreviewInfoKey.FontLineSpacing, ToFloat(font.LineSpacing));
        AssetsToolsImagePreviewSupport.AddDecimalInfo(items, AssetPreviewInfoKey.FontAscent, ToFloat(font.Ascent));
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.FontDefaultSize, font.DefaultSize);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.FontAsciiStartOffset, font.AsciiStartOffset);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.FontCharacterCount, font.CharacterCount);
        AssetsToolsImagePreviewSupport.AddInteger64Info(items, AssetPreviewInfoKey.FontTexturePathId, font.Texture?.PathId);
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.FontDataSize, ReadFontDataSize(fontField));
        return items;
    }

    private static long? ReadCharacterCodePoint(AssetTypeValueField character)
    {
        return AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(character, "index")) ??
               AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(character, "m_Index")) ??
               AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(character, "char")) ??
               AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(character, "m_Char"));
    }

    private static FontRect? ReadRect(AssetTypeValueField? rectField)
    {
        if (rectField is null)
        {
            return null;
        }

        var x = ReadNumber(rectField, "x");
        var y = ReadNumber(rectField, "y");
        var width = ReadNumber(rectField, "width") ?? ReadNumber(rectField, "w");
        var height = ReadNumber(rectField, "height") ?? ReadNumber(rectField, "h");
        return x.HasValue && y.HasValue && width.HasValue && height.HasValue
            ? new FontRect(x.Value, y.Value, width.Value, height.Value)
            : null;
    }

    private static double? ReadNumber(AssetTypeValueField baseField, params string[] path)
    {
        var field = path.Length == 0
            ? baseField
            : AssetsToolsFieldReader.TryGetField(baseField, path);
        var floatValue = AssetsToolsFieldReader.ReadFloat(field);
        if (floatValue.HasValue)
        {
            return floatValue.Value;
        }

        return AssetsToolsFieldReader.ReadInt64(field);
    }

    private static long? ReadFontDataSize(AssetTypeValueField fontField)
    {
        var fontDataField = AssetsToolsFieldReader.TryGetField(fontField, "m_FontData");
        var byteArray = AssetsToolsFieldReader.ReadByteArray(fontDataField);
        if (byteArray is { Length: > 0 })
        {
            return byteArray.LongLength;
        }

        var arrayCount = AssetsToolsFieldReader.CountChildren(AssetsToolsFieldReader.TryGetField(fontDataField, "Array"));
        return arrayCount > 0 ? arrayCount : null;
    }

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }

    private static int ReadCharacterCount(AssetTypeValueField fontField)
    {
        var characterRectsCount = CountArray(fontField, "m_CharacterRects");
        return characterRectsCount > 0
            ? characterRectsCount
            : CountArray(fontField, "m_Characters");
    }

    private static string? CreateDisplayText(long? codePoint)
    {
        if (codePoint is null || codePoint.Value < 0 || codePoint.Value > 0x10FFFF)
        {
            return null;
        }

        try
        {
            var text = char.ConvertFromUtf32((int)codePoint.Value);
            return text.All(char.IsControl) ? null : text;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
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

    private static float? ToFloat(double? value)
    {
        return value.HasValue
            ? (float)value.Value
            : null;
    }

    private sealed record FontRect(double X, double Y, double Width, double Height);
}
