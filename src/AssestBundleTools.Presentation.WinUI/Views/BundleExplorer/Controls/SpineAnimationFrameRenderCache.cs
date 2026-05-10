using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class SpineAnimationFrameRenderCache : IDisposable
{
    private const int MaxCachedFrames = 10;
    private const long MaxCachedPixels = 12_000_000L;
    private const float RenderTargetDpi = 96f;

    private readonly Dictionary<CacheKey, CacheEntry> _entries = [];
    private long _useStamp;

    public void Draw(
        CanvasDrawingSession drawingSession,
        ICanvasResourceCreator resourceCreator,
        SpineAnimationFrameDto frame,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePageSizes,
        float width,
        float height,
        SpineAnimationPreviewViewport viewport,
        SpineAnimationAutoFitBounds autoFitBounds = default)
    {
        ArgumentNullException.ThrowIfNull(drawingSession);
        ArgumentNullException.ThrowIfNull(resourceCreator);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(textureBitmaps);
        ArgumentNullException.ThrowIfNull(texturePageSizes);

        var key = CacheKey.Create(frame, width, height, viewport, autoFitBounds);
        var target = GetOrCreateTarget(
            resourceCreator,
            frame,
            textureBitmaps,
            texturePageSizes,
            key,
            viewport,
            autoFitBounds);
        drawingSession.DrawImage(
            target,
            new Rect(0d, 0d, width, height),
            new Rect(0d, 0d, key.Width, key.Height));
    }

    public void Clear()
    {
        foreach (var entry in _entries.Values)
        {
            entry.Target.Dispose();
        }

        _entries.Clear();
    }

    public void Dispose()
    {
        Clear();
    }

    private CanvasRenderTarget GetOrCreateTarget(
        ICanvasResourceCreator resourceCreator,
        SpineAnimationFrameDto frame,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePageSizes,
        CacheKey key,
        SpineAnimationPreviewViewport viewport,
        SpineAnimationAutoFitBounds autoFitBounds)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            if (ReferenceEquals(entry.Frame, frame))
            {
                entry.LastUsedStamp = ++_useStamp;
                return entry.Target;
            }

            entry.Target.Dispose();
            _entries.Remove(key);
        }

        var target = new CanvasRenderTarget(resourceCreator, key.Width, key.Height, RenderTargetDpi);
        using (var targetSession = target.CreateDrawingSession())
        {
            targetSession.Clear(Color.FromArgb(0, 0, 0, 0));
            InspectorSpineAnimationPreviewRenderer.Draw(
                targetSession,
                frame,
                textureBitmaps,
                texturePageSizes,
                key.Width,
                key.Height,
                viewport,
                autoFitBounds);
        }

        _entries[key] = new CacheEntry(frame, target, key.PixelCount, ++_useStamp);
        Trim();
        return target;
    }

    private void Trim()
    {
        while (_entries.Count > 1 &&
               (_entries.Count > MaxCachedFrames || ResolveCachedPixelCount() > MaxCachedPixels))
        {
            var oldestKey = default(CacheKey);
            CacheEntry? oldestEntry = null;
            foreach (var pair in _entries)
            {
                if (oldestEntry is null || pair.Value.LastUsedStamp < oldestEntry.LastUsedStamp)
                {
                    oldestKey = pair.Key;
                    oldestEntry = pair.Value;
                }
            }

            if (oldestEntry is null)
            {
                return;
            }

            oldestEntry.Target.Dispose();
            _entries.Remove(oldestKey);
        }
    }

    private long ResolveCachedPixelCount()
    {
        var pixelCount = 0L;
        foreach (var entry in _entries.Values)
        {
            pixelCount += entry.PixelCount;
        }

        return pixelCount;
    }

    private sealed class CacheEntry(
        SpineAnimationFrameDto frame,
        CanvasRenderTarget target,
        long pixelCount,
        long lastUsedStamp)
    {
        public SpineAnimationFrameDto Frame { get; } = frame;

        public CanvasRenderTarget Target { get; } = target;

        public long PixelCount { get; } = pixelCount;

        public long LastUsedStamp { get; set; } = lastUsedStamp;
    }

    private readonly record struct CacheKey(
        int FrameIdentity,
        int Width,
        int Height,
        SpineAnimationPreviewViewport Viewport,
        SpineAnimationAutoFitBounds AutoFitBounds)
    {
        public long PixelCount => (long)Width * Height;

        public static CacheKey Create(
            SpineAnimationFrameDto frame,
            float width,
            float height,
            SpineAnimationPreviewViewport viewport,
            SpineAnimationAutoFitBounds autoFitBounds)
        {
            var normalizedViewport = viewport.Normalize();
            return new CacheKey(
                RuntimeHelpers.GetHashCode(frame),
                Math.Max(1, (int)Math.Ceiling(width)),
                Math.Max(1, (int)Math.Ceiling(height)),
                normalizedViewport,
                autoFitBounds);
        }
    }
}
