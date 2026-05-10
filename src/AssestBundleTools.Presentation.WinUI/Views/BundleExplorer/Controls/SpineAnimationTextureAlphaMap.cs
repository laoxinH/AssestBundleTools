using System.Collections.Concurrent;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class SpineAnimationTextureAlphaMap
{
    private const double TriangleCoordinateScale = 1024d;
    private const double TriangleDegenerateThreshold = 0.0001d;
    private const double TriangleBarycentricTolerance = 0.0001d;

    private readonly byte[] _alphaPixels;
    private readonly ConcurrentDictionary<SpineAnimationTextureSourceRect, SpineAnimationTextureSourceAlphaBounds> _sourceBoundsCache = new();
    private readonly ConcurrentDictionary<SpineAnimationTextureTriangleKey, SpineAnimationTextureSourceAlphaBounds> _triangleBoundsCache = new();

    private SpineAnimationTextureAlphaMap(
        int width,
        int height,
        byte[] alphaPixels)
    {
        Width = width;
        Height = height;
        _alphaPixels = alphaPixels;
    }

    public int Width { get; }

    public int Height { get; }

    public static SpineAnimationTextureAlphaMap Create(
        int width,
        int height,
        byte[] bgraPixels)
    {
        ArgumentNullException.ThrowIfNull(bgraPixels);

        var expectedLength = checked(width * height * 4);
        if (width <= 0 || height <= 0 || bgraPixels.Length < expectedLength)
        {
            return new SpineAnimationTextureAlphaMap(1, 1, [byte.MaxValue]);
        }

        var alphaPixels = new byte[checked(width * height)];
        for (var pixelIndex = 0; pixelIndex < alphaPixels.Length; pixelIndex++)
        {
            alphaPixels[pixelIndex] = bgraPixels[(pixelIndex * 4) + 3];
        }

        return new SpineAnimationTextureAlphaMap(width, height, alphaPixels);
    }

    public bool TryResolveVisibleBounds(
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        out SpineAnimationTextureSourceAlphaBounds bounds)
    {
        var rect = SpineAnimationTextureSourceRect.Normalize(
            sourceX,
            sourceY,
            sourceWidth,
            sourceHeight,
            Width,
            Height);
        if (!rect.IsValid)
        {
            bounds = default;
            return false;
        }

        bounds = _sourceBoundsCache.GetOrAdd(rect, ResolveVisibleBounds);
        return bounds.HasVisiblePixels;
    }

    public bool TryResolveVisibleTriangleBounds(
        double firstU,
        double firstV,
        double secondU,
        double secondV,
        double thirdU,
        double thirdV,
        out SpineAnimationTextureSourceAlphaBounds bounds)
    {
        if (!double.IsFinite(firstU) ||
            !double.IsFinite(firstV) ||
            !double.IsFinite(secondU) ||
            !double.IsFinite(secondV) ||
            !double.IsFinite(thirdU) ||
            !double.IsFinite(thirdV))
        {
            bounds = default;
            return false;
        }

        var key = SpineAnimationTextureTriangleKey.Create(
            firstU,
            firstV,
            secondU,
            secondV,
            thirdU,
            thirdV,
            Width,
            Height,
            TriangleCoordinateScale);
        bounds = _triangleBoundsCache.GetOrAdd(key, ResolveVisibleTriangleBounds);
        return bounds.HasVisiblePixels;
    }

    private SpineAnimationTextureSourceAlphaBounds ResolveVisibleBounds(SpineAnimationTextureSourceRect rect)
    {
        var minX = rect.Right;
        var minY = rect.Bottom;
        var maxX = rect.Left;
        var maxY = rect.Top;
        for (var y = rect.Top; y < rect.Bottom; y++)
        {
            var rowOffset = y * Width;
            for (var x = rect.Left; x < rect.Right; x++)
            {
                var alpha = _alphaPixels[rowOffset + x];
                if (alpha == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + 1);
                maxY = Math.Max(maxY, y + 1);
            }
        }

        return maxX > minX && maxY > minY
            ? new SpineAnimationTextureSourceAlphaBounds(true, minX, minY, maxX, maxY)
            : SpineAnimationTextureSourceAlphaBounds.Empty;
    }

    private SpineAnimationTextureSourceAlphaBounds ResolveVisibleTriangleBounds(SpineAnimationTextureTriangleKey key)
    {
        var ax = key.FirstX / TriangleCoordinateScale;
        var ay = key.FirstY / TriangleCoordinateScale;
        var bx = key.SecondX / TriangleCoordinateScale;
        var by = key.SecondY / TriangleCoordinateScale;
        var cx = key.ThirdX / TriangleCoordinateScale;
        var cy = key.ThirdY / TriangleCoordinateScale;
        var determinant = ((by - cy) * (ax - cx)) + ((cx - bx) * (ay - cy));
        if (Math.Abs(determinant) < TriangleDegenerateThreshold)
        {
            return SpineAnimationTextureSourceAlphaBounds.Empty;
        }

        var left = Math.Clamp((int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))), 0, Width);
        var top = Math.Clamp((int)Math.Floor(Math.Min(ay, Math.Min(by, cy))), 0, Height);
        var right = Math.Clamp((int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))), 0, Width);
        var bottom = Math.Clamp((int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))), 0, Height);
        if (right <= left || bottom <= top)
        {
            return SpineAnimationTextureSourceAlphaBounds.Empty;
        }

        var minX = right;
        var minY = bottom;
        var maxX = left;
        var maxY = top;
        for (var y = top; y < bottom; y++)
        {
            var rowOffset = y * Width;
            var sampleY = y + 0.5d;
            for (var x = left; x < right; x++)
            {
                var alpha = _alphaPixels[rowOffset + x];
                if (alpha == 0)
                {
                    continue;
                }

                var sampleX = x + 0.5d;
                if (!ContainsTrianglePoint(sampleX, sampleY, ax, ay, bx, by, cx, cy, determinant))
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + 1);
                maxY = Math.Max(maxY, y + 1);
            }
        }

        return maxX > minX && maxY > minY
            ? new SpineAnimationTextureSourceAlphaBounds(true, minX, minY, maxX, maxY)
            : SpineAnimationTextureSourceAlphaBounds.Empty;
    }

    private static bool ContainsTrianglePoint(
        double x,
        double y,
        double ax,
        double ay,
        double bx,
        double by,
        double cx,
        double cy,
        double determinant)
    {
        var first = (((by - cy) * (x - cx)) + ((cx - bx) * (y - cy))) / determinant;
        var second = (((cy - ay) * (x - cx)) + ((ax - cx) * (y - cy))) / determinant;
        var third = 1d - first - second;
        return first >= -TriangleBarycentricTolerance &&
               second >= -TriangleBarycentricTolerance &&
               third >= -TriangleBarycentricTolerance;
    }
}

