using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal sealed record SpineAssetGroupTexturePreviewState(
    SpineAtlasPageDto AtlasPage,
    AssetPreviewDto Preview,
    ImageSource ImageSource,
    int PixelWidth,
    int PixelHeight);
