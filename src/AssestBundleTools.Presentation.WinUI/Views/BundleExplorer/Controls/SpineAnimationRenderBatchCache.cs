using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed class SpineAnimationRenderBatchCache
{
    private const int DefaultMaxCachedFrames = 96;

    private static readonly IReadOnlyList<SpineAnimationVertexBatch> EmptyVertexBatches =
        Array.Empty<SpineAnimationVertexBatch>();

    private readonly int _maxCachedFrames;
    private readonly Dictionary<SpineAnimationFrameDto, VertexBatchCacheEntry> _vertexEntries;
    private long _cacheStamp;
    private int _texturePageVersion = -1;

    public SpineAnimationRenderBatchCache(int maxCachedFrames = DefaultMaxCachedFrames)
    {
        _maxCachedFrames = Math.Max(1, maxCachedFrames);
        _vertexEntries = new Dictionary<SpineAnimationFrameDto, VertexBatchCacheEntry>(
            ReferenceEqualityComparer<SpineAnimationFrameDto>.Instance);
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

    public bool TryGetVertexBatches(
        SpineAnimationFrameDto? frame,
        int texturePageVersion,
        out IReadOnlyList<SpineAnimationVertexBatch> batches)
    {
        if (frame is null ||
            _texturePageVersion != texturePageVersion ||
            !_vertexEntries.TryGetValue(frame, out var entry))
        {
            batches = EmptyVertexBatches;
            return false;
        }

        entry.LastUsedStamp = NextStamp();
        batches = entry.VertexBatches;
        return true;
    }

    public void StoreVertexBatches(
        SpineAnimationFrameDto frame,
        int texturePageVersion,
        IReadOnlyList<SpineAnimationVertexBatch> batches)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(batches);

        if (_texturePageVersion != texturePageVersion)
        {
            Clear();
            _texturePageVersion = texturePageVersion;
        }

        if (_vertexEntries.TryGetValue(frame, out var entry))
        {
            if (ReferenceEquals(entry.VertexBatches, batches))
            {
                entry.LastUsedStamp = NextStamp();
                return;
            }
        }

        _vertexEntries[frame] = new VertexBatchCacheEntry(batches, NextStamp());
        TrimVertexEntries();
    }

    public void Clear()
    {
        _texturePageVersion = -1;
        _vertexEntries.Clear();
        _cacheStamp = 0;
    }

    private void TrimVertexEntries()
    {
        while (_vertexEntries.Count > _maxCachedFrames)
        {
            var leastUsed = _vertexEntries
                .OrderBy(static pair => pair.Value.LastUsedStamp)
                .First();
            _vertexEntries.Remove(leastUsed.Key);
        }
    }

    private long NextStamp() =>
        ++_cacheStamp;

    private sealed class VertexBatchCacheEntry(
        IReadOnlyList<SpineAnimationVertexBatch> vertexBatches,
        long lastUsedStamp)
    {
        public IReadOnlyList<SpineAnimationVertexBatch> VertexBatches { get; } = vertexBatches;

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
