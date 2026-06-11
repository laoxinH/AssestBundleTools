using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Replacement.Services;

internal static partial class WorkspaceBatchReplacementHeuristicPlanner
{
    private sealed record WorkspaceHeuristicIndex(
        IReadOnlyList<BundleIndexEntry> Bundles,
        IReadOnlyList<AssetIndexEntry> Assets);

    private sealed record BundleIndexEntry(BundleId BundleId, string DisplayName, IReadOnlySet<string> MatchKeys);

    private sealed record AssetIndexEntry(
        BundleIndexEntry Bundle,
        AssetId AssetId,
        string Name,
        string? DisplayName,
        string AssetPath,
        AssetType AssetType,
        string? AssetTypeDisplayName,
        long PathId,
        AssetMatchKeys MatchKeys)
    {
        public IReadOnlySet<string> MatchKeysWithoutOrdinal => MatchKeys.WithoutOrdinal;
    }

    private sealed record AssetMatchKeys(IReadOnlySet<string> Exact, IReadOnlySet<string> WithoutOrdinal)
    {
        public bool Contains(string key) => Exact.Contains(key);
    }

    private sealed record SourceFileAnalysis(
        string[] Segments,
        string Extension,
        string NameWithoutExtension,
        string? BundleSegment,
        string? TypeSegment,
        string? SpineGroupName,
        IReadOnlyList<AssetType> PossibleAssetTypes,
        bool IsRejected,
        IReadOnlyList<string> Diagnostics)
    {
        public string OutputRole => ResolveOutputRole(Extension, Segments);
    }

    private sealed record ScoredTarget(AssetIndexEntry Target, int Score, IReadOnlyList<string> Diagnostics);
}
