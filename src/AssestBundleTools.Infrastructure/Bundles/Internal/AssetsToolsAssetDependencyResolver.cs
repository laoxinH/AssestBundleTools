using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 负责从资源字段树中提取可稳定解析的资源级依赖关系。
/// </summary>
internal static class AssetsToolsAssetDependencyResolver
{
    public static IReadOnlyList<AssetBundleReadAssetDependencyDto> ResolveDependencies(
        string sourceAssetId,
        AssetsFileInstance serializedFile,
        AssetTypeValueField? baseField,
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
        var pendingFields = new Stack<AssetTypeValueField>();
        pendingFields.Push(baseField);

        while (pendingFields.Count > 0)
        {
            var currentField = pendingFields.Pop();

            if (TryResolvePointerTarget(
                    currentField,
                    serializedFile,
                    internalSerializedFileLookup,
                    out var targetObjectKey) &&
                assetLookup.TryGetValue(targetObjectKey, out var targetAsset) &&
                !string.Equals(targetAsset.AssetId, sourceAssetId, StringComparison.Ordinal) &&
                seenAssetIds.Add(targetAsset.AssetId))
            {
                dependencies.Add(new AssetBundleReadAssetDependencyDto(
                    AssetId: targetAsset.AssetId,
                    AssetName: targetAsset.AssetName,
                    AssetType: targetAsset.AssetType,
                    PathId: targetAsset.PathId,
                    AssetTypeDisplayName: targetAsset.AssetTypeDisplayName));
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

        return dependencies;
    }

    private static bool TryResolvePointerTarget(
        AssetTypeValueField field,
        AssetsFileInstance serializedFile,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out AssetsToolsAssetObjectKey targetObjectKey)
    {
        targetObjectKey = default;

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

        if (!AssetsToolsBundleDependencyResolver.TryResolveTargetSerializedFileName(
                serializedFile,
                fileId,
                internalSerializedFileLookup,
                out var targetSerializedFileName))
        {
            return false;
        }

        targetObjectKey = new AssetsToolsAssetObjectKey(targetSerializedFileName, pathId.Value);
        return true;
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
}

internal sealed record AssetsToolsResolvedAssetDescriptor(
    string AssetId,
    string AssetName,
    string AssetType,
    string? AssetTypeDisplayName,
    string SerializedFileName,
    long PathId);
