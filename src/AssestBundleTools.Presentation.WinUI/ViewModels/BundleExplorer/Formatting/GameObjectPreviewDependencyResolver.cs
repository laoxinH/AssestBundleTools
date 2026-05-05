using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal static class GameObjectPreviewDependencyResolver
{
    public static AssetDependencyDto? ResolvePathId(
        long? pathId,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (!pathId.HasValue || pathId.Value == 0)
        {
            return null;
        }

        return details.Dependencies
            .Where(dependency => dependency.PathId == pathId.Value)
            .OrderBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static AssetDependencyDto? ResolveReference(
        AssetPreviewGameObjectReferenceDto reference,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(details);

        if (!reference.PathId.HasValue || reference.PathId.Value == 0)
        {
            return null;
        }

        return details.Dependencies
            .Where(dependency => dependency.PathId == reference.PathId.Value)
            .OrderBy(dependency => MatchesReferenceType(reference, dependency) ? 0 : 1)
            .ThenBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static ResolvedAssetNavigationTarget CreateNavigationTarget(AssetDependencyDto dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        return new ResolvedAssetNavigationTarget(
            dependency.AssetId,
            dependency.BundleId,
            dependency.PathId,
            dependency.Name,
            ResolveAssetTypeDisplayName(dependency));
    }

    public static string ResolveAssetTypeDisplayName(AssetDependencyDto dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);

        return string.IsNullOrWhiteSpace(dependency.AssetTypeDisplayName)
            ? dependency.AssetType.ToString()
            : dependency.AssetTypeDisplayName.Trim();
    }

    private static bool MatchesReferenceType(
        AssetPreviewGameObjectReferenceDto reference,
        AssetDependencyDto dependency)
    {
        if (string.IsNullOrWhiteSpace(reference.TypeName))
        {
            return false;
        }

        return string.Equals(reference.TypeName, dependency.AssetTypeDisplayName, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(reference.TypeName, dependency.AssetType.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
