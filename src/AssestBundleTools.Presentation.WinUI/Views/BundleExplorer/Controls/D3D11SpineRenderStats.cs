using System.Globalization;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal readonly record struct D3D11SpineBatchStats(
    bool HasVertices,
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    double MinU,
    double MinV,
    double MaxU,
    double MaxV,
    double MinAlpha,
    double MaxAlpha)
{
    public static D3D11SpineBatchStats Empty =>
        new(false, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d);

    public D3D11SpineBatchStats Include(SpineAnimationVertexBatch batch)
    {
        var stats = this;
        foreach (var vertex in batch.Vertices)
        {
            stats = stats.Include(vertex);
        }

        return stats;
    }

    public string Format() =>
        HasVertices
            ? $"bounds={Format(MinX)},{Format(MinY)}..{Format(MaxX)},{Format(MaxY)} " +
              $"uv={Format(MinU)},{Format(MinV)}..{Format(MaxU)},{Format(MaxV)} " +
              $"alpha={Format(MinAlpha)}..{Format(MaxAlpha)}"
            : string.Empty;

    private D3D11SpineBatchStats Include(SpineAnimationBatchVertex vertex)
    {
        if (!HasVertices)
        {
            return new D3D11SpineBatchStats(
                true,
                vertex.X,
                vertex.Y,
                vertex.X,
                vertex.Y,
                vertex.U,
                vertex.V,
                vertex.U,
                vertex.V,
                vertex.A,
                vertex.A);
        }

        return this with
        {
            MinX = Math.Min(MinX, vertex.X),
            MinY = Math.Min(MinY, vertex.Y),
            MaxX = Math.Max(MaxX, vertex.X),
            MaxY = Math.Max(MaxY, vertex.Y),
            MinU = Math.Min(MinU, vertex.U),
            MinV = Math.Min(MinV, vertex.V),
            MaxU = Math.Max(MaxU, vertex.U),
            MaxV = Math.Max(MaxV, vertex.V),
            MinAlpha = Math.Min(MinAlpha, vertex.A),
            MaxAlpha = Math.Max(MaxAlpha, vertex.A)
        };
    }

    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}

internal readonly record struct D3D11SpineTexturePixelStats(
    bool HasPixels,
    int MinAlpha,
    int MaxAlpha,
    long NonZeroAlphaPixels,
    long TotalPixels)
{
    public static D3D11SpineTexturePixelStats Empty =>
        new(false, 255, 0, 0, 0);

    public D3D11SpineTexturePixelStats Include(SpineAnimationDecodedTexturePage page)
    {
        var stats = this;
        for (var index = 3; index < page.BgraPixels.Length; index += 4)
        {
            stats = stats.Include(page.BgraPixels[index]);
        }

        return stats;
    }

    public string Format()
    {
        if (!HasPixels || TotalPixels <= 0)
        {
            return string.Empty;
        }

        var nonZeroPercent = NonZeroAlphaPixels * 100d / TotalPixels;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"textureAlpha={MinAlpha}..{MaxAlpha} nonZero={nonZeroPercent:0.###}%");
    }

    private D3D11SpineTexturePixelStats Include(byte alpha)
    {
        return new D3D11SpineTexturePixelStats(
            true,
            HasPixels ? Math.Min(MinAlpha, alpha) : alpha,
            HasPixels ? Math.Max(MaxAlpha, alpha) : alpha,
            NonZeroAlphaPixels + (alpha > 0 ? 1 : 0),
            TotalPixels + 1);
    }
}
