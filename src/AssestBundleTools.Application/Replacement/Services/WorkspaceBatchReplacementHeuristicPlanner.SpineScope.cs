namespace AssestBundleTools.Application.Replacement.Services;

internal static partial class WorkspaceBatchReplacementHeuristicPlanner
{
    private static IEnumerable<ScoredTarget> ApplySpineGroupPreferredScope(
        this IEnumerable<ScoredTarget> targets,
        SourceFileAnalysis source)
    {
        var scoredTargets = targets.ToArray();
        if (!source.IsSpineContext ||
            string.IsNullOrWhiteSpace(source.SpineGroupName))
        {
            return scoredTargets;
        }

        var spineGroupKey = StripOrdinalSuffix(NormalizeName(source.SpineGroupName));
        var directoryMatches = scoredTargets
            .Where(target => TargetHasSpineGroupDirectory(target.Target, spineGroupKey))
            .ToArray();
        if (directoryMatches.Length > 0)
        {
            return directoryMatches;
        }

        var groupMatches = scoredTargets
            .Where(target => target.Target.MatchKeysWithoutOrdinal.Contains(spineGroupKey))
            .ToArray();
        return groupMatches.Length > 0
            ? groupMatches
            : scoredTargets;
    }

    private static bool TargetHasSpineGroupDirectory(AssetIndexEntry target, string spineGroupKey)
    {
        var segments = target.AssetPath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length <= 1)
        {
            return false;
        }

        return segments
            .Take(segments.Length - 1)
            .Select(static segment => StripOrdinalSuffix(NormalizeName(segment)))
            .Contains(spineGroupKey, StringComparer.OrdinalIgnoreCase);
    }
}
