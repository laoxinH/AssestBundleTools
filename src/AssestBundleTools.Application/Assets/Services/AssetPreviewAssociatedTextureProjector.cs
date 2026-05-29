using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 将依赖 Texture2D 的有效替换载荷投影到引用型图片预览。
/// </summary>
internal static class AssetPreviewAssociatedTextureProjector
{
    public static async Task<AssetPreviewDto> ApplyIfAvailableAsync(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        GetAssetPreviewQuery query,
        BundleIndexAssetCacheEntryDto assetCacheEntry,
        AssetPreviewDto preview,
        HashSet<string> replacementPreviewStack,
        Func<GetAssetPreviewQuery, HashSet<string>, CancellationToken, Task<Result<AssetPreviewDto>>> loadPreviewAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(assetCacheEntry);
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(replacementPreviewStack);
        ArgumentNullException.ThrowIfNull(loadPreviewAsync);

        if (!IsSprite(assetCacheEntry.AssetType) ||
            !workspaceSessionRegistry.TryGetAssetDetails(
                query.WorkspaceId,
                query.BundleId,
                query.AssetId,
                out var details) ||
            details is null)
        {
            return preview;
        }

        var textureDependency = ResolvePrimaryTextureDependency(details);
        if (textureDependency is null ||
            !workspaceSessionRegistry.TryGetAssetDetails(
                query.WorkspaceId,
                textureDependency.BundleId,
                textureDependency.AssetId,
                out var textureDetails) ||
            textureDetails is null ||
            !HasEffectiveReplacement(textureDetails))
        {
            return preview;
        }

        var texturePreviewResult = await loadPreviewAsync(
            query with
            {
                BundleId = textureDependency.BundleId,
                AssetId = textureDependency.AssetId
            },
            replacementPreviewStack,
            cancellationToken);
        if (!texturePreviewResult.IsSuccess || texturePreviewResult.Value is null)
        {
            return preview;
        }

        var texturePreview = texturePreviewResult.Value;
        if (texturePreview.Kind != AssetPreviewKind.Image)
        {
            return preview;
        }

        return texturePreview with
        {
            WorkspaceId = query.WorkspaceId,
            BundleId = query.BundleId,
            AssetId = query.AssetId,
            PreviewInfoItems = preview.PreviewInfoItems,
            PreviewMessage = preview.PreviewMessage,
            Diagnostic = preview.Diagnostic,
            ImageOverlay = preview.ImageOverlay,
            ImageFocus = null
        };
    }

    private static AssetDependencyDto? ResolvePrimaryTextureDependency(AssetDetailsDto details)
    {
        return (details.TextureAssociations ?? Array.Empty<AssetDependencyDto>())
            .Concat(details.Dependencies ?? Array.Empty<AssetDependencyDto>())
            .FirstOrDefault(static dependency => dependency.AssetType == AssetType.Texture2D);
    }

    private static bool HasEffectiveReplacement(AssetDetailsDto details) =>
        !string.IsNullOrWhiteSpace(details.FileReplacementSourcePath) ||
        details.WorkspaceReplacementSourceBundleId is not null ||
        details.WorkspaceReplacementSourceAssetId is not null;

    private static bool IsSprite(string assetType) =>
        Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var parsedAssetType) &&
        parsedAssetType == AssetType.Sprite;
}
