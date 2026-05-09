using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Windows.Graphics.Imaging;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationTexturePageDecoder
{
    public static async Task<IReadOnlyDictionary<string, SpineAnimationDecodedTexturePage>> DecodeAsync(
        IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents)
    {
        ArgumentNullException.ThrowIfNull(texturePageContents);

        var decodedPages = new Dictionary<string, SpineAnimationDecodedTexturePage>(StringComparer.Ordinal);
        foreach (var pair in texturePageContents)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) ||
                pair.Value.ColorContent.Length == 0)
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

    private static async Task<SpineAnimationDecodedTexturePage?> TryDecodeAsync(
        string pageName,
        SpineAnimationTexturePageContent textureContent)
    {
        try
        {
            var colorPage = await TryDecodeBgraPixelsAsync(textureContent.ColorContent);
            if (colorPage is null)
            {
                return null;
            }

            var hasExternalAlpha = await TryApplyExternalAlphaAsync(colorPage, textureContent.AlphaContent);
            PremultiplyBgra(colorPage.BgraPixels);
            return new SpineAnimationDecodedTexturePage(
                pageName,
                colorPage.Width,
                colorPage.Height,
                colorPage.BgraPixels,
                hasExternalAlpha);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> TryApplyExternalAlphaAsync(
        DecodedBgraPixels colorPage,
        byte[]? alphaContent)
    {
        if (alphaContent is not { Length: > 0 })
        {
            return false;
        }

        var alphaPage = await TryDecodeBgraPixelsAsync(alphaContent);
        if (alphaPage is null ||
            alphaPage.Width != colorPage.Width ||
            alphaPage.Height != colorPage.Height)
        {
            return false;
        }

        var useAlphaChannel = HasNonOpaqueAlpha(alphaPage.BgraPixels);
        for (var index = 0; index < colorPage.BgraPixels.Length; index += 4)
        {
            colorPage.BgraPixels[index + 3] = useAlphaChannel
                ? alphaPage.BgraPixels[index + 3]
                : ResolveLuminanceAlpha(alphaPage.BgraPixels[index], alphaPage.BgraPixels[index + 1], alphaPage.BgraPixels[index + 2]);
        }

        return true;
    }

    private static async Task<DecodedBgraPixels?> TryDecodeBgraPixelsAsync(byte[] textureContent)
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

        return new DecodedBgraPixels(width, height, pixelData.DetachPixelData());
    }

    private static void PremultiplyBgra(byte[] bgraPixels)
    {
        for (var index = 0; index < bgraPixels.Length; index += 4)
        {
            var alpha = bgraPixels[index + 3];
            if (alpha == byte.MaxValue)
            {
                continue;
            }

            bgraPixels[index] = PremultiplyChannel(bgraPixels[index], alpha);
            bgraPixels[index + 1] = PremultiplyChannel(bgraPixels[index + 1], alpha);
            bgraPixels[index + 2] = PremultiplyChannel(bgraPixels[index + 2], alpha);
        }
    }

    private static bool HasNonOpaqueAlpha(byte[] bgraPixels)
    {
        for (var index = 3; index < bgraPixels.Length; index += 4)
        {
            if (bgraPixels[index] != byte.MaxValue)
            {
                return true;
            }
        }

        return false;
    }

    private static byte ResolveLuminanceAlpha(byte blue, byte green, byte red)
    {
        var value = (0.0722d * blue) + (0.7152d * green) + (0.2126d * red);
        return (byte)Math.Clamp((int)Math.Round(value), 0, byte.MaxValue);
    }

    private static byte PremultiplyChannel(byte color, byte alpha) =>
        (byte)((color * alpha + 127) / byte.MaxValue);

    private sealed record DecodedBgraPixels(
        int Width,
        int Height,
        byte[] BgraPixels);
}

internal sealed record SpineAnimationDecodedTexturePage(
    string PageName,
    int Width,
    int Height,
    byte[] BgraPixels,
    bool HasExternalAlpha);
