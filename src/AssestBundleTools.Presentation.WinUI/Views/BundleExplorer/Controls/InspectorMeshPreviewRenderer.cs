using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class InspectorMeshPreviewRenderer
{
    private const double DegreesToRadians = Math.PI / 180d;
    private const double MinMeshExtent = 0.000001d;
    private const double MeshViewportPaddingRatio = 0.42d;
    private const double UvViewportPaddingRatio = 0.62d;
    private const int MaxVertexDotCount = 2000;

    public static void DrawWireframe(
        CanvasDrawingSession drawingSession,
        AssetPreviewMeshGeometryDto geometry,
        float width,
        float height,
        double rotationX,
        double rotationY,
        double zoom,
        CanvasBitmap? textureOverlayBitmap = null,
        bool drawMeshGrid = true)
    {
        DrawReferenceGrid(drawingSession, width, height);

        var projection = MeshProjection.Create(
            geometry,
            width,
            height,
            rotationX,
            rotationY,
            zoom);
        if (projection.Points.Count == 0)
        {
            return;
        }

        var shouldDrawStructure = drawMeshGrid || textureOverlayBitmap is null;
        if (shouldDrawStructure)
        {
            DrawAxes(drawingSession, projection);
        }

        var hasDrawnTexture = InspectorMeshTexturedTriangleRenderer.Draw(
            drawingSession,
            geometry,
            projection.Points,
            textureOverlayBitmap);
        if (!shouldDrawStructure && !hasDrawnTexture)
        {
            DrawAxes(drawingSession, projection);
            shouldDrawStructure = true;
        }

        if (shouldDrawStructure)
        {
            DrawEdges(drawingSession, geometry, projection);
            DrawVertices(drawingSession, projection);
        }
    }

    public static void DrawUv(
        CanvasDrawingSession drawingSession,
        AssetPreviewMeshGeometryDto geometry,
        float width,
        float height,
        double zoom,
        CanvasBitmap? textureOverlayBitmap,
        bool drawMeshGrid = true)
    {
        var uvs = geometry.Uv0;
        if (uvs is not { Count: > 0 })
        {
            DrawWireframe(drawingSession, geometry, width, height, 0d, 0d, zoom);
            return;
        }

        DrawReferenceGrid(drawingSession, width, height);

        var projection = MeshUvProjection.Create(uvs, width, height, zoom);
        var hasDrawnTexture = DrawUvTextureOverlay(drawingSession, projection, textureOverlayBitmap);
        var shouldDrawStructure = drawMeshGrid || !hasDrawnTexture;
        if (shouldDrawStructure)
        {
            DrawUvUnitGrid(drawingSession, projection);
            DrawUvEdges(drawingSession, geometry, projection);
            DrawUvVertices(drawingSession, projection);
        }
    }

    private static bool DrawUvTextureOverlay(
        CanvasDrawingSession drawingSession,
        MeshUvProjection projection,
        CanvasBitmap? textureOverlayBitmap)
    {
        if (textureOverlayBitmap is null)
        {
            return false;
        }

        var topLeft = projection.Project(new AssetPreviewMeshUvDto(0d, 1d));
        var bottomRight = projection.Project(new AssetPreviewMeshUvDto(1d, 0d));
        var destinationRect = new Rect(
            topLeft.X,
            topLeft.Y,
            Math.Max(1d, bottomRight.X - topLeft.X),
            Math.Max(1d, bottomRight.Y - topLeft.Y));
        var sourceRect = new Rect(
            0d,
            0d,
            textureOverlayBitmap.SizeInPixels.Width,
            textureOverlayBitmap.SizeInPixels.Height);

        drawingSession.DrawImage(
            textureOverlayBitmap,
            destinationRect,
            sourceRect,
            0.72f,
            CanvasImageInterpolation.Linear);
        return true;
    }

    private static void DrawReferenceGrid(CanvasDrawingSession drawingSession, float width, float height)
    {
        var majorLineColor = Color.FromArgb(30, 100, 116, 139);
        var minorLineColor = Color.FromArgb(16, 100, 116, 139);
        const float gridStep = 24f;
        var centerX = width / 2f;
        var centerY = height / 2f;

        for (var x = centerX % gridStep; x < width; x += gridStep)
        {
            drawingSession.DrawLine(x, 0f, x, height, minorLineColor, 1f);
        }

        for (var y = centerY % gridStep; y < height; y += gridStep)
        {
            drawingSession.DrawLine(0f, y, width, y, minorLineColor, 1f);
        }

        drawingSession.DrawLine(centerX, 0f, centerX, height, majorLineColor, 1f);
        drawingSession.DrawLine(0f, centerY, width, centerY, majorLineColor, 1f);
    }

    private static void DrawAxes(CanvasDrawingSession drawingSession, MeshProjection projection)
    {
        var axisLength = projection.MeshExtent * 0.72d;
        var center = projection.Transform.Center;
        var origin = projection.Project(center);
        var xAxis = projection.Project(new AssetPreviewMeshVertexDto(center.X + axisLength, center.Y, center.Z));
        var yAxis = projection.Project(new AssetPreviewMeshVertexDto(center.X, center.Y + axisLength, center.Z));
        var zAxis = projection.Project(new AssetPreviewMeshVertexDto(center.X, center.Y, center.Z + axisLength));

        drawingSession.DrawLine(origin.Screen, xAxis.Screen, Color.FromArgb(130, 239, 68, 68), 1.5f);
        drawingSession.DrawLine(origin.Screen, yAxis.Screen, Color.FromArgb(130, 34, 197, 94), 1.5f);
        drawingSession.DrawLine(origin.Screen, zAxis.Screen, Color.FromArgb(130, 59, 130, 246), 1.5f);
    }

    private static void DrawEdges(
        CanvasDrawingSession drawingSession,
        AssetPreviewMeshGeometryDto geometry,
        MeshProjection projection)
    {
        if (geometry.Edges.Count == 0)
        {
            return;
        }

        foreach (var edge in geometry.Edges
                     .Where(edge => IsProjectedEdgeValid(edge, projection.Points.Count))
                     .OrderBy(edge => projection.Points[edge.A].Depth + projection.Points[edge.B].Depth))
        {
            var start = projection.Points[edge.A];
            var end = projection.Points[edge.B];
            drawingSession.DrawLine(start.Screen, end.Screen, Color.FromArgb(70, 15, 23, 42), 3f);
            drawingSession.DrawLine(start.Screen, end.Screen, Color.FromArgb(225, 37, 99, 235), 1.2f);
        }
    }

    private static void DrawVertices(CanvasDrawingSession drawingSession, MeshProjection projection)
    {
        if (projection.Points.Count > MaxVertexDotCount)
        {
            return;
        }

        foreach (var vertex in projection.Points)
        {
            drawingSession.FillCircle(vertex.Screen, 2.1f, Color.FromArgb(205, 248, 250, 252));
        }
    }

    private static void DrawUvUnitGrid(CanvasDrawingSession drawingSession, MeshUvProjection projection)
    {
        var bottomLeft = projection.Project(new AssetPreviewMeshUvDto(0d, 0d));
        var bottomRight = projection.Project(new AssetPreviewMeshUvDto(1d, 0d));
        var topRight = projection.Project(new AssetPreviewMeshUvDto(1d, 1d));
        var topLeft = projection.Project(new AssetPreviewMeshUvDto(0d, 1d));
        var gridColor = Color.FromArgb(70, 20, 184, 166);
        var frameColor = Color.FromArgb(190, 15, 118, 110);

        for (var step = 1; step < 4; step++)
        {
            var value = step / 4d;
            drawingSession.DrawLine(
                projection.Project(new AssetPreviewMeshUvDto(value, 0d)),
                projection.Project(new AssetPreviewMeshUvDto(value, 1d)),
                gridColor,
                1f);
            drawingSession.DrawLine(
                projection.Project(new AssetPreviewMeshUvDto(0d, value)),
                projection.Project(new AssetPreviewMeshUvDto(1d, value)),
                gridColor,
                1f);
        }

        drawingSession.DrawLine(bottomLeft, bottomRight, frameColor, 1.4f);
        drawingSession.DrawLine(bottomRight, topRight, frameColor, 1.4f);
        drawingSession.DrawLine(topRight, topLeft, frameColor, 1.4f);
        drawingSession.DrawLine(topLeft, bottomLeft, frameColor, 1.4f);
    }

    private static void DrawUvEdges(
        CanvasDrawingSession drawingSession,
        AssetPreviewMeshGeometryDto geometry,
        MeshUvProjection projection)
    {
        if (geometry.Edges.Count == 0)
        {
            return;
        }

        foreach (var edge in geometry.Edges.Where(edge => IsProjectedEdgeValid(edge, projection.Points.Count)))
        {
            var start = projection.Points[edge.A];
            var end = projection.Points[edge.B];
            drawingSession.DrawLine(start, end, Color.FromArgb(80, 15, 23, 42), 3f);
            drawingSession.DrawLine(start, end, Color.FromArgb(230, 234, 88, 12), 1.2f);
        }
    }

    private static void DrawUvVertices(CanvasDrawingSession drawingSession, MeshUvProjection projection)
    {
        if (projection.Points.Count > MaxVertexDotCount)
        {
            return;
        }

        foreach (var point in projection.Points)
        {
            drawingSession.FillCircle(point, 2.1f, Color.FromArgb(215, 255, 247, 237));
        }
    }

    private static bool IsProjectedEdgeValid(AssetPreviewMeshEdgeDto edge, int projectedVertexCount)
    {
        return edge.A >= 0 &&
               edge.B >= 0 &&
               edge.A < projectedVertexCount &&
               edge.B < projectedVertexCount;
    }

    private sealed class MeshProjection
    {
        private MeshProjection(
            IReadOnlyList<InspectorMeshProjectedVertex> points,
            ProjectionTransform transform,
            double meshExtent)
        {
            Points = points;
            Transform = transform;
            MeshExtent = meshExtent;
        }

        public IReadOnlyList<InspectorMeshProjectedVertex> Points { get; }

        public ProjectionTransform Transform { get; }

        public double MeshExtent { get; }

        public static MeshProjection Create(
            AssetPreviewMeshGeometryDto geometry,
            float viewportWidth,
            float viewportHeight,
            double rotationX,
            double rotationY,
            double zoom)
        {
            var bounds = ResolveBounds(geometry);
            var meshExtent = ResolveMeshExtent(bounds, geometry.Vertices);
            var scale = (Math.Min(viewportWidth, viewportHeight) * MeshViewportPaddingRatio / meshExtent) *
                        Math.Max(0.01d, zoom);
            var transform = new ProjectionTransform(
                bounds.Center,
                viewportWidth / 2d,
                viewportHeight / 2d,
                scale,
                rotationX * DegreesToRadians,
                rotationY * DegreesToRadians,
                Math.Max(meshExtent * 5d, 1d));

            var projectedPoints = geometry.Vertices
                .Select(transform.Project)
                .ToArray();
            return new MeshProjection(projectedPoints, transform, meshExtent);
        }

        public InspectorMeshProjectedVertex Project(AssetPreviewMeshVertexDto vertex) => Transform.Project(vertex);

        private static AssetPreviewMeshBoundsDto ResolveBounds(AssetPreviewMeshGeometryDto geometry)
        {
            if (geometry.Bounds is not null)
            {
                return geometry.Bounds;
            }

            if (geometry.Vertices.Count == 0)
            {
                return new AssetPreviewMeshBoundsDto(
                    new AssetPreviewMeshVertexDto(0d, 0d, 0d),
                    new AssetPreviewMeshVertexDto(1d, 1d, 1d));
            }

            var minX = geometry.Vertices[0].X;
            var minY = geometry.Vertices[0].Y;
            var minZ = geometry.Vertices[0].Z;
            var maxX = minX;
            var maxY = minY;
            var maxZ = minZ;

            foreach (var vertex in geometry.Vertices)
            {
                minX = Math.Min(minX, vertex.X);
                minY = Math.Min(minY, vertex.Y);
                minZ = Math.Min(minZ, vertex.Z);
                maxX = Math.Max(maxX, vertex.X);
                maxY = Math.Max(maxY, vertex.Y);
                maxZ = Math.Max(maxZ, vertex.Z);
            }

            return new AssetPreviewMeshBoundsDto(
                new AssetPreviewMeshVertexDto(
                    (minX + maxX) / 2d,
                    (minY + maxY) / 2d,
                    (minZ + maxZ) / 2d),
                new AssetPreviewMeshVertexDto(
                    Math.Max((maxX - minX) / 2d, MinMeshExtent),
                    Math.Max((maxY - minY) / 2d, MinMeshExtent),
                    Math.Max((maxZ - minZ) / 2d, MinMeshExtent)));
        }

        private static double ResolveMeshExtent(
            AssetPreviewMeshBoundsDto bounds,
            IReadOnlyList<AssetPreviewMeshVertexDto> vertices)
        {
            var boundsExtent = Math.Max(
                Math.Max(Math.Abs(bounds.Extents.X), Math.Abs(bounds.Extents.Y)),
                Math.Abs(bounds.Extents.Z));
            if (boundsExtent > MinMeshExtent)
            {
                return boundsExtent;
            }

            var vertexExtent = vertices.Count == 0
                ? 1d
                : vertices.Max(vertex => Math.Max(
                    Math.Max(Math.Abs(vertex.X - bounds.Center.X), Math.Abs(vertex.Y - bounds.Center.Y)),
                    Math.Abs(vertex.Z - bounds.Center.Z)));
            return Math.Max(vertexExtent, 1d);
        }
    }

    private sealed class MeshUvProjection
    {
        private MeshUvProjection(
            IReadOnlyList<Vector2> points,
            double centerU,
            double centerV,
            double viewportCenterX,
            double viewportCenterY,
            double scale)
        {
            Points = points;
            CenterU = centerU;
            CenterV = centerV;
            ViewportCenterX = viewportCenterX;
            ViewportCenterY = viewportCenterY;
            Scale = scale;
        }

        public IReadOnlyList<Vector2> Points { get; }

        private double CenterU { get; }

        private double CenterV { get; }

        private double ViewportCenterX { get; }

        private double ViewportCenterY { get; }

        private double Scale { get; }

        public static MeshUvProjection Create(
            IReadOnlyList<AssetPreviewMeshUvDto> uvs,
            float viewportWidth,
            float viewportHeight,
            double zoom)
        {
            var minU = Math.Min(0d, uvs.Min(static uv => uv.U));
            var maxU = Math.Max(1d, uvs.Max(static uv => uv.U));
            var minV = Math.Min(0d, uvs.Min(static uv => uv.V));
            var maxV = Math.Max(1d, uvs.Max(static uv => uv.V));
            var extent = Math.Max(Math.Max(maxU - minU, maxV - minV), MinMeshExtent);
            var scale = (Math.Min(viewportWidth, viewportHeight) * UvViewportPaddingRatio / extent) *
                        Math.Max(0.01d, zoom);
            var projection = new MeshUvProjection(
                points: Array.Empty<Vector2>(),
                centerU: (minU + maxU) / 2d,
                centerV: (minV + maxV) / 2d,
                viewportCenterX: viewportWidth / 2d,
                viewportCenterY: viewportHeight / 2d,
                scale: scale);

            return new MeshUvProjection(
                points: uvs.Select(projection.Project).ToArray(),
                centerU: projection.CenterU,
                centerV: projection.CenterV,
                viewportCenterX: projection.ViewportCenterX,
                viewportCenterY: projection.ViewportCenterY,
                scale: projection.Scale);
        }

        public Vector2 Project(AssetPreviewMeshUvDto uv) =>
            new(
                (float)(ViewportCenterX + ((uv.U - CenterU) * Scale)),
                (float)(ViewportCenterY - ((uv.V - CenterV) * Scale)));
    }

    private readonly record struct ProjectionTransform(
        AssetPreviewMeshVertexDto Center,
        double ViewportCenterX,
        double ViewportCenterY,
        double Scale,
        double RotationX,
        double RotationY,
        double CameraDistance)
    {
        public InspectorMeshProjectedVertex Project(AssetPreviewMeshVertexDto vertex)
        {
            var x = vertex.X - Center.X;
            var y = vertex.Y - Center.Y;
            var z = vertex.Z - Center.Z;

            var sinX = Math.Sin(RotationX);
            var cosX = Math.Cos(RotationX);
            var rotatedY = (y * cosX) - (z * sinX);
            var rotatedZ = (y * sinX) + (z * cosX);

            var sinY = Math.Sin(RotationY);
            var cosY = Math.Cos(RotationY);
            var rotatedX = (x * cosY) + (rotatedZ * sinY);
            rotatedZ = (-x * sinY) + (rotatedZ * cosY);

            var perspective = CameraDistance / Math.Max(CameraDistance - rotatedZ, CameraDistance * 0.35d);
            perspective = Math.Clamp(perspective, 0.45d, 2.2d);

            return new InspectorMeshProjectedVertex(
                new Vector2(
                    (float)(ViewportCenterX + (rotatedX * Scale * perspective)),
                    (float)(ViewportCenterY - (rotatedY * Scale * perspective))),
                rotatedZ);
        }
    }
}

internal readonly record struct InspectorMeshProjectedVertex(
    Vector2 Screen,
    double Depth);
