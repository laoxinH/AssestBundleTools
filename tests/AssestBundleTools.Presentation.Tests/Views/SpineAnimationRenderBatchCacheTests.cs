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
    public void GetClipBatches_ShouldReuseBatches_ForSameSourceAndViewport()
    {
        var cache = new SpineAnimationRenderBatchCache();
        var vertexBatches = cache.GetVertexBatches(CreateFrame(), CreateTexturePages(), texturePageVersion: 1);

        var first = cache.GetClipBatches(vertexBatches, 320f, 240f);
        var second = cache.GetClipBatches(vertexBatches, 320f, 240f);
        var afterResize = cache.GetClipBatches(vertexBatches, 480f, 240f);

        Assert.Same(first, second);
        Assert.NotSame(first, afterResize);
    }

    [Fact]
    public void Clear_ShouldDropCachedVertexAndClipBatches()
    {
        var cache = new SpineAnimationRenderBatchCache();
        var frame = CreateFrame();
        var texturePages = CreateTexturePages();
        var first = cache.GetVertexBatches(frame, texturePages, texturePageVersion: 1);
        var firstClip = cache.GetClipBatches(first, 320f, 240f);

        cache.Clear();

        var second = cache.GetVertexBatches(frame, texturePages, texturePageVersion: 1);
        var secondClip = cache.GetClipBatches(second, 320f, 240f);

        Assert.NotSame(first, second);
        Assert.NotSame(firstClip, secondClip);
    }

    private static SpineAnimationFrameDto CreateFrame() =>
        new(
            0d,
            [
                new SpineAnimationDrawCommandDto(
                    "slot",
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
