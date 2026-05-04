using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 复用现有资产预览查询管线加载 Texture2D / Sprite 的图片载荷，避免各富预览各自实现缓存旁路。
/// </summary>
internal sealed class TexturePreviewPayloadLoader
{
    private readonly AssetPreviewQueryExecutor _previewQueryExecutor;

    public TexturePreviewPayloadLoader(AssetPreviewQueryExecutor previewQueryExecutor)
    {
        _previewQueryExecutor = previewQueryExecutor ?? throw new ArgumentNullException(nameof(previewQueryExecutor));
    }

    public async Task<byte[]?> LoadFirstAsync(
        GetAssetPreviewQuery sourcePreviewQuery,
        IEnumerable<AssetDependencyDto> textureCandidates,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        ArgumentNullException.ThrowIfNull(sourcePreviewQuery);
        ArgumentNullException.ThrowIfNull(textureCandidates);
        ArgumentNullException.ThrowIfNull(isPreviewRequestCurrentAndRelevant);

        foreach (var textureCandidate in textureCandidates)
        {
            var texturePreview = await TryLoadTexturePreviewAsync(
                sourcePreviewQuery,
                textureCandidate,
                previewCancellationToken,
                priority,
                isPreviewRequestCurrentAndRelevant);
            if (texturePreview?.BinaryContent is { Length: > 0 } binaryContent)
            {
                return binaryContent.ToArray();
            }
        }

        return null;
    }

    public async Task<IReadOnlyDictionary<string, byte[]>> LoadManyAsync(
        GetAssetPreviewQuery sourcePreviewQuery,
        IEnumerable<AssetDependencyDto> textureCandidates,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        ArgumentNullException.ThrowIfNull(sourcePreviewQuery);
        ArgumentNullException.ThrowIfNull(textureCandidates);
        ArgumentNullException.ThrowIfNull(isPreviewRequestCurrentAndRelevant);

        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var textureCandidate in textureCandidates)
        {
            var texturePreview = await TryLoadTexturePreviewAsync(
                sourcePreviewQuery,
                textureCandidate,
                previewCancellationToken,
                priority,
                isPreviewRequestCurrentAndRelevant);
            if (texturePreview?.BinaryContent is { Length: > 0 } binaryContent)
            {
                payloads[textureCandidate.AssetId.Value] = binaryContent.ToArray();
            }
        }

        return payloads;
    }

    private async Task<AssetPreviewDto?> TryLoadTexturePreviewAsync(
        GetAssetPreviewQuery sourcePreviewQuery,
        AssetDependencyDto textureCandidate,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        try
        {
            var texturePreviewQuery = sourcePreviewQuery with
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
