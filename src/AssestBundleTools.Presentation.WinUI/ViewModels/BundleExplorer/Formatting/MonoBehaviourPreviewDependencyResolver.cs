using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal static class MonoBehaviourPreviewDependencyResolver
{
    public static AssetDependencyDto? ResolveReference(
        AssetPreviewMonoBehaviourReferenceDto reference,
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

    public static ResolvedAssetNavigationTarget CreateNavigationTarget(AssetDependencyDto dependency) =>
        GameObjectPreviewDependencyResolver.CreateNavigationTarget(dependency);

    public static string ResolveAssetTypeDisplayName(AssetDependencyDto dependency) =>
        GameObjectPreviewDependencyResolver.ResolveAssetTypeDisplayName(dependency);

    private static bool MatchesReferenceType(
        AssetPreviewMonoBehaviourReferenceDto reference,
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
