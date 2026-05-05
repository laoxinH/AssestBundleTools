using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsGameObjectPreviewBuilder
{
    private const string GameObjectPreviewContentType = "application/vnd.assestbundletools.game-object-preview+json";
    private const int GameObjectPreviewSchemaVersion = 1;
    private const int MaxComponentCount = 64;
    private const int TransformClassId = 4;
    private const int RectTransformClassId = 224;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildGameObjectPreview(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField gameObjectField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        long serializedObjectSizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        ArgumentNullException.ThrowIfNull(assetsManager);
        ArgumentNullException.ThrowIfNull(serializedFiles);
        ArgumentNullException.ThrowIfNull(currentSerializedFile);
        ArgumentNullException.ThrowIfNull(gameObjectField);
        ArgumentNullException.ThrowIfNull(internalSerializedFileLookup);

        var componentReadResults = ReadComponents(
            assetsManager,
            serializedFiles,
            currentSerializedFile,
            gameObjectField,
            internalSerializedFileLookup,
            out var isComponentListTruncated,
            out var componentCount);
        var gameObject = new AssetPreviewGameObjectDto(
            SchemaVersion: GameObjectPreviewSchemaVersion,
            Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(gameObjectField, "m_Name")),
            Layer: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(gameObjectField, "m_Layer")),
            Tag: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(gameObjectField, "m_TagString")),
            IsActive: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(gameObjectField, "m_IsActive")),
            StaticEditorFlags: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(gameObjectField, "m_StaticEditorFlags")),
            ComponentCount: componentCount,
            Components: componentReadResults.Select(static result => result.Component).ToArray(),
            IsComponentListTruncated: isComponentListTruncated,
            Transform: ReadTransform(componentReadResults));

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.GameObject,
            ContentType: GameObjectPreviewContentType,
            Content: JsonSerializer.SerializeToUtf8Bytes(gameObject, JsonSerializerOptions),
            PreviewInfoItems: BuildPreviewInfoItems(gameObjectField, gameObject, serializedObjectSizeInBytes),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static IReadOnlyList<GameObjectComponentReadResult> ReadComponents(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField gameObjectField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out bool isTruncated,
        out int componentCount)
    {
        var componentArray = AssetsToolsFieldReader.TryGetField(gameObjectField, "m_Component", "Array");
        if (componentArray?.Children is not { Count: > 0 } children)
        {
            isTruncated = false;
            componentCount = 0;
            return Array.Empty<GameObjectComponentReadResult>();
        }

        componentCount = children.Count;
        isTruncated = children.Count > MaxComponentCount;
        var rows = new List<GameObjectComponentReadResult>(Math.Min(children.Count, MaxComponentCount));
        for (var index = 0; index < children.Count && rows.Count < MaxComponentCount; index++)
        {
            rows.Add(ReadComponent(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                children[index],
                internalSerializedFileLookup,
                index));
        }

        return rows;
    }

    private static GameObjectComponentReadResult ReadComponent(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField componentEntry,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        int index)
    {
        var componentPointer = AssetsToolsFieldReader.TryGetField(componentEntry, "component") ??
                               AssetsToolsFieldReader.TryGetField(componentEntry, "second") ??
                               componentEntry;
        var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(componentPointer, "m_FileID")) ?? 0;
        var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(componentPointer, "m_PathID"));
        var targetReference = TryResolveSerializedFileReference(
            currentSerializedFile,
            fileId,
            internalSerializedFileLookup,
            out var resolvedReference)
            ? resolvedReference
            : new AssetsToolsSerializedFileReference(
                currentSerializedFile.name,
                ExternalReferencePath: null,
                IsExternal: false);

        var targetSerializedFile = serializedFiles.FirstOrDefault(file =>
            string.Equals(file.name, targetReference.SerializedFileName, StringComparison.OrdinalIgnoreCase));
        AssetTypeValueField? componentField = null;
        int? rawTypeId = null;
        if (targetSerializedFile is not null &&
            pathId.HasValue &&
            AssetsToolsFieldReader.TryFindAssetInfoByPathId(targetSerializedFile, pathId.Value, out var assetInfo))
        {
            componentField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, targetSerializedFile, assetInfo);
            rawTypeId = assetInfo.TypeId;
        }

        var scriptReference = componentField is not null && targetSerializedFile is not null
            ? ReadScriptReference(
                assetsManager,
                serializedFiles,
                targetSerializedFile,
                componentField,
                internalSerializedFileLookup)
            : null;
        var references = componentField is not null && targetSerializedFile is not null
            ? AssetsToolsGameObjectComponentReferenceReader.ReadComponentReferences(
                assetsManager,
                serializedFiles,
                targetSerializedFile,
                componentField,
                internalSerializedFileLookup)
            : Array.Empty<AssetPreviewGameObjectReferenceDto>();
        var typeName = ResolveTypeName(rawTypeId);
        var summaries = componentField is not null
            ? AssetsToolsGameObjectComponentSummaryReader.ReadComponentSummaries(
                componentField,
                typeName,
                scriptReference?.Name,
                references)
            : Array.Empty<AssetPreviewGameObjectComponentSummaryDto>();

        var component = new AssetPreviewGameObjectComponentDto(
            Index: index,
            FileId: fileId,
            PathId: pathId is 0 ? null : pathId,
            TypeId: rawTypeId,
            TypeName: typeName,
            Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(componentField, "m_Name")),
            SerializedFileName: targetReference.SerializedFileName,
            ExternalReferencePath: targetReference.ExternalReferencePath,
            IsExternal: targetReference.IsExternal,
            IsEnabled: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(componentField, "m_Enabled")),
            ScriptPathId: scriptReference?.PathId,
            ScriptName: scriptReference?.Name,
            References: references,
            Summaries: summaries);

        return new GameObjectComponentReadResult(component, rawTypeId, componentField);
    }

    private static AssetPreviewGameObjectTransformDto? ReadTransform(
        IReadOnlyList<GameObjectComponentReadResult> componentReadResults)
    {
        var transform = componentReadResults.FirstOrDefault(static component =>
            component.TypeId is TransformClassId or RectTransformClassId &&
            component.ComponentField is not null);
        if (transform?.ComponentField is null)
        {
            return null;
        }

        var transformField = transform.ComponentField;
        var position = ReadVector3(AssetsToolsFieldReader.TryGetField(transformField, "m_LocalPosition"));
        var rotation = ReadQuaternion(AssetsToolsFieldReader.TryGetField(transformField, "m_LocalRotation"));
        var scale = ReadVector3(AssetsToolsFieldReader.TryGetField(transformField, "m_LocalScale"));
        var parentPathId = ReadPathId(AssetsToolsFieldReader.TryGetField(transformField, "m_Father"));
        var childCount = CountArray(transformField, "m_Children");

        return new AssetPreviewGameObjectTransformDto(
            PathId: transform.Component.PathId,
            TypeName: transform.Component.TypeName,
            ParentPathId: parentPathId,
            ChildCount: childCount,
            LocalPositionX: position?.X,
            LocalPositionY: position?.Y,
            LocalPositionZ: position?.Z,
            LocalRotationX: rotation?.X,
            LocalRotationY: rotation?.Y,
            LocalRotationZ: rotation?.Z,
            LocalRotationW: rotation?.W,
            LocalScaleX: scale?.X,
            LocalScaleY: scale?.Y,
            LocalScaleZ: scale?.Z);
    }

    private static GameObjectScriptReference? ReadScriptReference(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField componentField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        var scriptField = AssetsToolsFieldReader.TryGetField(componentField, "m_Script");
        var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(scriptField, "m_FileID")) ?? 0;
        var pathId = ReadPathId(scriptField);
        if (!pathId.HasValue)
        {
            return null;
        }

        if (!TryResolveSerializedFileReference(
                currentSerializedFile,
                fileId,
                internalSerializedFileLookup,
                out var targetReference))
        {
            return new GameObjectScriptReference(pathId.Value, null);
        }

        var name = TryResolveScriptName(
            assetsManager,
            serializedFiles,
            targetReference.SerializedFileName,
            pathId.Value);
        return new GameObjectScriptReference(pathId.Value, name);
    }

    private static string? TryResolveScriptName(
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

        var scriptField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, targetSerializedFile, assetInfo);
        return AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(scriptField, "m_ClassName")) ??
               AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(scriptField, "m_Name"));
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AssetTypeValueField gameObjectField,
        AssetPreviewGameObjectDto gameObject,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.GameObjectComponentCount, gameObject.ComponentCount);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.GameObjectLayer, gameObject.Layer);
        AssetsToolsImagePreviewSupport.AddBooleanInfo(items, AssetPreviewInfoKey.GameObjectIsActive, gameObject.IsActive);
        return items;
    }

    private static string? ResolveTypeName(int? rawTypeId)
    {
        if (!rawTypeId.HasValue)
        {
            return null;
        }

        return Enum.IsDefined(typeof(AssetClassID), rawTypeId.Value)
            ? ((AssetClassID)rawTypeId.Value).ToString()
            : null;
    }

    private static GameObjectVector3? ReadVector3(AssetTypeValueField? vectorField)
    {
        var x = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "x"));
        var y = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "y"));
        var z = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "z"));
        return x.HasValue && y.HasValue && z.HasValue
            ? new GameObjectVector3(x.Value, y.Value, z.Value)
            : null;
    }

    private static GameObjectQuaternion? ReadQuaternion(AssetTypeValueField? vectorField)
    {
        var x = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "x"));
        var y = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "y"));
        var z = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "z"));
        var w = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "w"));
        return x.HasValue && y.HasValue && z.HasValue && w.HasValue
            ? new GameObjectQuaternion(x.Value, y.Value, z.Value, w.Value)
            : null;
    }

    private static long? ReadPathId(AssetTypeValueField? pptrField)
    {
        var value = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(pptrField, "m_PathID"));
        return value is 0 ? null : value;
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

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }

    private sealed record GameObjectComponentReadResult(
        AssetPreviewGameObjectComponentDto Component,
        int? TypeId,
        AssetTypeValueField? ComponentField);

    private sealed record GameObjectScriptReference(long PathId, string? Name);

    private sealed record GameObjectVector3(double X, double Y, double Z);

    private sealed record GameObjectQuaternion(double X, double Y, double Z, double W);
}
