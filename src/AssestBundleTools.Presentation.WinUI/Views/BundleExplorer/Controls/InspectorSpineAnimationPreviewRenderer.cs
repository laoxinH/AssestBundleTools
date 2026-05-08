using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class InspectorSpineAnimationPreviewRenderer
{
    private const double MinExtent = 0.001d;

    public static void Draw(
        CanvasDrawingSession drawingSession,
        SpineAnimationClipDto animation,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps,
        float width,
        float height,
        double time)
    {
        DrawReferenceGrid(drawingSession, width, height);

        var drawItems = BuildDrawItems(animation.SampledFrames, time, textureBitmaps);
        if (drawItems.Count == 0)
        {
            return;
        }

        var viewportTransform = CreateViewportTransform(drawItems, width, height);
        foreach (var item in drawItems)
        {
            var previousTransform = drawingSession.Transform;
            drawingSession.Transform = item.Transform * viewportTransform;
            drawingSession.DrawImage(
                item.Texture,
                new Rect(-item.Width / 2d, -item.Height / 2d, item.Width, item.Height),
                item.SourceRect,
                1f,
                CanvasImageInterpolation.Linear);
            drawingSession.Transform = previousTransform;
        }

        DrawOrigin(drawingSession, viewportTransform);
    }

    private static IReadOnlyList<SpineDrawItem> BuildDrawItems(
        IReadOnlyList<SpineAnimationFrameDto> frames,
        double time,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps)
    {
        var frame = ResolveSampledFrame(frames, time);
        if (frame is null || frame.DrawCommands.Count == 0)
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
                new Rect(command.SourceX, command.SourceY, command.SourceWidth, command.SourceHeight),
                new Matrix3x2(
                    (float)command.TransformM11,
                    (float)command.TransformM12,
                    (float)command.TransformM21,
                    (float)command.TransformM22,
                    (float)command.TransformM31,
                    (float)command.TransformM32),
                command.Width,
                command.Height));
        }

        return drawItems;
    }

    private static SpineAnimationFrameDto? ResolveSampledFrame(
        IReadOnlyList<SpineAnimationFrameDto> frames,
        double time)
    {
        if (frames.Count == 0)
        {
            return null;
        }

        if (frames.Count == 1 || time <= frames[0].Time)
        {
            return frames[0];
        }

        for (var index = 1; index < frames.Count; index++)
        {
            if (time < frames[index].Time)
            {
                return frames[index - 1];
            }
        }

        return frames[^1];
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
        var halfWidth = (float)(item.Width / 2d);
        var halfHeight = (float)(item.Height / 2d);
        yield return Vector2.Transform(new Vector2(-halfWidth, -halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(halfWidth, -halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(halfWidth, halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(-halfWidth, halfHeight), item.Transform);
    }

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
        Rect SourceRect,
        Matrix3x2 Transform,
        double Width,
        double Height);

    private readonly record struct SpineBounds(
        double CenterX,
        double CenterY,
        double Width,
        double Height);
}
