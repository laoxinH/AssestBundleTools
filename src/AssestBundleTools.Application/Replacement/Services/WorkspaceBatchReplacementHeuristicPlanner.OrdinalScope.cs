namespace AssestBundleTools.Application.Replacement.Services;

internal static partial class WorkspaceBatchReplacementHeuristicPlanner
{
    private static IEnumerable<ScoredTarget> ApplyOrdinalPreferredScope(
        this IEnumerable<ScoredTarget> targets,
        SourceFileAnalysis source)
    {
        var scoredTargets = targets.ToArray();
        if (scoredTargets.Length <= 1)
        {
            return scoredTargets;
        }

        var sourceNameKey = NormalizeName(source.NameWithoutExtension);
        if (!HasOrdinalSuffix(sourceNameKey))
        {
            return scoredTargets;
        }

        var exactOrdinalMatches = scoredTargets
            .Where(target => target.Target.MatchKeys.Contains(sourceNameKey))
            .ToArray();
        return exactOrdinalMatches.Length > 0
            ? exactOrdinalMatches
            : scoredTargets;
    }

    private static bool HasOrdinalSuffix(string value) =>
        OrdinalSuffixRegex().IsMatch(value);
}
