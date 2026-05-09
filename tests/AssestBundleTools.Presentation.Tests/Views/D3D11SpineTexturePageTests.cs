using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class D3D11SpineTexturePageTests
{
    [Fact]
    public async Task DecodeAsync_ShouldDecodeImagePayloadToBgraPixels()
    {
        var texturePages = new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal)
        {
            ["page.png"] = new(
                "page.png",
                Convert.FromBase64String(
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="))
        };

        var decodedPages = await SpineAnimationTexturePageDecoder.DecodeAsync(texturePages);

        var decodedPage = Assert.Single(decodedPages).Value;
        Assert.Equal("page.png", decodedPage.PageName);
        Assert.Equal(1, decodedPage.Width);
        Assert.Equal(1, decodedPage.Height);
        Assert.Equal(4, decodedPage.BgraPixels.Length);
    }

    [Fact]
    public async Task DecodeAsync_ShouldComposeExternalAlphaAndPremultiplyPixels()
    {
        var texturePages = new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal)
        {
            ["page.png"] = new(
                "page.png",
                CreateRgbBmp(255, 0, 0),
                CreateRgbBmp(128, 128, 128))
        };

        var decodedPages = await SpineAnimationTexturePageDecoder.DecodeAsync(texturePages);

        var decodedPage = Assert.Single(decodedPages).Value;
        Assert.True(decodedPage.HasExternalAlpha);
        Assert.Equal(0, decodedPage.BgraPixels[0]);
        Assert.Equal(0, decodedPage.BgraPixels[1]);
        Assert.Equal(128, decodedPage.BgraPixels[2]);
        Assert.Equal(128, decodedPage.BgraPixels[3]);
    }

    [Fact]
    public void ReplaceDecodedPages_ShouldExposeTexturePageSizesAndClearGpuState()
    {
        using var cache = new D3D11SpineTexturePageCache();
        var decodedPages = new Dictionary<string, SpineAnimationDecodedTexturePage>(StringComparer.Ordinal)
        {
            ["page.png"] = new("page.png", 2, 3, new byte[2 * 3 * 4], HasExternalAlpha: false)
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

    private static byte[] CreateRgbBmp(byte red, byte green, byte blue)
    {
        const int fileHeaderSize = 14;
        const int dibHeaderSize = 40;
        const int width = 1;
        const int height = 1;
        const int bytesPerPixel = 3;
        const int rowSize = 4;
        const int pixelDataSize = rowSize * height;
        const int pixelDataOffset = fileHeaderSize + dibHeaderSize;
        const int fileSize = pixelDataOffset + pixelDataSize;

        var bytes = new byte[fileSize];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        WriteInt32(bytes, 2, fileSize);
        WriteInt32(bytes, 10, pixelDataOffset);
        WriteInt32(bytes, 14, dibHeaderSize);
        WriteInt32(bytes, 18, width);
        WriteInt32(bytes, 22, height);
        WriteInt16(bytes, 26, 1);
        WriteInt16(bytes, 28, bytesPerPixel * 8);
        WriteInt32(bytes, 34, pixelDataSize);
        bytes[pixelDataOffset] = blue;
        bytes[pixelDataOffset + 1] = green;
        bytes[pixelDataOffset + 2] = red;
        return bytes;
    }

    private static void WriteInt16(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }
}
