namespace AssestBundleTools.Application.Replacement.Services;

internal static partial class WorkspaceBatchReplacementHeuristicPlanner
{
    private static string? ResolveBundleSegment(SourceRootContext sourceRoot, string[] segments)
    {
        if (segments.Length >= 3)
        {
            return string.Equals(segments[0], SpineSegment, StringComparison.OrdinalIgnoreCase)
                ? sourceRoot.RootName
                : segments[0];
        }

        if (sourceRoot.IsSpineRoot)
        {
            return sourceRoot.ParentName;
        }

        if (sourceRoot.IsSpineGroupRoot)
        {
            return sourceRoot.GrandParentName;
        }

        if (segments.Length >= 2)
        {
            return sourceRoot.RootName;
        }

        return sourceRoot.RootTypeSegment is not null
            ? sourceRoot.ParentName
            : null;
    }

    private static string? ResolveTypeSegment(SourceRootContext sourceRoot, string[] segments)
    {
        if (segments.Length >= 3 &&
            !string.Equals(segments[1], SpineSegment, StringComparison.OrdinalIgnoreCase))
        {
            return segments[1];
        }

        if (segments.Length >= 2 &&
            !string.Equals(segments[0], SpineSegment, StringComparison.OrdinalIgnoreCase))
        {
            return segments[0];
        }

        return segments.Length >= 1
            ? sourceRoot.RootTypeSegment
            : null;
    }

    private static string? ResolveSpineGroupName(SourceRootContext sourceRoot, string[] segments) =>
        segments.Length >= 4 && string.Equals(segments[1], SpineSegment, StringComparison.OrdinalIgnoreCase)
            ? segments[2]
            : segments.Length >= 3 && string.Equals(segments[0], SpineSegment, StringComparison.OrdinalIgnoreCase)
                ? segments[1]
                : sourceRoot.IsSpineRoot && segments.Length >= 2
                    ? segments[0]
                    : sourceRoot.IsSpineGroupRoot && segments.Length >= 1
                        ? sourceRoot.RootName
                        : null;

    private static bool IsSpineContext(SourceRootContext sourceRoot, string[] segments) =>
        sourceRoot.IsSpineRoot ||
        sourceRoot.IsSpineGroupRoot ||
        IsSpinePath(segments);

    private static bool IsSpinePath(string[] segments) =>
        segments.Length >= 2 && string.Equals(segments[0], SpineSegment, StringComparison.OrdinalIgnoreCase) ||
        segments.Length >= 3 && string.Equals(segments[1], SpineSegment, StringComparison.OrdinalIgnoreCase);

    private static string ResolveOutputRole(string extension, string[] segments, bool isSpineContext)
    {
        if (isSpineContext)
        {
            return extension.ToLowerInvariant() switch
            {
                ".atlas" => "Atlas",
                ".skel" or ".json" => "Skeleton",
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga" or ".dds" => "TexturePage",
                _ => "SpinePart"
            };
        }

        return "Asset";
    }
}
