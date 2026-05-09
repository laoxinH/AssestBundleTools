using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class InspectorSpineAnimationPreviewRenderer
{
    private const double MinExtent = 0.001d;

    public static void Draw(
        CanvasDrawingSession drawingSession,
        SpineAnimationFrameDto frame,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps,
        float width,
        float height)
    {
        DrawReferenceGrid(drawingSession, width, height);

        var drawItems = BuildDrawItems(frame, textureBitmaps);
        if (drawItems.Count == 0)
        {
            return;
        }

        var viewportTransform = CreateViewportTransform(drawItems, width, height);
        foreach (var item in drawItems)
        {
            if (item.HasMesh &&
                InspectorSpineTexturedTriangleRenderer.Draw(
                    drawingSession,
                    item.Texture,
                    item.Image,
                    item.MeshVertices,
                    item.MeshTriangles,
                    viewportTransform,
                    item.Opacity))
            {
                continue;
            }

            DrawRectangleItem(drawingSession, item, viewportTransform);
        }

        DrawOrigin(drawingSession, viewportTransform);
    }

    public static void DrawExport(
        CanvasDrawingSession drawingSession,
        SpineAnimationFrameDto frame,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps,
        SpineAnimationRuntimeFrameSequenceExportLayoutDto layout)
    {
        ArgumentNullException.ThrowIfNull(drawingSession);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(textureBitmaps);
        ArgumentNullException.ThrowIfNull(layout);

        var drawItems = BuildDrawItems(frame, textureBitmaps);
        if (drawItems.Count == 0)
        {
            return;
        }

        var viewportTransform = CreateExportTransform(layout);
        foreach (var item in drawItems)
        {
            if (item.HasMesh &&
                InspectorSpineTexturedTriangleRenderer.Draw(
                    drawingSession,
                    item.Texture,
                    item.Image,
                    item.MeshVertices,
                    item.MeshTriangles,
                    viewportTransform,
                    item.Opacity))
            {
                continue;
            }

            DrawRectangleItem(drawingSession, item, viewportTransform);
        }
    }

    private static IReadOnlyList<SpineDrawItem> BuildDrawItems(
        SpineAnimationFrameDto frame,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps)
    {
        if (frame.DrawCommands.Count == 0)
        {
            return Array.Empty<SpineDrawItem>();
        }

        var drawItems = new List<SpineDrawItem>(frame.DrawCommands.Count);
        foreach (var command in frame.DrawCommands)
        {
            if (!textureBitmaps.TryGetValue(command.PageName, out var texture))
            {
                continue;
            }

            drawItems.Add(new SpineDrawItem(
                texture,
                CreateTintedImage(texture, new SpineTint(
                    command.TintR,
                    command.TintG,
                    command.TintB,
                    command.TintA)),
                new Rect(command.SourceX, command.SourceY, command.SourceWidth, command.SourceHeight),
                new Matrix3x2(
                    (float)command.TransformM11,
                    (float)command.TransformM12,
                    (float)command.TransformM21,
                    (float)command.TransformM22,
                    (float)command.TransformM31,
                    (float)command.TransformM32),
                command.Width,
                command.Height,
                ClampUnit(command.TintA),
                command.MeshVertices,
                command.MeshTriangles));
        }

        return drawItems;
    }

    private static Matrix3x2 CreateViewportTransform(
        IReadOnlyList<SpineDrawItem> drawItems,
        float width,
        float height)
    {
        var bounds = ResolveBounds(drawItems);
        var extent = Math.Max(Math.Max(bounds.Width, bounds.Height), MinExtent);
        var scale = (Math.Min(width, height) * 0.72d) / extent;
        return Matrix3x2.CreateTranslation(
                   (float)(-bounds.CenterX),
                   (float)(-bounds.CenterY)) *
               Matrix3x2.CreateScale((float)scale, (float)-scale) *
               Matrix3x2.CreateTranslation(width / 2f, height / 2f);
    }

    private static Matrix3x2 CreateExportTransform(
        SpineAnimationRuntimeFrameSequenceExportLayoutDto layout) =>
        new(
            (float)layout.WorldToPixelScale,
            0f,
            0f,
            (float)-layout.WorldToPixelScale,
            (float)layout.WorldToPixelOffsetX,
            (float)layout.WorldToPixelOffsetY);

    private static SpineBounds ResolveBounds(IReadOnlyList<SpineDrawItem> drawItems)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var item in drawItems)
        {
            foreach (var point in EnumerateCorners(item))
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
            !double.IsFinite(maxX) || !double.IsFinite(maxY))
        {
            return new SpineBounds(0d, 0d, 1d, 1d);
        }

        return new SpineBounds(
            (minX + maxX) / 2d,
            (minY + maxY) / 2d,
            Math.Max(maxX - minX, 1d),
            Math.Max(maxY - minY, 1d));
    }

    private static IEnumerable<Vector2> EnumerateCorners(SpineDrawItem item)
    {
        if (item.MeshVertices.Count > 0)
        {
            foreach (var vertex in item.MeshVertices)
            {
                yield return new Vector2((float)vertex.X, (float)vertex.Y);
            }

            yield break;
        }

        var halfWidth = (float)(item.Width / 2d);
        var halfHeight = (float)(item.Height / 2d);
        yield return Vector2.Transform(new Vector2(-halfWidth, -halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(halfWidth, -halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(halfWidth, halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(-halfWidth, halfHeight), item.Transform);
    }

    private static void DrawRectangleItem(
        CanvasDrawingSession drawingSession,
        SpineDrawItem item,
        Matrix3x2 viewportTransform)
    {
        var previousTransform = drawingSession.Transform;
        drawingSession.Transform = item.Transform * viewportTransform;
        drawingSession.DrawImage(
            item.Image,
            new Rect(-item.Width / 2d, -item.Height / 2d, item.Width, item.Height),
            item.SourceRect,
            item.Opacity,
            CanvasImageInterpolation.Linear);
        drawingSession.Transform = previousTransform;
    }

    private static ICanvasImage CreateTintedImage(CanvasBitmap texture, SpineTint tint)
    {
        if (!tint.HasRgbTint)
        {
            return texture;
        }

        return new ColorMatrixEffect
        {
            Source = texture,
            ColorMatrix = new Matrix5x4
            {
                M11 = ClampUnit(tint.R),
                M22 = ClampUnit(tint.G),
                M33 = ClampUnit(tint.B),
                M44 = 1f
            }
        };
    }

    private static float ClampUnit(double value) =>
        (float)Math.Clamp(value, 0d, 1d);

    private static void DrawReferenceGrid(CanvasDrawingSession drawingSession, float width, float height)
    {
        var minorLineColor = Color.FromArgb(14, 100, 116, 139);
        const float gridStep = 24f;
        for (var x = width / 2f % gridStep; x < width; x += gridStep)
        {
            drawingSession.DrawLine(x, 0f, x, height, minorLineColor, 1f);
        }

        for (var y = height / 2f % gridStep; y < height; y += gridStep)
        {
            drawingSession.DrawLine(0f, y, width, y, minorLineColor, 1f);
        }
    }

    private static void DrawOrigin(CanvasDrawingSession drawingSession, Matrix3x2 viewportTransform)
    {
        var origin = Vector2.Transform(Vector2.Zero, viewportTransform);
        drawingSession.FillCircle(origin, 3.2f, Color.FromArgb(220, 59, 130, 246));
    }

    private sealed record SpineDrawItem(
        CanvasBitmap Texture,
        ICanvasImage Image,
        Rect SourceRect,
        Matrix3x2 Transform,
        double Width,
        double Height,
        float Opacity,
        IReadOnlyList<SpineAnimationMeshVertexDto> MeshVertices,
        IReadOnlyList<SpineAnimationMeshTriangleDto> MeshTriangles)
    {
        public bool HasMesh => MeshVertices.Count > 0 && MeshTriangles.Count > 0;
    }

    private readonly record struct SpineBounds(
        double CenterX,
        double CenterY,
        double Width,
        double Height);

    private readonly record struct SpineTint(
        double R,
        double G,
        double B,
        double A)
    {
        public bool HasRgbTint =>
            Math.Abs(R - 1d) > 0.0001d ||
            Math.Abs(G - 1d) > 0.0001d ||
            Math.Abs(B - 1d) > 0.0001d;
    }
}
