namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationClipSpaceProjector
{
    public static IReadOnlyList<SpineAnimationClipSpaceBatch> Project(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        float viewportWidth,
        float viewportHeight,
        SpineAnimationPreviewViewport? viewport = null,
        SpineAnimationAutoFitBounds autoFitBounds = default)
    {
        ArgumentNullException.ThrowIfNull(batches);

        var transform = SpineAnimationClipSpaceTransform.Resolve(
            batches,
            viewportWidth,
            viewportHeight,
            viewport ?? SpineAnimationPreviewViewport.Default,
            autoFitBounds);
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
    float ViewportHeight,
    SpineAnimationPreviewViewport Viewport)
{
    private const double MinExtent = 0.001d;
    private const double ViewportFitRatio = 0.72d;

    public bool IsValid => Scale > 0d && ViewportWidth > 1f && ViewportHeight > 1f;

    public static SpineAnimationClipSpaceTransform Resolve(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        float viewportWidth,
        float viewportHeight,
        SpineAnimationPreviewViewport viewport,
        SpineAnimationAutoFitBounds autoFitBounds = default)
    {
        ArgumentNullException.ThrowIfNull(batches);

        if (batches.Count == 0 || viewportWidth <= 1f || viewportHeight <= 1f)
        {
            return default;
        }

        var bounds = ResolveBounds(autoFitBounds.IsValid
            ? autoFitBounds
            : SpineAnimationAutoFitBoundsResolver.Resolve(batches));
        var scale = ResolveScale(bounds, viewportWidth, viewportHeight);
        return new SpineAnimationClipSpaceTransform(
            bounds.CenterX,
            bounds.CenterY,
            scale,
            viewportWidth,
            viewportHeight,
            viewport.Normalize());
    }

    public SpineAnimationClipSpaceVertex Project(SpineAnimationBatchVertex vertex)
    {
        if (!IsValid)
        {
            return default;
        }

        var pixelX = ((vertex.X - CenterX) * Scale) + (ViewportWidth / 2d);
        var pixelY = (-(vertex.Y - CenterY) * Scale) + (ViewportHeight / 2d);
        pixelX = ApplyViewportAxis(
            pixelX,
            ViewportWidth / 2d,
            Viewport.Zoom,
            Viewport.PanX);
        pixelY = ApplyViewportAxis(
            pixelY,
            ViewportHeight / 2d,
            Viewport.Zoom,
            Viewport.PanY);
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

    private static double ApplyViewportAxis(
        double pixel,
        double center,
        double zoom,
        double pan) =>
        center + ((pixel - center) * zoom) + pan;

    private static SpineAnimationVertexBounds ResolveBounds(SpineAnimationAutoFitBounds bounds)
    {
        if (!bounds.IsValid)
        {
            return new SpineAnimationVertexBounds(0d, 0d, 1d, 1d, 1d, 1d);
        }

        return new SpineAnimationVertexBounds(
            bounds.CenterX,
            bounds.CenterY,
            Math.Max(bounds.Width, 1d),
            Math.Max(bounds.Height, 1d),
            Math.Max(bounds.Width, 1d),
            Math.Max(bounds.Height, 1d));
    }

    private static double ResolveScale(
        SpineAnimationVertexBounds bounds,
        float viewportWidth,
        float viewportHeight)
    {
        var widthScale = viewportWidth / Math.Max(bounds.FitWidth, MinExtent);
        var heightScale = viewportHeight / Math.Max(bounds.FitHeight, MinExtent);
        return Math.Min(widthScale, heightScale) * ViewportFitRatio;
    }

    private readonly record struct SpineAnimationVertexBounds(
        double CenterX,
        double CenterY,
        double Width,
        double Height,
        double FitWidth,
        double FitHeight);
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
