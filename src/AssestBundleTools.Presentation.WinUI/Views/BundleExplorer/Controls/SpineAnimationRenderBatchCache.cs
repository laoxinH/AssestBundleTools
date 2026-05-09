using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class SpineAnimationRenderBatchCache
{
    private const int DefaultMaxCachedFrames = 96;
    private const int MaxClipViewportsPerFrame = 2;

    private static readonly IReadOnlyList<SpineAnimationVertexBatch> EmptyVertexBatches =
        Array.Empty<SpineAnimationVertexBatch>();
    private static readonly IReadOnlyList<SpineAnimationClipSpaceBatch> EmptyClipBatches =
        Array.Empty<SpineAnimationClipSpaceBatch>();

    private readonly int _maxCachedFrames;
    private readonly Dictionary<SpineAnimationFrameDto, VertexBatchCacheEntry> _vertexEntries;
    private readonly Dictionary<IReadOnlyList<SpineAnimationVertexBatch>, List<ClipBatchCacheEntry>> _clipEntries;
    private long _cacheStamp;
    private int _texturePageVersion = -1;

    public SpineAnimationRenderBatchCache(int maxCachedFrames = DefaultMaxCachedFrames)
    {
        _maxCachedFrames = Math.Max(1, maxCachedFrames);
        _vertexEntries = new Dictionary<SpineAnimationFrameDto, VertexBatchCacheEntry>(
            ReferenceEqualityComparer<SpineAnimationFrameDto>.Instance);
        _clipEntries = new Dictionary<IReadOnlyList<SpineAnimationVertexBatch>, List<ClipBatchCacheEntry>>(
            ReferenceEqualityComparer<IReadOnlyList<SpineAnimationVertexBatch>>.Instance);
    }

    public IReadOnlyList<SpineAnimationVertexBatch> GetVertexBatches(
        SpineAnimationFrameDto? frame,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePages,
        int texturePageVersion)
    {
        ArgumentNullException.ThrowIfNull(texturePages);

        if (frame is null || texturePages.Count == 0)
        {
            Clear();
            return EmptyVertexBatches;
        }

        if (_texturePageVersion != texturePageVersion)
        {
            Clear();
            _texturePageVersion = texturePageVersion;
        }

        if (_vertexEntries.TryGetValue(frame, out var entry))
        {
            entry.LastUsedStamp = NextStamp();
            return entry.VertexBatches;
        }

        var batches = SpineAnimationVertexBatchProjector.Project(frame, texturePages);
        _vertexEntries.Add(frame, new VertexBatchCacheEntry(batches, NextStamp()));
        TrimVertexEntries();
        return batches;
    }

    public IReadOnlyList<SpineAnimationClipSpaceBatch> GetClipBatches(
        IReadOnlyList<SpineAnimationVertexBatch> batches,
        float viewportWidth,
        float viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(batches);

        if (batches.Count == 0)
        {
            return EmptyClipBatches;
        }

        if (!_clipEntries.TryGetValue(batches, out var entries))
        {
            entries = [];
            _clipEntries.Add(batches, entries);
        }

        foreach (var entry in entries)
        {
            if (NearlyEquals(entry.ViewportWidth, viewportWidth) &&
                NearlyEquals(entry.ViewportHeight, viewportHeight))
            {
                entry.LastUsedStamp = NextStamp();
                return entry.ClipBatches;
            }
        }

        var clipBatches = SpineAnimationClipSpaceProjector.Project(batches, viewportWidth, viewportHeight);
        entries.Add(new ClipBatchCacheEntry(viewportWidth, viewportHeight, clipBatches, NextStamp()));
        TrimClipEntries(entries);
        return clipBatches;
    }

    public void Clear()
    {
        _texturePageVersion = -1;
        _vertexEntries.Clear();
        _clipEntries.Clear();
        _cacheStamp = 0;
    }

    private void TrimVertexEntries()
    {
        while (_vertexEntries.Count > _maxCachedFrames)
        {
            var leastUsed = _vertexEntries
                .OrderBy(static pair => pair.Value.LastUsedStamp)
                .First();
            _clipEntries.Remove(leastUsed.Value.VertexBatches);
            _vertexEntries.Remove(leastUsed.Key);
        }
    }

    private void TrimClipEntries(List<ClipBatchCacheEntry> entries)
    {
        while (entries.Count > MaxClipViewportsPerFrame)
        {
            var leastUsed = entries
                .OrderBy(static entry => entry.LastUsedStamp)
                .First();
            entries.Remove(leastUsed);
        }
    }

    private long NextStamp() =>
        ++_cacheStamp;

    private static bool NearlyEquals(float left, float right) =>
        Math.Abs(left - right) < 0.001f;

    private sealed class VertexBatchCacheEntry(
        IReadOnlyList<SpineAnimationVertexBatch> vertexBatches,
        long lastUsedStamp)
    {
        public IReadOnlyList<SpineAnimationVertexBatch> VertexBatches { get; } = vertexBatches;

        public long LastUsedStamp { get; set; } = lastUsedStamp;
    }

    private sealed class ClipBatchCacheEntry(
        float viewportWidth,
        float viewportHeight,
        IReadOnlyList<SpineAnimationClipSpaceBatch> clipBatches,
        long lastUsedStamp)
    {
        public float ViewportWidth { get; } = viewportWidth;

        public float ViewportHeight { get; } = viewportHeight;

        public IReadOnlyList<SpineAnimationClipSpaceBatch> ClipBatches { get; } = clipBatches;

        public long LastUsedStamp { get; set; } = lastUsedStamp;
    }

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static ReferenceEqualityComparer<T> Instance { get; } = new();

        public bool Equals(T? x, T? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(T obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
