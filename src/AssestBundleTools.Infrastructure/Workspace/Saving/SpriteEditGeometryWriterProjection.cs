using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

/// <summary>
/// 将 UI 使用的源图 top-left 坐标投影为 Unity Sprite 写包字段。
/// </summary>
internal static class SpriteEditGeometryWriterProjection
{
    public static SpriteEditGeometryWriterProjectionDto Create(
        WorkspaceSpriteEditGeometryDto geometry,
        bool useNormalizedPivot)
    {
        var normalized = Normalize(geometry);
        var unityY = Math.Max(0d, normalized.CanvasHeight - normalized.RegionY - normalized.RegionHeight);
        var localPivotX = normalized.PivotX - normalized.RegionX;
        var localPivotY = normalized.CanvasHeight - normalized.PivotY - unityY;

        if (useNormalizedPivot)
        {
            localPivotX = normalized.RegionWidth <= 0d ? 0d : localPivotX / normalized.RegionWidth;
            localPivotY = normalized.RegionHeight <= 0d ? 0d : localPivotY / normalized.RegionHeight;
        }

        return new SpriteEditGeometryWriterProjectionDto(
            RectX: normalized.RegionX,
            RectY: unityY,
            RectWidth: normalized.RegionWidth,
            RectHeight: normalized.RegionHeight,
            PivotX: localPivotX,
            PivotY: localPivotY);
    }

    private static WorkspaceSpriteEditGeometryDto Normalize(
        WorkspaceSpriteEditGeometryDto geometry)
    {
        var canvasWidth = NormalizePositive(geometry.CanvasWidth, fallback: 1d);
        var canvasHeight = NormalizePositive(geometry.CanvasHeight, fallback: 1d);
        var regionWidth = Math.Min(
            NormalizePositive(geometry.RegionWidth, fallback: 1d),
            canvasWidth);
        var regionHeight = Math.Min(
            NormalizePositive(geometry.RegionHeight, fallback: 1d),
            canvasHeight);
        var regionX = Math.Clamp(
            NormalizeFinite(geometry.RegionX, fallback: 0d),
            0d,
            Math.Max(0d, canvasWidth - regionWidth));
        var regionY = Math.Clamp(
            NormalizeFinite(geometry.RegionY, fallback: 0d),
            0d,
            Math.Max(0d, canvasHeight - regionHeight));

        return new WorkspaceSpriteEditGeometryDto(
            canvasWidth,
            canvasHeight,
            regionX,
            regionY,
            regionWidth,
            regionHeight,
            Math.Clamp(
                NormalizeFinite(geometry.PivotX, regionX + (regionWidth / 2d)),
                regionX,
                regionX + regionWidth),
            Math.Clamp(
                NormalizeFinite(geometry.PivotY, regionY + (regionHeight / 2d)),
                regionY,
                regionY + regionHeight));
    }

    private static double NormalizeFinite(double value, double fallback) =>
        double.IsFinite(value) ? value : fallback;

    private static double NormalizePositive(double value, double fallback) =>
        double.IsFinite(value) && value > 0d
            ? value
            : fallback;
}

internal sealed record SpriteEditGeometryWriterProjectionDto(
    double RectX,
    double RectY,
    double RectWidth,
    double RectHeight,
    double PivotX,
    double PivotY);
