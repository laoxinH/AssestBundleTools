using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class InspectorSpineTexturedTriangleRenderer
{
    public static bool Draw(
        CanvasDrawingSession drawingSession,
        CanvasBitmap texture,
        ICanvasImage image,
        IReadOnlyList<SpineAnimationMeshVertexDto> vertices,
        IReadOnlyList<SpineAnimationMeshTriangleDto> triangles,
        Matrix3x2 viewportTransform,
        float opacity)
    {
        if (triangles.Count == 0 ||
            vertices.Count == 0 ||
            !HasValidTriangle(triangles, vertices.Count))
        {
            return false;
        }

        var drawnTriangleCount = 0;
        var textureWidth = Math.Max(1f, texture.SizeInPixels.Width);
        var textureHeight = Math.Max(1f, texture.SizeInPixels.Height);
        foreach (var triangle in triangles)
        {
            if (!IsValid(triangle, vertices.Count))
            {
                continue;
            }

            var projectedTriangle = Project(triangle, vertices, textureWidth, textureHeight, viewportTransform);
            if (projectedTriangle is null)
            {
                continue;
            }

            DrawTriangle(drawingSession, texture, image, projectedTriangle.Value, opacity);
            drawnTriangleCount++;
        }

        return drawnTriangleCount > 0;
    }

    private static bool HasValidTriangle(
        IReadOnlyList<SpineAnimationMeshTriangleDto> triangles,
        int vertexCount)
    {
        foreach (var triangle in triangles)
        {
            if (IsValid(triangle, vertexCount))
            {
                return true;
            }
        }

        return false;
    }

    private static ProjectedTexturedTriangle? Project(
        SpineAnimationMeshTriangleDto triangle,
        IReadOnlyList<SpineAnimationMeshVertexDto> vertices,
        float textureWidth,
        float textureHeight,
        Matrix3x2 viewportTransform)
    {
        var vertexA = vertices[triangle.A];
        var vertexB = vertices[triangle.B];
        var vertexC = vertices[triangle.C];
        var screenA = ToScreenPoint(vertexA, viewportTransform);
        var screenB = ToScreenPoint(vertexB, viewportTransform);
        var screenC = ToScreenPoint(vertexC, viewportTransform);
        if (Math.Abs(Cross(screenB - screenA, screenC - screenA)) < 0.5f)
        {
            return null;
        }

        var textureA = ToTexturePoint(vertexA, textureWidth, textureHeight);
        var textureB = ToTexturePoint(vertexB, textureWidth, textureHeight);
        var textureC = ToTexturePoint(vertexC, textureWidth, textureHeight);
        var matrix = TryCreateTextureToScreenTransform(textureA, textureB, textureC, screenA, screenB, screenC);
        return matrix is null
            ? null
            : new ProjectedTexturedTriangle(screenA, screenB, screenC, matrix.Value);
    }

    private static void DrawTriangle(
        CanvasDrawingSession drawingSession,
        CanvasBitmap texture,
        ICanvasImage image,
        ProjectedTexturedTriangle triangle,
        float opacity)
    {
        using var pathBuilder = new CanvasPathBuilder(drawingSession);
        pathBuilder.BeginFigure(triangle.ScreenA);
        pathBuilder.AddLine(triangle.ScreenB);
        pathBuilder.AddLine(triangle.ScreenC);
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);
        using var clipGeometry = CanvasGeometry.CreatePath(pathBuilder);
        using var layer = drawingSession.CreateLayer(opacity, clipGeometry);

        var previousTransform = drawingSession.Transform;
        drawingSession.Transform = triangle.TextureToScreenTransform * previousTransform;
        drawingSession.DrawImage(
            image,
            Vector2.Zero,
            new Rect(0, 0, texture.SizeInPixels.Width, texture.SizeInPixels.Height),
            1f,
            CanvasImageInterpolation.Linear);
        drawingSession.Transform = previousTransform;
    }

    private static bool IsValid(SpineAnimationMeshTriangleDto triangle, int vertexCount) =>
        IsValidVertexIndex(triangle.A, vertexCount) &&
        IsValidVertexIndex(triangle.B, vertexCount) &&
        IsValidVertexIndex(triangle.C, vertexCount);

    private static bool IsValidVertexIndex(int index, int vertexCount) =>
        index >= 0 && index < vertexCount;

    private static Vector2 ToScreenPoint(
        SpineAnimationMeshVertexDto vertex,
        Matrix3x2 viewportTransform) =>
        Vector2.Transform(new Vector2((float)vertex.X, (float)vertex.Y), viewportTransform);

    private static Vector2 ToTexturePoint(
        SpineAnimationMeshVertexDto vertex,
        float textureWidth,
        float textureHeight) =>
        new((float)(vertex.U * textureWidth), (float)(vertex.V * textureHeight));

    private static Matrix3x2? TryCreateTextureToScreenTransform(
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
        Matrix3x2 TextureToScreenTransform);
}
