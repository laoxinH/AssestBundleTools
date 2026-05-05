using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 负责从资源字段树中提取可稳定解析的资源级依赖关系。
/// </summary>
internal static class AssetsToolsAssetDependencyResolver
{
    private const string GameObjectTypeDisplayName = "GameObject";

    public static IReadOnlyList<AssetBundleReadAssetDependencyDto> ResolveDependencies(
        string sourceAssetId,
        AssetsFileInstance serializedFile,
        AssetTypeValueField? baseField,
        string? assetTypeDisplayName,
        IReadOnlyDictionary<AssetsToolsAssetObjectKey, AssetsToolsResolvedAssetDescriptor> assetLookup,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        ArgumentNullException.ThrowIfNull(sourceAssetId);
        ArgumentNullException.ThrowIfNull(serializedFile);
        ArgumentNullException.ThrowIfNull(assetLookup);
        ArgumentNullException.ThrowIfNull(internalSerializedFileLookup);

        if (baseField is null)
        {
            return Array.Empty<AssetBundleReadAssetDependencyDto>();
        }

        var dependencies = new List<AssetBundleReadAssetDependencyDto>();
        var seenAssetIds = new HashSet<string>(StringComparer.Ordinal);
        AddDependenciesFromFieldTree(
            sourceAssetId,
            serializedFile,
            baseField,
            assetLookup,
            internalSerializedFileLookup,
            dependencies,
            seenAssetIds);

        foreach (var component in EnumerateGameObjectComponentDependencyRoots(
                     assetTypeDisplayName,
                     serializedFile,
                     baseField,
                     assetLookup,
                     internalSerializedFileLookup))
        {
            AddDependenciesFromFieldTree(
                sourceAssetId,
                component.SerializedFile,
                component.BaseField,
                assetLookup,
                internalSerializedFileLookup,
                dependencies,
                seenAssetIds);
        }

        return dependencies;
    }

    private static void AddDependenciesFromFieldTree(
        string sourceAssetId,
        AssetsFileInstance serializedFile,
        AssetTypeValueField baseField,
        IReadOnlyDictionary<AssetsToolsAssetObjectKey, AssetsToolsResolvedAssetDescriptor> assetLookup,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        ICollection<AssetBundleReadAssetDependencyDto> dependencies,
        ISet<string> seenAssetIds)
    {
        var pendingFields = new Stack<AssetTypeValueField>();
        pendingFields.Push(baseField);

        while (pendingFields.Count > 0)
        {
            var currentField = pendingFields.Pop();

            if (TryResolvePointerTarget(
                    currentField,
                    serializedFile,
                    internalSerializedFileLookup,
                    out var targetObjectKey,
                    out var targetReference))
            {
                if (assetLookup.TryGetValue(targetObjectKey, out var targetAsset) &&
                    !string.Equals(targetAsset.AssetId, sourceAssetId, StringComparison.Ordinal) &&
                    seenAssetIds.Add(targetAsset.AssetId))
                {
                    dependencies.Add(new AssetBundleReadAssetDependencyDto(
                        AssetId: targetAsset.AssetId,
                        AssetName: targetAsset.AssetName,
                        AssetType: targetAsset.AssetType,
                        PathId: targetAsset.PathId,
                        AssetTypeDisplayName: targetAsset.AssetTypeDisplayName,
                        SerializedFileName: targetAsset.SerializedFileName));
                }
                else if (targetReference.IsExternal)
                {
                    var externalAssetId = AssetsToolsAssetIdFactory.CreateAssetId(
                        targetReference.SerializedFileName,
                        targetObjectKey.PathId);
                    if (!string.Equals(externalAssetId, sourceAssetId, StringComparison.Ordinal) &&
                        seenAssetIds.Add(externalAssetId))
                    {
                        dependencies.Add(new AssetBundleReadAssetDependencyDto(
                            AssetId: externalAssetId,
                            AssetName: CreateExternalDependencyName(targetReference, targetObjectKey.PathId),
                            AssetType: "Unknown",
                            PathId: targetObjectKey.PathId,
                            AssetTypeDisplayName: null,
                            SerializedFileName: targetReference.SerializedFileName,
                            ExternalReferencePath: targetReference.ExternalReferencePath));
                    }
                }
            }

            if (currentField.Children is not { Count: > 0 })
            {
                continue;
            }

            for (var index = currentField.Children.Count - 1; index >= 0; index--)
            {
                pendingFields.Push(currentField.Children[index]);
            }
        }
    }

