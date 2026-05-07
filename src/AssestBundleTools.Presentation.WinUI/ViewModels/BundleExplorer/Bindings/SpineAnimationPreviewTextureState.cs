using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal sealed record SpineAnimationPreviewTextureState(
    string PageName,
    SpineAssetGroupTexturePreviewState TexturePreview)
{
    public byte[]? BinaryContent => TexturePreview.Preview.BinaryContent;

    public int PixelWidth => TexturePreview.PixelWidth;

    public int PixelHeight => TexturePreview.PixelHeight;
}
