using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Queries;

internal static partial class SpineAssetGroupDetector
{
    private static void AddDependencyClassifiedTextParts(
        BundleId targetBundleId,
        IReadOnlyList<AssetCandidate> candidates,
        ICollection<ClassifiedPart> textParts,
        ISet<string> classifiedTextPartKeys,
        ISet<string> anchorGroupKeys,
        IReadOnlyDictionary<string, AssetDetailsDto>? assetDetailsByAssetKey)
    {
        if (assetDetailsByAssetKey is not { Count: > 0 })
        {
            return;
        }

        var textCandidatesByAssetKey = candidates
            .Where(static candidate => candidate.Item.AssetType == AssetType.TextAsset)
            .GroupBy(static candidate => CreateAssetKey(candidate.Bundle.BundleId, candidate.Item.AssetId), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        if (textCandidatesByAssetKey.Count == 0)
        {
            return;
        }

        var details = assetDetailsByAssetKey.Values.ToArray();
        foreach (var skeletonHost in details
                     .Where(static detail => IsSkeletonDataHost(detail))
                     .OrderBy(static detail => PartOwnerSortKey(detail), StringComparer.OrdinalIgnoreCase))
        {
            AddDependencyTextParts(
                targetBundleId,
                skeletonHost,
                SpineAssetGroupPartRole.Skeleton,
                textCandidatesByAssetKey,
                textParts,
                classifiedTextPartKeys,
                anchorGroupKeys);
        }

        foreach (var atlasHost in details
                     .Where(static detail => IsAtlasAssetHost(detail))
                     .OrderBy(static detail => PartOwnerSortKey(detail), StringComparer.OrdinalIgnoreCase))
        {
            AddDependencyTextParts(
                targetBundleId,
                atlasHost,
                SpineAssetGroupPartRole.Atlas,
                textCandidatesByAssetKey,
                textParts,
                classifiedTextPartKeys,
                anchorGroupKeys);
        }
    }

    private static void AddDependencyTextParts(
        BundleId targetBundleId,
        AssetDetailsDto dependencyOwner,
        SpineAssetGroupPartRole role,
        IReadOnlyDictionary<string, AssetCandidate> textCandidatesByAssetKey,
        ICollection<ClassifiedPart> textParts,
        ISet<string> classifiedTextPartKeys,
        ISet<string> anchorGroupKeys)
    {
        foreach (var dependency in dependencyOwner.Dependencies)
        {
            if (dependency.AssetType != AssetType.TextAsset)
            {
                continue;
            }

            var dependencyKey = CreateAssetKey(dependency.BundleId, dependency.AssetId);
            if (classifiedTextPartKeys.Contains(dependencyKey) ||
                !textCandidatesByAssetKey.TryGetValue(dependencyKey, out var candidate) ||
                ShouldSkipDependencyRole(candidate.Item, dependency, role))
            {
                continue;
            }

            var groupKey = ResolveDependencyGroupKey(candidate.Item);
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                continue;
            }

            var classifiedPart = new ClassifiedPart(
                groupKey,
                role,
                CreatePart(candidate, role));
            textParts.Add(classifiedPart);
            classifiedTextPartKeys.Add(dependencyKey);
            if (candidate.Bundle.BundleId.Equals(targetBundleId))
            {
                anchorGroupKeys.Add(classifiedPart.GroupKey);
            }
        }
    }

    private static string ResolveDependencyGroupKey(AssetListItemDto item) =>
        EnumerateCandidatePaths(item)
            .Select(NormalizeGroupKey)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static bool ShouldSkipDependencyRole(
        AssetListItemDto item,
        AssetDependencyDto dependency,
        SpineAssetGroupPartRole role)
    {
        if (role == SpineAssetGroupPartRole.Skeleton)
        {
            return ContainsNormalizedIdentity("atlas", item.Name, item.AssetPath, dependency.Name);
        }

        return role == SpineAssetGroupPartRole.Atlas &&
               ContainsNormalizedIdentity("skeleton", item.Name, item.AssetPath, dependency.Name);
    }

    private static bool IsSkeletonDataHost(AssetDetailsDto detail) =>
        IsMonoBehaviour(detail) &&
        ContainsNormalizedIdentity("skeletondata", detail.Name, detail.AssetPath, detail.ContainerPath);

    private static bool IsAtlasAssetHost(AssetDetailsDto detail) =>
        IsMonoBehaviour(detail) &&
        ContainsNormalizedIdentity("atlas", detail.Name, detail.AssetPath, detail.ContainerPath) &&
        !IsSkeletonDataHost(detail);

    private static bool IsMonoBehaviour(AssetDetailsDto detail) =>
        string.Equals(detail.AssetTypeDisplayName, "MonoBehaviour", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsNormalizedIdentity(string token, params string?[] values)
    {
        foreach (var value in values)
        {
            if (NormalizeIdentity(value).Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeIdentity(string? value) =>
        NormalizePath(value)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

    private static string PartOwnerSortKey(AssetDetailsDto detail) =>
        string.Concat(detail.AssetPath, "\n", detail.Name, "\n", detail.AssetId.Value);
}
