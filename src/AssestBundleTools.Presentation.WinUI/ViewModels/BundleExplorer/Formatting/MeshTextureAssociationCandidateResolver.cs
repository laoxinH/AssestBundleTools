using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal static class MeshTextureAssociationCandidateResolver
{
    public static IReadOnlyList<AssetDependencyDto> CollectDisplayCandidates(
        AssetDetailsDto? details,
        int maxCount)
    {
        var primaryAssociations = CollectTextureAssociationCandidates(details, maxCount);
        if (primaryAssociations.Count > 0)
        {
            return primaryAssociations;
        }

        return CollectCandidates(
            details,
            static assetType => assetType is AssetType.Material or AssetType.Texture2D or AssetType.Sprite,
            maxCount,
            includeTextureAssociations: false);
    }

    public static IReadOnlyList<AssetDependencyDto> CollectTextureOverlayCandidates(
        AssetDetailsDto? details,
        int maxCount)
    {
        var primaryAssociations = CollectTextureAssociationCandidates(details, maxCount);
        if (primaryAssociations.Count > 0)
        {
            return primaryAssociations;
        }

        var candidates = CollectCandidates(
            details,
            static assetType => assetType is AssetType.Texture2D or AssetType.Sprite,
            maxCount: int.MaxValue,
            includeTextureAssociations: false);
        if (candidates.Count == 0)
        {
            return Array.Empty<AssetDependencyDto>();
        }

        return candidates
            .OrderBy(static dependency => dependency.AssetType == AssetType.Texture2D ? 0 : 1)
            .Take(Math.Max(0, maxCount))
            .ToArray();
    }

    private static IReadOnlyList<AssetDependencyDto> CollectTextureAssociationCandidates(
        AssetDetailsDto? details,
        int maxCount)
    {
        if (details?.TextureAssociations is not { Count: > 0 } textureAssociations || maxCount <= 0)
        {
            return Array.Empty<AssetDependencyDto>();
        }

        return textureAssociations
            .Where(static dependency => dependency.AssetType is AssetType.Texture2D or AssetType.Sprite)
            .DistinctBy(static dependency => dependency.AssetId.Value)
            .OrderBy(static dependency => dependency.AssetType == AssetType.Texture2D ? 0 : 1)
            .ThenBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToArray();
    }

    private static IReadOnlyList<AssetDependencyDto> CollectCandidates(
        AssetDetailsDto? details,
        Func<AssetType, bool> isAcceptedType,
        int maxCount,
        bool includeTextureAssociations = true)
    {
        if (details is null || maxCount <= 0)
        {
            return Array.Empty<AssetDependencyDto>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var associations = new List<AssetDependencyDto>();
        foreach (var dependency in EnumerateAssociationCandidates(details, includeTextureAssociations))
        {
            if (!isAcceptedType(dependency.AssetType) ||
                !seen.Add(dependency.AssetId.Value))
            {
                continue;
            }

            associations.Add(dependency);
            if (associations.Count >= maxCount)
            {
                break;
            }
        }

        return associations;
    }

    private static IEnumerable<AssetDependencyDto> EnumerateAssociationCandidates(
        AssetDetailsDto details,
        bool includeTextureAssociations)
    {
        if (includeTextureAssociations)
        {
            foreach (var dependency in details.TextureAssociations ?? Array.Empty<AssetDependencyDto>())
            {
                yield return dependency;
            }
        }

        foreach (var dependency in details.Dependencies)
        {
            yield return dependency;
        }

        foreach (var dependency in details.ReferencedBy ?? Array.Empty<AssetDependencyDto>())
        {
            yield return dependency;
        }

        foreach (var dependency in details.DependencyClosure ?? Array.Empty<AssetDependencyDto>())
        {
            yield return dependency;
        }
    }
}
