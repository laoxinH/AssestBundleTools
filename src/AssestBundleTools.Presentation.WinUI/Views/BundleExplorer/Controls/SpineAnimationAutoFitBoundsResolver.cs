namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationAutoFitBoundsResolver
{
    private const float VisibleAlphaThreshold = 0.0001f;

    public static SpineAnimationAutoFitBounds Resolve(IReadOnlyList<SpineAnimationVertexBatch> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);

        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var hasVisibleVertices = false;
        foreach (var batch in batches)
        {
            if (batch.AutoFitBounds.IsValid)
            {
                hasVisibleVertices = true;
                minX = Math.Min(minX, batch.AutoFitBounds.MinX);
                minY = Math.Min(minY, batch.AutoFitBounds.MinY);
                maxX = Math.Max(maxX, batch.AutoFitBounds.MaxX);
                maxY = Math.Max(maxY, batch.AutoFitBounds.MaxY);

                continue;
            }

            foreach (var vertex in batch.Vertices)
            {
                if (!IsVisibleForAutoFit(vertex))
                {
                    continue;
                }

                hasVisibleVertices = true;
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
            }
        }

        if (!hasVisibleVertices)
        {
            foreach (var vertex in batches.SelectMany(static batch => batch.Vertices))
            {
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
            }
        }

        if (!double.IsFinite(minX) ||
            !double.IsFinite(minY) ||
            !double.IsFinite(maxX) ||
            !double.IsFinite(maxY))
        {
            return default;
        }

        return new SpineAnimationAutoFitBounds(true, minX, minY, maxX, maxY);
    }

    public static SpineAnimationAutoFitBounds Union(
        SpineAnimationAutoFitBounds current,
        SpineAnimationAutoFitBounds next)
    {
        if (!current.IsValid)
        {
            return next;
        }

        if (!next.IsValid)
        {
            return current;
        }

        return new SpineAnimationAutoFitBounds(
            true,
            Math.Min(current.MinX, next.MinX),
            Math.Min(current.MinY, next.MinY),
            Math.Max(current.MaxX, next.MaxX),
            Math.Max(current.MaxY, next.MaxY));
    }

    private static bool IsVisibleForAutoFit(SpineAnimationBatchVertex vertex) =>
        vertex.A > VisibleAlphaThreshold;
}