    private static bool TryResolvePointerTarget(
        AssetTypeValueField field,
        AssetsFileInstance serializedFile,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out AssetsToolsAssetObjectKey targetObjectKey,
        out AssetsToolsSerializedFileReference targetReference)
    {
        targetObjectKey = default;
        targetReference = default;

        if (!IsAssetPointerField(field))
        {
            return false;
        }

        var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(field, "m_FileID")) ?? 0;
        var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(field, "m_PathID"));
        // Unity 的 PathId 是有符号 long，负数同样可能是合法对象标识。
        // 这里不能把 <= 0 一律视为无效，否则像部分 Sprite -> Texture2D 引用就会被误过滤。
        if (pathId is null || pathId.Value == 0)
        {
            return false;
        }

        if (!AssetsToolsBundleDependencyResolver.TryResolveTargetSerializedFileReference(
                serializedFile,
                fileId,
                internalSerializedFileLookup,
                out targetReference))
        {
            return false;
        }

        targetObjectKey = new AssetsToolsAssetObjectKey(targetReference.SerializedFileName, pathId.Value);
        return true;
    }

    private static IEnumerable<GameObjectComponentDependencyRoot> EnumerateGameObjectComponentDependencyRoots(
        string? assetTypeDisplayName,
        AssetsFileInstance serializedFile,
        AssetTypeValueField gameObjectField,
        IReadOnlyDictionary<AssetsToolsAssetObjectKey, AssetsToolsResolvedAssetDescriptor> assetLookup,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        if (!string.Equals(assetTypeDisplayName, GameObjectTypeDisplayName, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var componentArray = AssetsToolsFieldReader.TryGetField(gameObjectField, "m_Component", "Array");
        if (componentArray?.Children is not { Count: > 0 } children)
        {
            yield break;
        }

        var seenComponentKeys = new HashSet<AssetsToolsAssetObjectKey>();
        foreach (var componentEntry in children)
        {
            var componentPointer = AssetsToolsFieldReader.TryGetField(componentEntry, "component") ??
                                   AssetsToolsFieldReader.TryGetField(componentEntry, "second") ??
                                   componentEntry;
            if (!TryResolvePointerTarget(
                    componentPointer,
                    serializedFile,
                    internalSerializedFileLookup,
                    out var componentKey,
                    out _) ||
                !seenComponentKeys.Add(componentKey) ||
                !assetLookup.TryGetValue(componentKey, out var componentAsset) ||
                componentAsset.BaseField is null)
            {
                continue;
            }

            yield return new GameObjectComponentDependencyRoot(
                componentAsset.SerializedFile,
                componentAsset.BaseField);
        }
    }

    private static string CreateExternalDependencyName(
        AssetsToolsSerializedFileReference targetReference,
        long pathId)
    {
        var serializedFileName = string.IsNullOrWhiteSpace(targetReference.SerializedFileName)
            ? targetReference.ExternalReferencePath
            : targetReference.SerializedFileName;
        return string.IsNullOrWhiteSpace(serializedFileName)
            ? $"External #{pathId}"
            : $"{serializedFileName} #{pathId}";
    }

    private static bool IsAssetPointerField(AssetTypeValueField field)
    {
        if (field is null)
        {
            return false;
        }

        return field.TypeName.StartsWith("PPtr<", StringComparison.OrdinalIgnoreCase) ||
               (field.Children?.Count == 2 &&
                AssetsToolsFieldReader.TryGetField(field, "m_FileID") is not null &&
                AssetsToolsFieldReader.TryGetField(field, "m_PathID") is not null);
    }

    private sealed record GameObjectComponentDependencyRoot(
        AssetsFileInstance SerializedFile,
        AssetTypeValueField BaseField);
}

internal sealed record AssetsToolsResolvedAssetDescriptor(
    string AssetId,
    string AssetName,
    string AssetType,
    string? AssetTypeDisplayName,
    string SerializedFileName,
    long PathId,
    AssetsFileInstance SerializedFile,
    AssetTypeValueField? BaseField);
