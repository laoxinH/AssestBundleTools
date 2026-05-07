using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal sealed record SpineAssetGroupDetailsLoadResult(
    SpineAssetGroupDetailsDto Details,
    SpineAssetGroupTexturePreviewState? TexturePreview);
