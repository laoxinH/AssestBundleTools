using System.IO;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 统一维护文件替换流程中“资源类型 <-> 文件扩展名”的轻量兼容规则。
/// </summary>
/// <remarks>
/// 当前阶段仍处于 M8 骨架期，这里的规则主要服务于两件事：
/// 1. 为 Presentation 层文件选择器提供更合理的扩展名过滤。
/// 2. 为 Application 层候选查询提供基础兼容性判断，避免把明显不兼容的文件带入应用替换。
/// 后续接入真实 AB 解析和更严格校验时，可以继续把规则收敛到专门的替换校验服务中。
/// </remarks>
public static class ReplacementFileTypeCatalog
{
    private static readonly IReadOnlyDictionary<AssetType, string[]> CompatibleExtensionsByAssetType =
        new Dictionary<AssetType, string[]>
        {
            [AssetType.Texture2D] = [".png", ".jpg", ".jpeg", ".bmp", ".tga", ".dds"],
            [AssetType.Sprite] = [".png", ".jpg", ".jpeg", ".bmp", ".tga", ".dds"],
            [AssetType.TextAsset] = [".txt", ".json", ".xml", ".bytes", ".csv"],
            [AssetType.AudioClip] = [".wav", ".mp3", ".ogg"],
            [AssetType.Mesh] = [".mesh", ".fbx", ".obj"],
            [AssetType.Material] = [".mat"],
            [AssetType.Unknown] = [".bin"]
        };

    private static readonly IReadOnlyDictionary<string, AssetType[]> CandidateAssetTypesByExtension =
        new Dictionary<string, AssetType[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = [AssetType.Texture2D, AssetType.Sprite],
            [".jpg"] = [AssetType.Texture2D, AssetType.Sprite],
            [".jpeg"] = [AssetType.Texture2D, AssetType.Sprite],
            [".bmp"] = [AssetType.Texture2D, AssetType.Sprite],
            [".tga"] = [AssetType.Texture2D, AssetType.Sprite],
            [".dds"] = [AssetType.Texture2D, AssetType.Sprite],
            [".txt"] = [AssetType.TextAsset],
            [".json"] = [AssetType.TextAsset],
            [".xml"] = [AssetType.TextAsset],
            [".bytes"] = [AssetType.TextAsset],
            [".csv"] = [AssetType.TextAsset],
            [".wav"] = [AssetType.AudioClip],
            [".mp3"] = [AssetType.AudioClip],
            [".ogg"] = [AssetType.AudioClip],
            [".mesh"] = [AssetType.Mesh],
            [".fbx"] = [AssetType.Mesh],
            [".obj"] = [AssetType.Mesh],
            [".mat"] = [AssetType.Material],
            [".bin"] = [AssetType.Unknown]
        };

    private static readonly string[] AllKnownExtensions = CompatibleExtensionsByAssetType.Values
        .SelectMany(static extensions => extensions)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(static extension => extension, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// 返回目标资源类型当前允许选择的扩展名列表。
    /// 若目标类型尚未建模，则回退到已知扩展名全集，避免文件选择器变成不可用状态。
    /// </summary>
    public static IReadOnlyList<string> GetCompatibleExtensions(AssetType targetAssetType)
    {
        if (CompatibleExtensionsByAssetType.TryGetValue(targetAssetType, out var extensions) &&
            extensions.Length > 0)
        {
            return extensions;
        }

        return AllKnownExtensions;
    }

    /// <summary>
    /// 根据目标资源类型和已选中文件路径，解析本次文件替换候选的资源类型。
    /// 优先使用“目标类型兼容”规则，避免像 Sprite / Texture2D 这类共享扩展名的类型被误判。
    /// </summary>
    public static AssetType ResolveCandidateAssetType(AssetType targetAssetType, string sourceFilePath)
    {
        var normalizedExtension = NormalizeExtension(Path.GetExtension(sourceFilePath));
        if (normalizedExtension is null)
        {
            return AssetType.Unknown;
        }

        if (targetAssetType != AssetType.Unknown &&
            IsExtensionCompatible(targetAssetType, normalizedExtension))
        {
            return targetAssetType;
        }

        if (CandidateAssetTypesByExtension.TryGetValue(normalizedExtension, out var candidateTypes) &&
            candidateTypes.Length > 0)
        {
            return candidateTypes[0];
        }

        return AssetType.Unknown;
    }

    /// <summary>
    /// 判断给定文件路径是否与目标资源类型兼容。
    /// </summary>
    public static bool IsFilePathCompatible(AssetType targetAssetType, string sourceFilePath)
    {
        var normalizedExtension = NormalizeExtension(Path.GetExtension(sourceFilePath));
        return normalizedExtension is not null &&
               IsExtensionCompatible(targetAssetType, normalizedExtension);
    }

    private static bool IsExtensionCompatible(AssetType targetAssetType, string extension)
    {
        if (!CompatibleExtensionsByAssetType.TryGetValue(targetAssetType, out var compatibleExtensions))
        {
            return false;
        }

        return compatibleExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string? NormalizeExtension(string? extension) =>
        string.IsNullOrWhiteSpace(extension) ? null : extension.Trim().ToLowerInvariant();
}
