using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsGameObjectComponentReferenceReader
{
    private const int MaxComponentReferenceCount = 32;
    private const string ReferenceRoleMesh = "mesh";
    private const string ReferenceRoleMaterial = "material";
    private const string ReferenceRoleSprite = "sprite";
    private const string ReferenceRoleController = "controller";
    private const string ReferenceRoleAvatar = "avatar";
    private const string ReferenceRoleAudioClip = "audio-clip";
    private const string ReferenceRoleScript = "script";

    public static IReadOnlyList<AssetPreviewGameObjectReferenceDto> ReadComponentReferences(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField componentField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        var references = new List<AssetPreviewGameObjectReferenceDto>();
        AddReference(
            references,
            ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                componentField,
                ReferenceRoleMesh,
                internalSerializedFileLookup,
                "m_Mesh"));
        AddReference(
            references,
            ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                componentField,
                ReferenceRoleSprite,
                internalSerializedFileLookup,
                "m_Sprite"));
        AddReference(
            references,
            ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                componentField,
                ReferenceRoleController,
                internalSerializedFileLookup,
                "m_Controller"));
        AddReference(
            references,
            ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                componentField,
                ReferenceRoleAvatar,
                internalSerializedFileLookup,
                "m_Avatar"));
        AddReference(
            references,
            ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                componentField,
                ReferenceRoleAudioClip,
                internalSerializedFileLookup,
                "m_audioClip"));
        AddReference(
            references,
            ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                componentField,
                ReferenceRoleAudioClip,
                internalSerializedFileLookup,
                "m_AudioClip"));
        AddReference(
            references,
            ReadReference(
                assetsManager,
                serializedFiles,
                currentSerializedFile,
                componentField,
                ReferenceRoleScript,
                internalSerializedFileLookup,
                "m_Script"));

        foreach (var materialReference in ReadReferenceArray(
                     assetsManager,
                     serializedFiles,
                     currentSerializedFile,
                     componentField,
                     ReferenceRoleMaterial,
                     internalSerializedFileLookup,
                     "m_Materials"))
        {
            AddReference(references, materialReference);
        }

        return references
            .DistinctBy(static reference => $"{reference.Role}|{reference.FileId}|{reference.PathId}")
            .Take(MaxComponentReferenceCount)
            .ToArray();
    }

    private static void AddReference(
        ICollection<AssetPreviewGameObjectReferenceDto> references,
        AssetPreviewGameObjectReferenceDto? reference)
    {
        if (reference is not null)
        {
            references.Add(reference);
        }
    }

    private static IReadOnlyList<AssetPreviewGameObjectReferenceDto> ReadReferenceArray(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField baseField,
        string role,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        if (arrayField?.Children is not { Count: > 0 } children)
        {
            return Array.Empty<AssetPreviewGameObjectReferenceDto>();
        }

        var references = new List<AssetPreviewGameObjectReferenceDto>(children.Count);
        foreach (var child in children)
        {
            AddReference(
                references,
                ReadReference(
                    assetsManager,
                    serializedFiles,
                    currentSerializedFile,
                    child,
                    role,
                    internalSerializedFileLookup));
        }

        return references;
    }

    private static AssetPreviewGameObjectReferenceDto? ReadReference(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField baseField,
        string role,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        params string[] path)
    {
        var referenceField = path.Length == 0
            ? baseField
            : AssetsToolsFieldReader.TryGetField(baseField, path);
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
                currentSerializedFile.name,
                ExternalReferencePath: null,
                IsExternal: false);
        var resolvedAsset = TryResolveAssetSummary(
            assetsManager,
            serializedFiles,
            targetReference.SerializedFileName,
            pathId.Value);

        return new AssetPreviewGameObjectReferenceDto(
            Role: role,
            FileId: fileId,
            PathId: pathId.Value,
            TypeName: resolvedAsset?.TypeName,
            Name: resolvedAsset?.Name,
            SerializedFileName: targetReference.SerializedFileName,
            ExternalReferencePath: targetReference.ExternalReferencePath,
            IsExternal: targetReference.IsExternal);
    }

    private static GameObjectResolvedAssetSummary? TryResolveAssetSummary(
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
        var name = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name")) ??
                   AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_ClassName"));
        return new GameObjectResolvedAssetSummary(
            ResolveTypeName(assetInfo.TypeId),
            name);
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

    private sealed record GameObjectResolvedAssetSummary(string? TypeName, string? Name);
}
