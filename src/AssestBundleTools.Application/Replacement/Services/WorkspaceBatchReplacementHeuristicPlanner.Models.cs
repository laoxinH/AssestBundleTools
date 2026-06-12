using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Replacement.Services;

internal static partial class WorkspaceBatchReplacementHeuristicPlanner
{
    private static string? GetDirectoryNameSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static string? GetParentDirectoryNameSegment(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return GetDirectoryNameSegment(parent);
    }

    private static string? GetAncestorDirectoryNameSegment(string? path, int parentLevels)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            parentLevels < 1)
        {
            return null;
        }

        var current = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var index = 0; index < parentLevels; index++)
        {
            current = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(current))
            {
                return null;
            }
        }

        return GetDirectoryNameSegment(current);
    }

    private static SourceRootContext CreateSourceRootContext(string sourceRootDirectory)
    {
        var rootName = GetDirectoryNameSegment(sourceRootDirectory);
        var parentName = GetParentDirectoryNameSegment(sourceRootDirectory);
        return new SourceRootContext(
            RootName: rootName,
            ParentName: parentName,
            GrandParentName: GetAncestorDirectoryNameSegment(sourceRootDirectory, 2),
            IsSpineRoot: string.Equals(rootName, SpineSegment, StringComparison.OrdinalIgnoreCase),
            IsSpineGroupRoot: string.Equals(parentName, SpineSegment, StringComparison.OrdinalIgnoreCase),
            RootTypeSegment: ParseAssetType(rootName) is null ? null : rootName);
    }

    private sealed record SourceRootContext(
        string? RootName,
        string? ParentName,
        string? GrandParentName,
        bool IsSpineRoot,
        bool IsSpineGroupRoot,
        string? RootTypeSegment);

    private sealed record WorkspaceHeuristicIndex(
        IReadOnlyList<BundleIndexEntry> Bundles,
        IReadOnlyList<AssetIndexEntry> Assets);

    private sealed record SpineGroupHeuristicIndex(IReadOnlyList<SpineGroupTargetEntry> Entries)
    {
        public static SpineGroupHeuristicIndex Empty { get; } = new(Array.Empty<SpineGroupTargetEntry>());
    }

    private sealed record SpineGroupTargetEntry(
        AssetIndexEntry Target,
        IReadOnlySet<string> GroupMatchKeys,
        string OutputRole);

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
        IReadOnlyList<string> Diagnostics,
        bool IsSpineContext)
    {
        public string OutputRole => ResolveOutputRole(Extension, Segments, IsSpineContext);
    }

    private sealed record ScoredTarget(AssetIndexEntry Target, int Score, IReadOnlyList<string> Diagnostics);
}
