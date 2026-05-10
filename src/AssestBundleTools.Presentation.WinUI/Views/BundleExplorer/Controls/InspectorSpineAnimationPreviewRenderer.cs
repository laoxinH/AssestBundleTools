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
    private const double ViewportFitRatio = 0.72d;
    private const float VisibleOpacityThreshold = 0.0001f;

    public static void Draw(
        CanvasDrawingSession drawingSession,
        SpineAnimationFrameDto frame,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePageSizes,
        float width,
        float height,
        SpineAnimationPreviewViewport viewport,
        SpineAnimationAutoFitBounds autoFitBounds = default)
    {
        DrawReferenceGrid(drawingSession, width, height);

        var drawItems = BuildDrawItems(frame, textureBitmaps);
        if (drawItems.Count == 0)
        {
            return;
        }

        var viewportTransform = CreateViewportTransform(
            drawItems,
            texturePageSizes,
            width,
            height,
            viewport,
            autoFitBounds);
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
                command.PageName,
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
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePageSizes,
        float width,
        float height,
        SpineAnimationPreviewViewport viewport,
        SpineAnimationAutoFitBounds autoFitBounds)
    {
        var normalizedViewport = viewport.Normalize();
        var bounds = autoFitBounds.IsValid
            ? CreateBounds(autoFitBounds)
            : ResolveBounds(drawItems, texturePageSizes);
        var scale = Math.Min(
            width / Math.Max(bounds.FitWidth, MinExtent),
            height / Math.Max(bounds.FitHeight, MinExtent)) * ViewportFitRatio;
        var viewportCenterX = width / 2f;
        var viewportCenterY = height / 2f;
        return Matrix3x2.CreateTranslation(
                   (float)(-bounds.CenterX),
                   (float)(-bounds.CenterY)) *
               Matrix3x2.CreateScale((float)scale, (float)-scale) *
               Matrix3x2.CreateTranslation(viewportCenterX, viewportCenterY) *
               Matrix3x2.CreateTranslation(-viewportCenterX, -viewportCenterY) *
               Matrix3x2.CreateScale((float)normalizedViewport.Zoom) *
               Matrix3x2.CreateTranslation(
                   viewportCenterX + (float)normalizedViewport.PanX,
                   viewportCenterY + (float)normalizedViewport.PanY);
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

    private static SpineBounds ResolveBounds(
        IReadOnlyList<SpineDrawItem> drawItems,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePageSizes)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        var hasVisibleItems = false;
        foreach (var item in drawItems)
        {
            if (!IsVisibleForAutoFit(item))
            {
                continue;
            }

            var contribution = ResolveVisibleContribution(item, texturePageSizes);
            foreach (var point in contribution.BoundsPoints)
            {
                hasVisibleItems = true;
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        if (!hasVisibleItems)
        {
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
        }

        if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
            !double.IsFinite(maxX) || !double.IsFinite(maxY))
        {
            return new SpineBounds(0d, 0d, 1d, 1d, 1d, 1d);
        }

        var centerX = (minX + maxX) / 2d;
        var centerY = (minY + maxY) / 2d;
        return new SpineBounds(
            centerX,
            centerY,
            Math.Max(maxX - minX, 1d),
            Math.Max(maxY - minY, 1d),
            Math.Max(Math.Max(maxX - centerX, centerX - minX) * 2d, 1d),
            Math.Max(Math.Max(maxY - centerY, centerY - minY) * 2d, 1d));
    }

    private static SpineBounds CreateBounds(SpineAnimationAutoFitBounds autoFitBounds) =>
        autoFitBounds.IsValid
            ? new SpineBounds(
                autoFitBounds.CenterX,
                autoFitBounds.CenterY,
                autoFitBounds.Width,
                autoFitBounds.Height,
                autoFitBounds.Width,
                autoFitBounds.Height)
            : new SpineBounds(0d, 0d, 1d, 1d, 1d, 1d);

    private static bool IsVisibleForAutoFit(SpineDrawItem item) =>
        item.Opacity > VisibleOpacityThreshold;

    private static SpineDrawAutoFitContribution ResolveVisibleContribution(
        SpineDrawItem item,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePageSizes)
    {
        if (!texturePageSizes.TryGetValue(item.PageName, out var pageSize))
        {
            return new SpineDrawAutoFitContribution(EnumerateCorners(item).ToArray());
        }

        if (item.HasMesh)
        {
            return ResolveVisibleMeshContribution(item, pageSize);
        }

        var sourceX = (int)Math.Round(item.SourceRect.X);
        var sourceY = (int)Math.Round(item.SourceRect.Y);
        var sourceWidth = (int)Math.Round(item.SourceRect.Width);
        var sourceHeight = (int)Math.Round(item.SourceRect.Height);
        if (pageSize.AlphaMap?.TryResolveVisibleBounds(
                sourceX,
                sourceY,
                sourceWidth,
                sourceHeight,
                out var sourceBounds) != true)
        {
            return pageSize.AlphaMap is null
                ? new SpineDrawAutoFitContribution(EnumerateCorners(item).ToArray())
                : SpineDrawAutoFitContribution.Empty;
        }

        return new SpineDrawAutoFitContribution(
            EnumerateVisibleRegionCorners(item, sourceBounds, sourceX, sourceY, sourceWidth, sourceHeight).ToArray());
    }

    private static SpineDrawAutoFitContribution ResolveVisibleMeshContribution(
        SpineDrawItem item,
        SpineAnimationTexturePageSize pageSize)
    {
        if (pageSize.AlphaMap is null)
        {
            return new SpineDrawAutoFitContribution(EnumerateCorners(item).ToArray());
        }

        var points = new List<Vector2>();
        foreach (var triangle in item.MeshTriangles)
        {
            if (!IsValid(triangle, item.MeshVertices.Count))
            {
                continue;
            }

            var contribution = SpineAnimationMeshAutoFitBoundsResolver.ResolveTriangleContribution(
                item.MeshVertices,
                triangle,
                pageSize);
            points.AddRange(contribution.BoundsPoints);
        }

        return new SpineDrawAutoFitContribution(points);
    }

    private static IEnumerable<Vector2> EnumerateVisibleRegionCorners(
        SpineDrawItem item,
        SpineAnimationTextureSourceAlphaBounds sourceBounds,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight)
    {
        var left = ResolveLocalAxis(sourceBounds.Left, sourceX, sourceWidth, item.Width);
        var right = ResolveLocalAxis(sourceBounds.Right, sourceX, sourceWidth, item.Width);
        var top = ResolveLocalAxis(sourceBounds.Top, sourceY, sourceHeight, item.Height);
        var bottom = ResolveLocalAxis(sourceBounds.Bottom, sourceY, sourceHeight, item.Height);
        yield return Vector2.Transform(new Vector2(left, top), item.Transform);
        yield return Vector2.Transform(new Vector2(right, top), item.Transform);
        yield return Vector2.Transform(new Vector2(right, bottom), item.Transform);
        yield return Vector2.Transform(new Vector2(left, bottom), item.Transform);
    }

    private static float ResolveLocalAxis(
        int pixel,
        int sourceStart,
        int sourceLength,
        double worldLength) =>
        ResolveLocalAxis((double)pixel, sourceStart, sourceLength, worldLength);

    private static float ResolveLocalAxis(
        double pixel,
        int sourceStart,
        int sourceLength,
        double worldLength)
    {
        if (sourceLength <= 0 || worldLength <= 0d)
        {
            return 0f;
        }

        var ratio = (pixel - sourceStart) / (double)sourceLength;
        return (float)((ratio * worldLength) - (worldLength / 2d));
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

    private static bool IsValid(SpineAnimationMeshTriangleDto triangle, int vertexCount) =>
        IsValidVertexIndex(triangle.A, vertexCount) &&
        IsValidVertexIndex(triangle.B, vertexCount) &&
        IsValidVertexIndex(triangle.C, vertexCount);

    private static bool IsValidVertexIndex(int index, int vertexCount) =>
        index >= 0 && index < vertexCount;

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

}
