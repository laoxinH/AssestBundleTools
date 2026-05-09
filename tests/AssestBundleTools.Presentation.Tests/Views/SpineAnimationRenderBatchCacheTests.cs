using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SpineAnimationRenderBatchCacheTests
{
    [Fact]
    public void GetVertexBatches_ShouldReuseBatches_ForSameFrameAndTextureVersion()
    {
        var cache = new SpineAnimationRenderBatchCache();
        var frame = CreateFrame();
        var texturePages = CreateTexturePages();

        var first = cache.GetVertexBatches(frame, texturePages, texturePageVersion: 1);
        var second = cache.GetVertexBatches(frame, texturePages, texturePageVersion: 1);
        var afterTextureChange = cache.GetVertexBatches(frame, texturePages, texturePageVersion: 2);

        Assert.Same(first, second);
        Assert.NotSame(first, afterTextureChange);
    }

    [Fact]
    public void GetVertexBatches_ShouldReuseEarlierFrame_WhenWithinCapacity()
    {
        var cache = new SpineAnimationRenderBatchCache(maxCachedFrames: 2);
        var firstFrame = CreateFrame("first");
        var secondFrame = CreateFrame("second");
        var texturePages = CreateTexturePages();

        var first = cache.GetVertexBatches(firstFrame, texturePages, texturePageVersion: 1);
        _ = cache.GetVertexBatches(secondFrame, texturePages, texturePageVersion: 1);
        var reused = cache.GetVertexBatches(firstFrame, texturePages, texturePageVersion: 1);

        Assert.Same(first, reused);
    }

    [Fact]
    public void TryGetVertexBatches_ShouldReturnStoredBatches_ForMatchingFrameAndTextureVersion()
    {
        var cache = new SpineAnimationRenderBatchCache();
        var frame = CreateFrame();
        var stored = SpineAnimationVertexBatchProjector.Project(frame, CreateTexturePages());

        cache.StoreVertexBatches(frame, texturePageVersion: 3, stored);
        var result = cache.TryGetVertexBatches(frame, texturePageVersion: 3, out var resolved);

        Assert.True(result);
        Assert.Same(stored, resolved);
    }

    [Fact]
    public void TryGetVertexBatches_ShouldRejectStoredBatches_WhenTextureVersionChanges()
    {
        var cache = new SpineAnimationRenderBatchCache();
        var frame = CreateFrame();
        var stored = SpineAnimationVertexBatchProjector.Project(frame, CreateTexturePages());

        cache.StoreVertexBatches(frame, texturePageVersion: 3, stored);
        var result = cache.TryGetVertexBatches(frame, texturePageVersion: 4, out var resolved);

        Assert.False(result);
        Assert.Empty(resolved);
    }

    [Fact]
    public void GetVertexBatches_ShouldEvictLeastRecentlyUsedFrame_WhenCapacityIsExceeded()
    {
        var cache = new SpineAnimationRenderBatchCache(maxCachedFrames: 2);
        var firstFrame = CreateFrame("first");
        var secondFrame = CreateFrame("second");
        var thirdFrame = CreateFrame("third");
        var texturePages = CreateTexturePages();
        var first = cache.GetVertexBatches(firstFrame, texturePages, texturePageVersion: 1);
        _ = cache.GetVertexBatches(secondFrame, texturePages, texturePageVersion: 1);

        _ = cache.GetVertexBatches(thirdFrame, texturePages, texturePageVersion: 1);
        var afterEviction = cache.GetVertexBatches(firstFrame, texturePages, texturePageVersion: 1);

        Assert.NotSame(first, afterEviction);
    }

    [Fact]
    public void Clear_ShouldDropCachedVertexBatches()
    {
        var cache = new SpineAnimationRenderBatchCache();
        var frame = CreateFrame();
        var texturePages = CreateTexturePages();
        var first = cache.GetVertexBatches(frame, texturePages, texturePageVersion: 1);

        cache.Clear();

        var second = cache.GetVertexBatches(frame, texturePages, texturePageVersion: 1);

        Assert.NotSame(first, second);
    }

    private static SpineAnimationFrameDto CreateFrame(string slotName = "slot") =>
        new(
            0d,
            [
                new SpineAnimationDrawCommandDto(
                    slotName,
                    "attachment",
                    "region",
                    "page.png",
                    SourceX: 0,
                    SourceY: 0,
                    SourceWidth: 10,
                    SourceHeight: 10,
                    Width: 10d,
                    Height: 10d,
                    TransformM11: 1d,
                    TransformM12: 0d,
                    TransformM21: 0d,
                    TransformM22: 1d,
                    TransformM31: 0d,
                    TransformM32: 0d)
            ]);

    private static IReadOnlyDictionary<string, SpineAnimationTexturePageSize> CreateTexturePages() =>
        new Dictionary<string, SpineAnimationTexturePageSize>(StringComparer.Ordinal)
        {
            ["page.png"] = new(100, 100)
        };
}