internal readonly record struct SpineAnimationTextureTriangleKey(
    long FirstX,
    long FirstY,
    long SecondX,
    long SecondY,
    long ThirdX,
    long ThirdY)
{
    public static SpineAnimationTextureTriangleKey Create(
        double firstU,
        double firstV,
        double secondU,
        double secondV,
        double thirdU,
        double thirdV,
        int textureWidth,
        int textureHeight,
        double coordinateScale) =>
        new(
            ToKeyCoordinate(firstU, textureWidth, coordinateScale),
            ToKeyCoordinate(firstV, textureHeight, coordinateScale),
            ToKeyCoordinate(secondU, textureWidth, coordinateScale),
            ToKeyCoordinate(secondV, textureHeight, coordinateScale),
            ToKeyCoordinate(thirdU, textureWidth, coordinateScale),
            ToKeyCoordinate(thirdV, textureHeight, coordinateScale));

    private static long ToKeyCoordinate(
        double value,
        int textureExtent,
        double coordinateScale)
    {
        var scaled = Math.Round(value * textureExtent * coordinateScale, MidpointRounding.AwayFromZero);
        if (scaled >= long.MaxValue)
        {
            return long.MaxValue;
        }

        if (scaled <= long.MinValue)
        {
            return long.MinValue;
        }

        return (long)scaled;
    }
}

internal readonly record struct SpineAnimationTextureSourceRect(
    int Left,
    int Top,
    int Right,
    int Bottom)
{
    public bool IsValid => Right > Left && Bottom > Top;

    public static SpineAnimationTextureSourceRect Normalize(
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        int textureWidth,
        int textureHeight)
    {
        var left = Math.Clamp(sourceX, 0, textureWidth);
        var top = Math.Clamp(sourceY, 0, textureHeight);
        var right = Math.Clamp(sourceX + Math.Max(0, sourceWidth), 0, textureWidth);
        var bottom = Math.Clamp(sourceY + Math.Max(0, sourceHeight), 0, textureHeight);
        return new SpineAnimationTextureSourceRect(left, top, right, bottom);
    }
}

internal readonly record struct SpineAnimationTextureSourceAlphaBounds(
    bool HasVisiblePixels,
    int Left,
    int Top,
    int Right,
    int Bottom)
{
    public static SpineAnimationTextureSourceAlphaBounds Empty { get; } =
        new(false, 0, 0, 0, 0);
}
