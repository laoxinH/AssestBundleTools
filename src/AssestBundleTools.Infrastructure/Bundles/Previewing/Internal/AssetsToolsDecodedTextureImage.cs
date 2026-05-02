namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal readonly record struct AssetsToolsDecodedTextureImage(
    int Width,
    int Height,
    byte[] RgbaPixels);
