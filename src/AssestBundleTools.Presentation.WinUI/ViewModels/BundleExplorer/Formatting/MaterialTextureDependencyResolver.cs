using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal static class MaterialTextureDependencyResolver
{
    private const string MainTexturePropertyName = "_MainTex";

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

    public static AssetPreviewMaterialTexturePropertyDto? SelectMainTextureProperty(AssetPreviewMaterialDto material)
    {
        ArgumentNullException.ThrowIfNull(material);

        return material.TextureProperties.FirstOrDefault(static property =>
                   HasTexturePath(property) &&
                   string.Equals(property.Name, MainTexturePropertyName, StringComparison.OrdinalIgnoreCase))
               ?? material.TextureProperties.FirstOrDefault(HasTexturePath);
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

        var orderedProperties = new List<AssetPreviewMaterialTexturePropertyDto>();
        var mainTextureProperty = SelectMainTextureProperty(material);
        if (mainTextureProperty is not null)
        {
            orderedProperties.Add(mainTextureProperty);
        }

        orderedProperties.AddRange(material.TextureProperties.Where(property => !ReferenceEquals(property, mainTextureProperty)));

        return orderedProperties
            .Select(property => ResolveTextureDependency(property, details))
            .Where(static dependency => dependency is not null)
            .Select(static dependency => dependency!)
            .DistinctBy(static dependency => dependency.AssetId.Value)
            .Take(maxCount)
            .ToArray();
    }

    private static bool HasTexturePath(AssetPreviewMaterialTexturePropertyDto property) =>
        property.PathId is { } pathId && pathId != 0;

    private static bool IsTextureType(AssetType assetType) =>
        assetType is AssetType.Texture2D or AssetType.Sprite;
}
