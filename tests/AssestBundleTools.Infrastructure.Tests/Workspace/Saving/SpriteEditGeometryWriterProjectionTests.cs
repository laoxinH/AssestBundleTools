using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static class SpriteEditGeometryWriterProjectionTests
{
    public static void Create_ShouldConvertTopOriginGeometry_ToUnitySpriteFields()
    {
        var projection = SpriteEditGeometryWriterProjection.Create(
            new WorkspaceSpriteEditGeometryDto(
                CanvasWidth: 256,
                CanvasHeight: 128,
                RegionX: 8,
                RegionY: 12,
                RegionWidth: 64,
                RegionHeight: 32,
                PivotX: 40,
                PivotY: 28),
            useNormalizedPivot: false);

        AssertClose(8, projection.RectX, "Rect x should keep the source image x coordinate.");
        AssertClose(84, projection.RectY, "Rect y should convert from top-left UI coordinates to Unity bottom-left coordinates.");
        AssertClose(64, projection.RectWidth, "Rect width should keep the edited crop width.");
        AssertClose(32, projection.RectHeight, "Rect height should keep the edited crop height.");
        AssertClose(32, projection.PivotX, "Pixel pivot x should be local to the cropped Sprite rect.");
        AssertClose(16, projection.PivotY, "Pixel pivot y should be local to the cropped Sprite rect and use Unity bottom-left coordinates.");
    }

    public static void Create_ShouldNormalizePivot_WhenTargetSpriteUsesNormalizedPivot()
    {
        var projection = SpriteEditGeometryWriterProjection.Create(
            new WorkspaceSpriteEditGeometryDto(
                CanvasWidth: 256,
                CanvasHeight: 128,
                RegionX: 8,
                RegionY: 12,
                RegionWidth: 64,
                RegionHeight: 32,
                PivotX: 40,
                PivotY: 28),
            useNormalizedPivot: true);

        AssertClose(0.5, projection.PivotX, "Normalized pivot x should use local pivot divided by crop width.");
        AssertClose(0.5, projection.PivotY, "Normalized pivot y should use local pivot divided by crop height.");
    }

    private static void AssertClose(double expected, double actual, string message)
    {
        if (Math.Abs(expected - actual) > 0.001d)
        {
            throw new InvalidOperationException($"{message} Expected: {expected:0.###}. Actual: {actual:0.###}.");
        }
    }
}
