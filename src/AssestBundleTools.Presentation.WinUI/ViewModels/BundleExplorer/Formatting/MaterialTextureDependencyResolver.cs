using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal static class MaterialTextureDependencyResolver
{
    public static AssetDependencyDto? ResolveTextureDependency(
        AssetPreviewMaterialTexturePropertyDto property,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(details);

        if (property.PathId is not { } pathId || pathId == 0)
        {
            return null;
        }

        return details.Dependencies
            .Where(dependency => dependency.PathId == pathId)
            .OrderBy(static dependency => IsTextureType(dependency.AssetType) ? 0 : 1)
            .ThenBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static dependency => IsTextureType(dependency.AssetType));
    }

    public static IReadOnlyList<AssetDependencyDto> CollectPreviewCandidates(
        AssetPreviewMaterialDto material,
        AssetDetailsDto details,
        int maxCount)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(details);

        if (maxCount <= 0 || material.TextureProperties.Count == 0)
        {
            return Array.Empty<AssetDependencyDto>();
        }

        return material.TextureProperties
            .Select(property => ResolveTextureDependency(property, details))
            .Where(static dependency => dependency is not null)
            .Select(static dependency => dependency!)
            .DistinctBy(static dependency => dependency.AssetId.Value)
            .Take(maxCount)
            .ToArray();
    }

    private static bool IsTextureType(AssetType assetType) =>
        assetType is AssetType.Texture2D or AssetType.Sprite;
}
