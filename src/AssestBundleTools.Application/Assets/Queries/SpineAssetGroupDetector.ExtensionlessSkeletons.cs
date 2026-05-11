using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Queries;

internal static partial class SpineAssetGroupDetector
{
    private static void AddExtensionlessSkeletonParts(
        BundleId targetBundleId,
        IReadOnlyList<AssetCandidate> candidates,
        ICollection<ClassifiedPart> textParts,
        ISet<string> classifiedTextPartKeys,
        ISet<string> anchorGroupKeys)
    {
        var atlasGroupKeys = textParts
            .Where(static part => part.Role == SpineAssetGroupPartRole.Atlas)
            .Select(static part => part.GroupKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (atlasGroupKeys.Count == 0)
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Item.AssetType != AssetType.TextAsset ||
                classifiedTextPartKeys.Contains(CreateAssetKey(candidate.Bundle.BundleId, candidate.Item.AssetId)))
            {
                continue;
            }

            if (!TryCreateExtensionlessSkeletonPart(candidate, atlasGroupKeys, out var classifiedPart))
            {
                continue;
            }

            textParts.Add(classifiedPart);
            classifiedTextPartKeys.Add(CreateAssetKey(classifiedPart.Part));
            if (candidate.Bundle.BundleId.Equals(targetBundleId))
            {
                anchorGroupKeys.Add(classifiedPart.GroupKey);
            }
        }
    }

    private static bool TryCreateExtensionlessSkeletonPart(
        AssetCandidate candidate,
        IReadOnlySet<string> atlasGroupKeys,
        out ClassifiedPart classifiedPart)
    {
        foreach (var path in EnumerateCandidatePaths(candidate.Item))
        {
            var groupKey = NormalizeGroupKey(path);
            if (IsExtensionlessPath(groupKey) &&
                atlasGroupKeys.Contains(groupKey))
            {
                classifiedPart = new ClassifiedPart(
                    groupKey,
                    SpineAssetGroupPartRole.Skeleton,
                    CreatePart(candidate, SpineAssetGroupPartRole.Skeleton));
                return true;
            }
        }

        classifiedPart = default;
        return false;
    }

    private static bool IsExtensionlessPath(string value)
    {
        var fileName = GetFileName(value);
        return !string.IsNullOrWhiteSpace(fileName) &&
               !fileName.Contains('.', StringComparison.Ordinal);
    }
}
