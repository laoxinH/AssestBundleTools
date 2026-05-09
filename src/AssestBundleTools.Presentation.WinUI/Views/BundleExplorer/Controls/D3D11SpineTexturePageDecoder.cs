using Windows.Graphics.Imaging;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class D3D11SpineTexturePageDecoder
{
    public static async Task<IReadOnlyDictionary<string, D3D11SpineDecodedTexturePage>> DecodeAsync(
        IReadOnlyDictionary<string, byte[]> texturePageContents)
    {
        ArgumentNullException.ThrowIfNull(texturePageContents);

        var decodedPages = new Dictionary<string, D3D11SpineDecodedTexturePage>(StringComparer.Ordinal);
        foreach (var pair in texturePageContents)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value.Length == 0)
            {
                continue;
            }

            var decodedPage = await TryDecodeAsync(pair.Key, pair.Value);
            if (decodedPage is not null)
            {
                decodedPages[pair.Key] = decodedPage;
            }
        }

        return decodedPages;
    }

    private static async Task<D3D11SpineDecodedTexturePage?> TryDecodeAsync(
        string pageName,
        byte[] textureContent)
    {
        try
        {
            using var stream = await SpineAnimationTextureStreamFactory.CreateAsync(textureContent);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);
            var width = checked((int)decoder.PixelWidth);
            var height = checked((int)decoder.PixelHeight);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return new D3D11SpineDecodedTexturePage(
                pageName,
                width,
                height,
                pixelData.DetachPixelData());
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record D3D11SpineDecodedTexturePage(
    string PageName,
    int Width,
    int Height,
    byte[] BgraPixels);
