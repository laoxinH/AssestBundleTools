namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationClipSpaceProjector
{
    public static IReadOnlyList<SpineAnimationClipSpaceBatch> Project(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        float viewportWidth,
        float viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(batches);

        var transform = SpineAnimationClipSpaceTransform.Resolve(batches, viewportWidth, viewportHeight);
        if (!transform.IsValid)
        {
            return Array.Empty<SpineAnimationClipSpaceBatch>();
        }

        var projected = new List<SpineAnimationClipSpaceBatch>(batches.Count);
        foreach (var batch in batches)
        {
            if (batch.Vertices.Count == 0 || batch.Indices.Count == 0)
            {
                continue;
            }

            projected.Add(new SpineAnimationClipSpaceBatch(
                batch.PageName,
                batch.Vertices.Select(transform.Project).ToArray(),
                batch.Indices));
        }

        return projected;
    }
}

internal readonly record struct SpineAnimationClipSpaceTransform(
    double CenterX,
    double CenterY,
    double Scale,
    float ViewportWidth,
    float ViewportHeight)
{
    private const double MinExtent = 0.001d;

    public bool IsValid => Scale > 0d && ViewportWidth > 1f && ViewportHeight > 1f;

    public static SpineAnimationClipSpaceTransform Resolve(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        float viewportWidth,
        float viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(batches);

        if (batches.Count == 0 || viewportWidth <= 1f || viewportHeight <= 1f)
        {
            return default;
        }

        var bounds = ResolveBounds(batches);
        var scale = ResolveScale(bounds, viewportWidth, viewportHeight);
        return new SpineAnimationClipSpaceTransform(
            bounds.CenterX,
            bounds.CenterY,
            scale,
            viewportWidth,
            viewportHeight);
    }

    public SpineAnimationClipSpaceVertex Project(SpineAnimationBatchVertex vertex)
    {
        if (!IsValid)
        {
            return default;
        }

        var pixelX = ((vertex.X - CenterX) * Scale) + (ViewportWidth / 2d);
        var pixelY = (-(vertex.Y - CenterY) * Scale) + (ViewportHeight / 2d);
        return new SpineAnimationClipSpaceVertex(
            (float)((pixelX / ViewportWidth * 2d) - 1d),
            (float)(1d - (pixelY / ViewportHeight * 2d)),
            vertex.U,
            vertex.V,
            vertex.R,
            vertex.G,
            vertex.B,
            vertex.A);
    }

    private static SpineAnimationVertexBounds ResolveBounds(IReadOnlyList<SpineAnimationVertexBatch> batches)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var vertex in batches.SelectMany(static batch => batch.Vertices))
        {
            minX = Math.Min(minX, vertex.X);
            minY = Math.Min(minY, vertex.Y);
            maxX = Math.Max(maxX, vertex.X);
            maxY = Math.Max(maxY, vertex.Y);
        }

        if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
            !double.IsFinite(maxX) || !double.IsFinite(maxY))
        {
            return new SpineAnimationVertexBounds(0d, 0d, 1d, 1d);
        }

        return new SpineAnimationVertexBounds(
            (minX + maxX) / 2d,
            (minY + maxY) / 2d,
            Math.Max(maxX - minX, 1d),
            Math.Max(maxY - minY, 1d));
    }

    private static double ResolveScale(
        SpineAnimationVertexBounds bounds,
        float viewportWidth,
        float viewportHeight)
    {
        var extent = Math.Max(Math.Max(bounds.Width, bounds.Height), MinExtent);
        return (Math.Min(viewportWidth, viewportHeight) * 0.72d) / extent;
    }

    private readonly record struct SpineAnimationVertexBounds(
        double CenterX,
        double CenterY,
        double Width,
        double Height);
}

internal sealed record SpineAnimationClipSpaceBatch(
    string PageName,
    IReadOnlyList<SpineAnimationClipSpaceVertex> Vertices,
    IReadOnlyList<int> Indices);

internal readonly record struct SpineAnimationClipSpaceVertex(
    float X,
    float Y,
    float U,
    float V,
    float R,
    float G,
    float B,
    float A);
