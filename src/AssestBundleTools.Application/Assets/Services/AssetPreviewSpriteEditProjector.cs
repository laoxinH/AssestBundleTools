using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 将工作区中已应用的 Sprite 编辑几何投影到预览覆盖层。
/// </summary>
internal static class AssetPreviewSpriteEditProjector
{
    public static AssetPreviewDto ApplyIfAvailable(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        GetAssetPreviewQuery query,
        BundleIndexAssetCacheEntryDto assetCacheEntry,
        AssetPreviewDto preview)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(assetCacheEntry);
        ArgumentNullException.ThrowIfNull(preview);

        if (!IsSprite(assetCacheEntry.AssetType) ||
            !workspaceSessionRegistry.TryGetAssetDetails(
                query.WorkspaceId,
                query.BundleId,
                query.AssetId,
                out var details) ||
            details?.SpriteEditGeometry is not { } geometry)
        {
            return preview;
        }

        return preview with
        {
            ImageOverlay = new AssetPreviewImageOverlayDto(
                CanvasPixelWidth: (int)Math.Round(Math.Max(1d, geometry.CanvasWidth), MidpointRounding.AwayFromZero),
                CanvasPixelHeight: (int)Math.Round(Math.Max(1d, geometry.CanvasHeight), MidpointRounding.AwayFromZero),
                HighlightRegion: new AssetPreviewImageRegionDto(
                    geometry.RegionX,
                    geometry.RegionY,
                    geometry.RegionWidth,
                    geometry.RegionHeight),
                PivotPoint: new AssetPreviewImagePointDto(geometry.PivotX, geometry.PivotY),
                NineSliceBorder: preview.ImageOverlay?.NineSliceBorder),
            ImageFocus = null
        };
    }

    private static bool IsSprite(string assetType) =>
        Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var parsedAssetType) &&
        parsedAssetType == AssetType.Sprite;
}
