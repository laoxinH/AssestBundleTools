using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationTextureStreamFactory
{
    public static async Task<IRandomAccessStream> CreateAsync(byte[] textureContent)
    {
        ArgumentNullException.ThrowIfNull(textureContent);

        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(textureContent);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        return stream;
    }
}
