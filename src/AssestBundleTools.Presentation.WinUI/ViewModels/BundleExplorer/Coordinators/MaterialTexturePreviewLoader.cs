using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal sealed class MaterialTexturePreviewLoader
{
    private const int MaxThumbnailPreviewCount = 6;

    private readonly AssetPreviewQueryExecutor _previewQueryExecutor;

    public MaterialTexturePreviewLoader(AssetPreviewQueryExecutor previewQueryExecutor)
    {
        _previewQueryExecutor = previewQueryExecutor ?? throw new ArgumentNullException(nameof(previewQueryExecutor));
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

        var thumbnailPayloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var textureCandidate in MaterialTextureDependencyResolver.CollectPreviewCandidates(
                     material,
                     details,
                     MaxThumbnailPreviewCount))
        {
            var texturePreview = await TryLoadTexturePreviewAsync(
                materialPreviewQuery,
                textureCandidate,
                previewCancellationToken,
                priority,
                isPreviewRequestCurrentAndRelevant);
            if (texturePreview?.BinaryContent is { Length: > 0 } binaryContent)
            {
                thumbnailPayloads[textureCandidate.AssetId.Value] = binaryContent.ToArray();
            }
        }

        return thumbnailPayloads;
    }

    private async Task<AssetPreviewDto?> TryLoadTexturePreviewAsync(
        GetAssetPreviewQuery materialPreviewQuery,
        AssetDependencyDto textureCandidate,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        try
        {
            var texturePreviewQuery = materialPreviewQuery with
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
