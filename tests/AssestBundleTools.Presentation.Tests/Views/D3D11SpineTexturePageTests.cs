using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class D3D11SpineTexturePageTests
{
    [Fact]
    public async Task DecodeAsync_ShouldDecodeImagePayloadToBgraPixels()
    {
        var texturePages = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["page.png"] = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=")
        };

        var decodedPages = await D3D11SpineTexturePageDecoder.DecodeAsync(texturePages);

        var decodedPage = Assert.Single(decodedPages).Value;
        Assert.Equal("page.png", decodedPage.PageName);
        Assert.Equal(1, decodedPage.Width);
        Assert.Equal(1, decodedPage.Height);
        Assert.Equal(4, decodedPage.BgraPixels.Length);
    }

    [Fact]
    public void ReplaceDecodedPages_ShouldExposeTexturePageSizesAndClearGpuState()
    {
        using var cache = new D3D11SpineTexturePageCache();
        var decodedPages = new Dictionary<string, D3D11SpineDecodedTexturePage>(StringComparer.Ordinal)
        {
            ["page.png"] = new("page.png", 2, 3, new byte[2 * 3 * 4])
        };

        cache.ReplaceDecodedPages(decodedPages);

        var size = Assert.Single(cache.TexturePageSizes).Value;
        Assert.Equal(2, size.Width);
        Assert.Equal(3, size.Height);
        Assert.Empty(cache.GpuPages);

        cache.Clear();

        Assert.Empty(cache.TexturePageSizes);
        Assert.Empty(cache.GpuPages);
    }
}
