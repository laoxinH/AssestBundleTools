using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal static class AssetPreviewMediaSourceFactory
{
    public static async Task<BitmapImage?> CreateImageSourceAsync(byte[]? imagePayload)
    {
        if (imagePayload is not { Length: > 0 })
        {
            return null;
        }

        var bitmapImage = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(imagePayload);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        await bitmapImage.SetSourceAsync(stream);
        return bitmapImage;
    }

    public static async Task<AudioPreviewSource?> CreateAudioSourceAsync(byte[]? audioPayload, string contentType)
    {
        if (audioPayload is not { Length: > 0 } || string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var stream = new InMemoryRandomAccessStream();

        try
        {
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(audioPayload);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var mediaSource = MediaSource.CreateFromStream(stream, contentType.Trim());
            return new AudioPreviewSource(mediaSource, stream);
        }
        catch
        {
            stream.Dispose();
            return null;
        }
    }
}
