using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal sealed class MaterialTexturePreviewLoader
{
    private const int MaxThumbnailPreviewCount = 6;

    private readonly TexturePreviewPayloadLoader _texturePreviewPayloadLoader;

    public MaterialTexturePreviewLoader(TexturePreviewPayloadLoader texturePreviewPayloadLoader)
    {
        _texturePreviewPayloadLoader = texturePreviewPayloadLoader ?? throw new ArgumentNullException(nameof(texturePreviewPayloadLoader));
    }

    public async Task<IReadOnlyDictionary<string, byte[]>> LoadAsync(
        AssetDetailsDto details,
        AssetPreviewMaterialDto material,
        GetAssetPreviewQuery materialPreviewQuery,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(materialPreviewQuery);
        ArgumentNullException.ThrowIfNull(isPreviewRequestCurrentAndRelevant);

        return await _texturePreviewPayloadLoader.LoadManyAsync(
            materialPreviewQuery,
            MaterialTextureDependencyResolver.CollectPreviewCandidates(
                material,
                details,
                MaxThumbnailPreviewCount),
            previewCancellationToken,
            priority,
            isPreviewRequestCurrentAndRelevant);
    }
}
