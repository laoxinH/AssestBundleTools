using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal sealed class InlineAssetPreviewTextSourceReader : IAssetPreviewTextSourceReader
{
    public TextReader OpenTextReader(AssetPreviewTextSourceDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!string.IsNullOrEmpty(source.InlineText))
        {
            return new StringReader(source.InlineText);
        }

        throw new InvalidDataException("Text source reference reader is not configured.");
    }
}
