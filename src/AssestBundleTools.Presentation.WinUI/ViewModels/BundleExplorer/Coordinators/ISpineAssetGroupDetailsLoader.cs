using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal interface ISpineAssetGroupDetailsLoader
{
    Task<SpineAssetGroupDetailsLoadResult?> LoadAsync(
        SpineAssetGroupDocumentTabViewModel groupTab,
        CancellationToken cancellationToken = default);

    Task<SpineAssetGroupTexturePreviewState?> LoadTexturePreviewAsync(
        SpineAssetGroupDocumentTabViewModel groupTab,
        SpineAtlasPageOptionViewModel atlasPageOption,
        CancellationToken cancellationToken = default);
}
