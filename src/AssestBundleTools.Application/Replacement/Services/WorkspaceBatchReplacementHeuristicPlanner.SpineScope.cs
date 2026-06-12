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

        var exactSpineGroupKey = NormalizeName(source.SpineGroupName);
        var exactDirectoryMatches = scoredTargets
            .Where(target => TargetHasSpineGroupDirectory(target.Target, exactSpineGroupKey, stripOrdinal: false))
            .ToArray();
        if (exactDirectoryMatches.Length > 0)
        {
            return PreferSourceExtensionMatches(exactDirectoryMatches, source);
        }

        var spineGroupKeyWithoutOrdinal = StripOrdinalSuffix(exactSpineGroupKey);
        var ordinalDirectoryMatches = scoredTargets
            .Where(target => TargetHasSpineGroupDirectory(target.Target, spineGroupKeyWithoutOrdinal, stripOrdinal: true))
            .ToArray();
        if (ordinalDirectoryMatches.Length > 0)
        {
            return PreferSourceExtensionMatches(ordinalDirectoryMatches, source);
        }

        var groupMatches = scoredTargets
            .Where(target => target.Target.MatchKeysWithoutOrdinal.Contains(spineGroupKeyWithoutOrdinal))
            .ToArray();
        return groupMatches.Length > 0
            ? groupMatches
            : scoredTargets;
    }

    private static IReadOnlyList<ScoredTarget> PreferSourceExtensionMatches(
        IReadOnlyList<ScoredTarget> targets,
        SourceFileAnalysis source)
    {
        if (string.IsNullOrWhiteSpace(source.Extension))
        {
            return targets;
        }

        var extensionMatches = targets
            .Where(target => TargetExtensionMatchesSource(target.Target, source.Extension))
            .ToArray();
        return extensionMatches.Length > 0
            ? extensionMatches
            : targets;
    }

    private static bool TargetExtensionMatchesSource(AssetIndexEntry target, string sourceExtension)
    {
        var targetExtension = Path.GetExtension(target.AssetPath);
        return !string.IsNullOrWhiteSpace(targetExtension) &&
               string.Equals(targetExtension, sourceExtension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TargetHasSpineGroupDirectory(
        AssetIndexEntry target,
        string spineGroupKey,
        bool stripOrdinal)
    {
        var segments = target.AssetPath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length <= 1)
        {
            return false;
        }

        return segments
            .Take(segments.Length - 1)
            .Select(segment => stripOrdinal
                ? StripOrdinalSuffix(NormalizeName(segment))
                : NormalizeName(segment))
            .Contains(spineGroupKey, StringComparer.OrdinalIgnoreCase);
    }
}
