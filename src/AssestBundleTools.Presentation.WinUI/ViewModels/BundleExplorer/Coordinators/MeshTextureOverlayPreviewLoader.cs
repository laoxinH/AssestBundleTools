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
    private readonly AssetPreviewQueryExecutor _previewQueryExecutor;

    public MeshTextureOverlayPreviewLoader(AssetPreviewQueryExecutor previewQueryExecutor)
    {
        _previewQueryExecutor = previewQueryExecutor ?? throw new ArgumentNullException(nameof(previewQueryExecutor));
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

        foreach (var textureCandidate in MeshTextureAssociationCandidateResolver.CollectTextureOverlayCandidates(details, maxCount: 4))
        {
            var texturePreview = await TryLoadTexturePreviewAsync(
                meshPreviewQuery,
                textureCandidate,
                previewCancellationToken,
                priority,
                isPreviewRequestCurrentAndRelevant);
            if (texturePreview is not null)
            {
                return texturePreview.BinaryContent!.ToArray();
            }
        }

        return null;
    }

    private async Task<AssetPreviewDto?> TryLoadTexturePreviewAsync(
        GetAssetPreviewQuery meshPreviewQuery,
        AssetDependencyDto textureCandidate,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        try
        {
            var texturePreviewQuery = meshPreviewQuery with
            {
                BundleId = textureCandidate.BundleId,
                AssetId = textureCandidate.AssetId
            };

            var texturePreviewResult = await _previewQueryExecutor.ExecuteAsync(
                texturePreviewQuery,
                previewCancellationToken,
                priority,
                isPreviewRequestCurrentAndRelevant);
            if (texturePreviewResult is null ||
                !texturePreviewResult.IsSuccess ||
                texturePreviewResult.Value is not { Kind: AssetPreviewKind.Image, BinaryContent.Length: > 0 } texturePreview)
            {
                return null;
            }

            return texturePreview;
        }
        catch (OperationCanceledException) when (previewCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }
}
