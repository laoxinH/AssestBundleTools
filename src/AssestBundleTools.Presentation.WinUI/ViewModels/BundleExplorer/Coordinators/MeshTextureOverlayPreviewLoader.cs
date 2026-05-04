using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 通过现有预览查询管线加载 Mesh 可叠加的关联贴图载荷。
/// </summary>
internal sealed class MeshTextureOverlayPreviewLoader
{
    private readonly TexturePreviewPayloadLoader _texturePreviewPayloadLoader;

    public MeshTextureOverlayPreviewLoader(TexturePreviewPayloadLoader texturePreviewPayloadLoader)
    {
        _texturePreviewPayloadLoader = texturePreviewPayloadLoader ?? throw new ArgumentNullException(nameof(texturePreviewPayloadLoader));
    }

    public async Task<byte[]?> LoadAsync(
        AssetDetailsDto details,
        GetAssetPreviewQuery meshPreviewQuery,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(meshPreviewQuery);
        ArgumentNullException.ThrowIfNull(isPreviewRequestCurrentAndRelevant);

        return await _texturePreviewPayloadLoader.LoadFirstAsync(
            meshPreviewQuery,
            MeshTextureAssociationCandidateResolver.CollectTextureOverlayCandidates(details, maxCount: 4),
            previewCancellationToken,
            priority,
            isPreviewRequestCurrentAndRelevant);
    }
}
