using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class InspectorMeshTexturedTriangleRenderer
{
    public static bool Draw(
        CanvasDrawingSession drawingSession,
        AssetPreviewMeshGeometryDto geometry,
        IReadOnlyList<InspectorMeshProjectedVertex> projectedVertices,
        CanvasBitmap? textureOverlayBitmap)
    {
        if (textureOverlayBitmap is null ||
            geometry.Triangles is not { Count: > 0 } triangles ||
            geometry.Uv0 is not { Count: > 0 } uvs)
        {
            return false;
        }

        var textureWidth = Math.Max(1f, textureOverlayBitmap.SizeInPixels.Width);
        var textureHeight = Math.Max(1f, textureOverlayBitmap.SizeInPixels.Height);
        var hasDrawnTriangle = false;
        foreach (var triangle in triangles
                     .Where(triangle => IsValid(triangle, projectedVertices.Count, uvs.Count))
                     .Select(triangle => Project(triangle, projectedVertices, uvs, textureWidth, textureHeight))
                     .Where(static triangle => triangle is not null)
                     .Cast<ProjectedTexturedTriangle>()
                     .OrderBy(static triangle => triangle.Depth))
        {
            DrawTriangle(drawingSession, textureOverlayBitmap, triangle);
            hasDrawnTriangle = true;
        }

        return hasDrawnTriangle;
    }

    private static ProjectedTexturedTriangle? Project(
        AssetPreviewMeshTriangleDto triangle,
        IReadOnlyList<InspectorMeshProjectedVertex> projectedVertices,
        IReadOnlyList<AssetPreviewMeshUvDto> uvs,
        float textureWidth,
        float textureHeight)
    {
        var screenA = projectedVertices[triangle.A].Screen;
        var screenB = projectedVertices[triangle.B].Screen;
        var screenC = projectedVertices[triangle.C].Screen;
        if (Math.Abs(Cross(screenB - screenA, screenC - screenA)) < 0.5f)
        {
            return null;
        }

        var uvA = ToTexturePoint(uvs[triangle.A], textureWidth, textureHeight);
        var uvB = ToTexturePoint(uvs[triangle.B], textureWidth, textureHeight);
        var uvC = ToTexturePoint(uvs[triangle.C], textureWidth, textureHeight);
        var matrix = TryCreateUvToScreenTransform(uvA, uvB, uvC, screenA, screenB, screenC);
        if (matrix is null)
        {
            return null;
        }

        return new ProjectedTexturedTriangle(
            screenA,
            screenB,
            screenC,
            matrix.Value,
            (projectedVertices[triangle.A].Depth + projectedVertices[triangle.B].Depth + projectedVertices[triangle.C].Depth) / 3d);
    }

    private static void DrawTriangle(
        CanvasDrawingSession drawingSession,
        CanvasBitmap textureOverlayBitmap,
        ProjectedTexturedTriangle triangle)
    {
        using var pathBuilder = new CanvasPathBuilder(drawingSession);
        pathBuilder.BeginFigure(triangle.ScreenA);
        pathBuilder.AddLine(triangle.ScreenB);
        pathBuilder.AddLine(triangle.ScreenC);
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);
        using var clipGeometry = CanvasGeometry.CreatePath(pathBuilder);
        using var layer = drawingSession.CreateLayer(0.92f, clipGeometry);

        var previousTransform = drawingSession.Transform;
        drawingSession.Transform = triangle.TextureToScreenTransform * previousTransform;
        drawingSession.DrawImage(
            textureOverlayBitmap,
            Vector2.Zero,
            new Rect(
                0,
                0,
                textureOverlayBitmap.SizeInPixels.Width,
                textureOverlayBitmap.SizeInPixels.Height),
            1f,
            CanvasImageInterpolation.Linear);
        drawingSession.Transform = previousTransform;
    }

    private static bool IsValid(
        AssetPreviewMeshTriangleDto triangle,
        int projectedVertexCount,
        int uvCount)
    {
        return triangle.A >= 0 &&
               triangle.B >= 0 &&
               triangle.C >= 0 &&
               triangle.A < projectedVertexCount &&
               triangle.B < projectedVertexCount &&
               triangle.C < projectedVertexCount &&
               triangle.A < uvCount &&
               triangle.B < uvCount &&
               triangle.C < uvCount;
    }

    private static Vector2 ToTexturePoint(AssetPreviewMeshUvDto uv, float textureWidth, float textureHeight) =>
        new(
            (float)(uv.U * textureWidth),
            (float)((1d - uv.V) * textureHeight));

    private static Matrix3x2? TryCreateUvToScreenTransform(
        Vector2 sourceA,
        Vector2 sourceB,
        Vector2 sourceC,
        Vector2 destinationA,
        Vector2 destinationB,
        Vector2 destinationC)
    {
        var sourceU = sourceB - sourceA;
        var sourceV = sourceC - sourceA;
        var determinant = Cross(sourceU, sourceV);
        if (Math.Abs(determinant) < 0.0001f)
        {
            return null;
        }

        var destinationU = destinationB - destinationA;
        var destinationV = destinationC - destinationA;
        var m11 = ((destinationU.X * sourceV.Y) - (destinationV.X * sourceU.Y)) / determinant;
        var m12 = ((destinationU.Y * sourceV.Y) - (destinationV.Y * sourceU.Y)) / determinant;
        var m21 = ((destinationV.X * sourceU.X) - (destinationU.X * sourceV.X)) / determinant;
        var m22 = ((destinationV.Y * sourceU.X) - (destinationU.Y * sourceV.X)) / determinant;
        var translation = destinationA - TransformPointNoTranslation(sourceA, m11, m12, m21, m22);
        return new Matrix3x2(m11, m12, m21, m22, translation.X, translation.Y);
    }

    private static Vector2 TransformPointNoTranslation(
        Vector2 point,
        float m11,
        float m12,
        float m21,
        float m22) =>
        new(
            (point.X * m11) + (point.Y * m21),
            (point.X * m12) + (point.Y * m22));

    private static float Cross(Vector2 a, Vector2 b) =>
        (a.X * b.Y) - (a.Y * b.X);

    private readonly record struct ProjectedTexturedTriangle(
        Vector2 ScreenA,
        Vector2 ScreenB,
        Vector2 ScreenC,
        Matrix3x2 TextureToScreenTransform,
        double Depth);
}
