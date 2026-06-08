using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Queries;

internal static partial class SpineAssetGroupDetector
{
    internal static string CreateAssetKey(BundleId bundleId, AssetId assetId) =>
        string.Concat(bundleId.Value, "\n", assetId.Value);

    private static IReadOnlyList<SpineAssetGroupDto> CreateLinkedGroups(
        BundleId targetBundleId,
        IReadOnlyList<AssetCandidate> candidates,
        IReadOnlyList<ClassifiedPart> textParts,
        IReadOnlyList<TextureCandidate> textureCandidates,
        IReadOnlyDictionary<string, AssetDetailsDto>? assetDetailsByAssetKey,
        bool includeAssetDetailState)
    {
        if (assetDetailsByAssetKey is not { Count: > 0 })
        {
            return Array.Empty<SpineAssetGroupDto>();
        }

        var candidateKeys = candidates
            .Select(static candidate => CreateAssetKey(candidate.Bundle.BundleId, candidate.Item.AssetId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var detailsByAssetKey = assetDetailsByAssetKey
            .Where(pair => candidateKeys.Contains(pair.Key))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (detailsByAssetKey.Count == 0)
        {
            return Array.Empty<SpineAssetGroupDto>();
        }

        var textPartsByAssetKey = textParts
            .GroupBy(static part => CreateAssetKey(part.Part), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        var texturePartsByAssetKey = textureCandidates
            .GroupBy(static texture => CreateAssetKey(texture.Part), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().Part, StringComparer.OrdinalIgnoreCase);
        var detailsByDependencyKey = detailsByAssetKey.Values
            .SelectMany(detail => detail.Dependencies.Select(dependency => new
            {
                DependencyKey = CreateAssetKey(dependency.BundleId, dependency.AssetId),
                Source = detail
            }))
            .GroupBy(static item => item.DependencyKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static item => item.Source).ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var links = new List<LinkedSpineAssetGroup>();
        foreach (var skeletonPart in textParts
                     .Where(static part => part.Role == SpineAssetGroupPartRole.Skeleton)
                     .Where(part => part.Part.BundleId.Equals(targetBundleId))
                     .OrderBy(static part => PartSortKey(part.Part), StringComparer.OrdinalIgnoreCase))
        {
            var skeletonKey = CreateAssetKey(skeletonPart.Part);
            if (!detailsByDependencyKey.TryGetValue(skeletonKey, out var skeletonHosts))
            {
                continue;
            }

            foreach (var skeletonHost in skeletonHosts.OrderBy(static detail => detail.Name, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var atlasLink in ResolveAtlasLinks(skeletonHost, textPartsByAssetKey, detailsByAssetKey))
                {
                    links.Add(new LinkedSpineAssetGroup(
                        skeletonPart.GroupKey,
                        skeletonPart.Part,
                        atlasLink.Atlas.Part,
                        ResolveTextureParts(atlasLink.AtlasOwner, texturePartsByAssetKey, detailsByAssetKey)));
                }
            }
        }

        return links
            .GroupBy(static link => CreateLinkedGroupUniquenessKey(link), StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .GroupBy(static link => link.GroupKey, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => CreateLinkedGroupDtos(targetBundleId, group.ToArray()))
            .ToArray();
    }

    private static IReadOnlySet<string> CreateLinkedGroupKeySet(
        IReadOnlyList<SpineAssetGroupDto> linkedGroups,
        IReadOnlyList<ClassifiedPart> textParts)
    {
        var linkedGroupKeys = linkedGroups
            .Select(static group => group.GroupKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (linkedGroups.Count == 0)
        {
            return linkedGroupKeys;
        }

        var linkedPartKeys = linkedGroups
            .SelectMany(static group => group.SkeletonAssets.Concat(group.AtlasAssets))
            .Select(static part => CreateAssetKey(part))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var textPart in textParts)
        {
            if (linkedPartKeys.Contains(CreateAssetKey(textPart.Part)))
            {
                linkedGroupKeys.Add(textPart.GroupKey);
            }
        }

        return linkedGroupKeys;
    }

    private static IEnumerable<LinkedAtlasAsset> ResolveAtlasLinks(
        AssetDetailsDto skeletonHost,
        IReadOnlyDictionary<string, ClassifiedPart> textPartsByAssetKey,
        IReadOnlyDictionary<string, AssetDetailsDto> detailsByAssetKey)
    {
        foreach (var directDependency in skeletonHost.Dependencies)
        {
            var dependencyKey = CreateAssetKey(directDependency.BundleId, directDependency.AssetId);
            if (TryResolveAtlasPart(dependencyKey, textPartsByAssetKey, out var directAtlas))
            {
                yield return new LinkedAtlasAsset(directAtlas, skeletonHost);
                continue;
            }

            if (!detailsByAssetKey.TryGetValue(dependencyKey, out var dependencyDetails))
            {
                continue;
            }

            foreach (var nestedDependency in dependencyDetails.Dependencies)
            {
                var nestedDependencyKey = CreateAssetKey(nestedDependency.BundleId, nestedDependency.AssetId);
                if (TryResolveAtlasPart(nestedDependencyKey, textPartsByAssetKey, out var nestedAtlas))
                {
                    yield return new LinkedAtlasAsset(nestedAtlas, dependencyDetails);
                }
            }
        }
    }

    private static bool TryResolveAtlasPart(
        string dependencyKey,
        IReadOnlyDictionary<string, ClassifiedPart> textPartsByAssetKey,
        out ClassifiedPart atlas)
    {
        if (textPartsByAssetKey.TryGetValue(dependencyKey, out var textPart) &&
            textPart.Role == SpineAssetGroupPartRole.Atlas)
        {
            atlas = textPart;
            return true;
        }

        atlas = default;
        return false;
    }

    private static IReadOnlyList<SpineAssetGroupPartDto> ResolveTextureParts(
        AssetDetailsDto atlasOwner,
        IReadOnlyDictionary<string, SpineAssetGroupPartDto> texturePartsByAssetKey,
        IReadOnlyDictionary<string, AssetDetailsDto> detailsByAssetKey)
    {
        var texturePartsByKey = new Dictionary<string, SpineAssetGroupPartDto>(StringComparer.OrdinalIgnoreCase);
        AddDirectTextureDependencies(atlasOwner, texturePartsByAssetKey, texturePartsByKey);

        foreach (var dependency in atlasOwner.Dependencies)
        {
            var dependencyKey = CreateAssetKey(dependency.BundleId, dependency.AssetId);
            if (detailsByAssetKey.TryGetValue(dependencyKey, out var dependencyDetails))
            {
                AddDirectTextureDependencies(dependencyDetails, texturePartsByAssetKey, texturePartsByKey);
            }
        }

        return texturePartsByKey.Values
            .OrderBy(PartSortKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddDirectTextureDependencies(
        AssetDetailsDto source,
        IReadOnlyDictionary<string, SpineAssetGroupPartDto> texturePartsByAssetKey,
        IDictionary<string, SpineAssetGroupPartDto> texturePartsByKey)
    {
        foreach (var dependency in source.Dependencies)
        {
            if (dependency.AssetType != AssetType.Texture2D)
            {
                continue;
            }

            var dependencyKey = CreateAssetKey(dependency.BundleId, dependency.AssetId);
            if (texturePartsByAssetKey.TryGetValue(dependencyKey, out var texturePart))
            {
                texturePartsByKey.TryAdd(dependencyKey, texturePart);
            }
        }
    }

    private static IEnumerable<SpineAssetGroupDto> CreateLinkedGroupDtos(
        BundleId targetBundleId,
        IReadOnlyList<LinkedSpineAssetGroup> links)
    {
        var orderedLinks = links
            .OrderBy(static link => PartSortKey(link.Skeleton), StringComparer.OrdinalIgnoreCase)
            .ThenBy(static link => PartSortKey(link.Atlas), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var duplicateCount = orderedLinks.Length;
        for (var index = 0; index < orderedLinks.Length; index++)
        {
            var link = orderedLinks[index];
            var textures = link.Textures
                .DistinctBy(static part => CreateAssetKey(part))
                .OrderBy(PartSortKey, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var displayName = duplicateCount == 1
                ? ResolveDisplayName(link.GroupKey)
                : string.Concat(ResolveDisplayName(link.GroupKey), " [", index + 1, "]");

            yield return new SpineAssetGroupDto(
                GroupId: index == 0
                    ? CreateGroupId(targetBundleId, link.GroupKey)
                    : CreateLinkedGroupId(targetBundleId, link),
                BundleId: targetBundleId,
                DisplayName: displayName,
                GroupKey: link.GroupKey,
                SkeletonAssets: [link.Skeleton],
                AtlasAssets: [link.Atlas],
                TexturePageAssets: textures,
                Diagnostics: CreateDiagnostics(link.GroupKey, [link.Skeleton], [link.Atlas], textures));
        }
    }

    private static string CreateLinkedGroupId(
        BundleId targetBundleId,
        LinkedSpineAssetGroup link) =>
        string.Concat(
            CreateGroupId(targetBundleId, link.GroupKey),
            ":linked:",
            link.Skeleton.AssetId.Value,
            ":",
            link.Atlas.AssetId.Value);

    private static string CreateLinkedGroupUniquenessKey(LinkedSpineAssetGroup link) =>
        string.Concat(
            link.GroupKey,
            "\n",
            CreateAssetKey(link.Skeleton),
            "\n",
            CreateAssetKey(link.Atlas));

    private static string CreateAssetKey(SpineAssetGroupPartDto part) =>
        CreateAssetKey(part.BundleId, part.AssetId);

    private sealed record LinkedSpineAssetGroup(
        string GroupKey,
        SpineAssetGroupPartDto Skeleton,
        SpineAssetGroupPartDto Atlas,
        IReadOnlyList<SpineAssetGroupPartDto> Textures);

    private sealed record LinkedAtlasAsset(
        ClassifiedPart Atlas,
        AssetDetailsDto AtlasOwner);
}
