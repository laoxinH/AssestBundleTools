using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationVertexBatchProjector
{
    public static IReadOnlyList<SpineAnimationVertexBatch> Project(
        SpineAnimationFrameDto frame,
        IReadOnlyDictionary<string, SpineAnimationTexturePageSize> texturePages)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(texturePages);

        var batches = new List<SpineAnimationVertexBatch>();
        BatchBuilder? builder = null;
        foreach (var command in frame.DrawCommands)
        {
            if (!texturePages.TryGetValue(command.PageName, out var pageSize) ||
                !pageSize.IsValid)
            {
                continue;
            }

            if (builder is null || !string.Equals(builder.PageName, command.PageName, StringComparison.Ordinal))
            {
                FlushBuilder();
                builder = new BatchBuilder(command.PageName);
            }

            if (command.MeshVertices.Count > 0 && command.MeshTriangles.Count > 0)
            {
                AppendMesh(builder, command);
            }
            else
            {
                AppendRegion(builder, command, pageSize);
            }
        }

        FlushBuilder();
        return batches;

        void FlushBuilder()
        {
            if (builder is null || builder.IndexCount == 0)
            {
                return;
            }

            batches.Add(builder.Build());
        }
    }

    private static void AppendRegion(
        BatchBuilder builder,
        SpineAnimationDrawCommandDto command,
        SpineAnimationTexturePageSize pageSize)
    {
        if (command.Width <= 0d ||
            command.Height <= 0d ||
            command.SourceWidth <= 0 ||
            command.SourceHeight <= 0)
        {
            return;
        }

        var baseIndex = builder.VertexCount;
        var transform = CreateTransform(command);
        var halfWidth = (float)(command.Width / 2d);
        var halfHeight = (float)(command.Height / 2d);
        var tint = CreateTint(command);
        var uv = CreateRegionUv(command, pageSize);
        builder.AddVertex(CreateVertex(-halfWidth, -halfHeight, uv.Left, uv.Top, transform, tint));
        builder.AddVertex(CreateVertex(halfWidth, -halfHeight, uv.Right, uv.Top, transform, tint));
        builder.AddVertex(CreateVertex(halfWidth, halfHeight, uv.Right, uv.Bottom, transform, tint));
        builder.AddVertex(CreateVertex(-halfWidth, halfHeight, uv.Left, uv.Bottom, transform, tint));
        builder.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
        builder.AddTriangle(baseIndex, baseIndex + 2, baseIndex + 3);
    }

    private static void AppendMesh(
        BatchBuilder builder,
        SpineAnimationDrawCommandDto command)
    {
        var baseIndex = builder.VertexCount;
        var indexStart = builder.IndexCount;
        var tint = CreateTint(command);
        foreach (var vertex in command.MeshVertices)
        {
            builder.AddVertex(new SpineAnimationBatchVertex(
                (float)vertex.X,
                (float)vertex.Y,
                (float)vertex.U,
                (float)vertex.V,
                tint.R,
                tint.G,
                tint.B,
                tint.A));
        }

        foreach (var triangle in command.MeshTriangles)
        {
            if (!IsValidTriangle(triangle, command.MeshVertices.Count))
            {
                continue;
            }

            builder.AddTriangle(
                baseIndex + triangle.A,
                baseIndex + triangle.B,
                baseIndex + triangle.C);
        }

        if (builder.IndexCount == indexStart)
        {
            builder.RemoveVerticesFrom(baseIndex);
        }
    }

    private static Matrix3x2 CreateTransform(SpineAnimationDrawCommandDto command) =>
        new(
            (float)command.TransformM11,
            (float)command.TransformM12,
            (float)command.TransformM21,
            (float)command.TransformM22,
            (float)command.TransformM31,
            (float)command.TransformM32);

    private static SpineAnimationBatchVertex CreateVertex(
        float x,
        float y,
        float u,
        float v,
        Matrix3x2 transform,
        SpineAnimationVertexTint tint)
    {
        var position = Vector2.Transform(new Vector2(x, y), transform);
        return new SpineAnimationBatchVertex(position.X, position.Y, u, v, tint.R, tint.G, tint.B, tint.A);
    }

    private static SpineRegionUv CreateRegionUv(
        SpineAnimationDrawCommandDto command,
        SpineAnimationTexturePageSize pageSize)
    {
        var left = (float)command.SourceX / pageSize.Width;
        var top = (float)command.SourceY / pageSize.Height;
        var right = (float)(command.SourceX + command.SourceWidth) / pageSize.Width;
        var bottom = (float)(command.SourceY + command.SourceHeight) / pageSize.Height;
        return new SpineRegionUv(left, top, right, bottom);
    }

    private static SpineAnimationVertexTint CreateTint(SpineAnimationDrawCommandDto command) =>
        new(
            ClampUnit(command.TintR),
            ClampUnit(command.TintG),
            ClampUnit(command.TintB),
            ClampUnit(command.TintA));

    private static bool IsValidTriangle(SpineAnimationMeshTriangleDto triangle, int vertexCount) =>
        IsValidVertexIndex(triangle.A, vertexCount) &&
        IsValidVertexIndex(triangle.B, vertexCount) &&
        IsValidVertexIndex(triangle.C, vertexCount);

    private static bool IsValidVertexIndex(int index, int vertexCount) =>
        index >= 0 && index < vertexCount;

    private static float ClampUnit(double value) =>
        (float)Math.Clamp(value, 0d, 1d);

    private sealed class BatchBuilder(string pageName)
    {
        private readonly List<SpineAnimationBatchVertex> _vertices = [];
        private readonly List<int> _indices = [];

        public string PageName { get; } = pageName;

        public int VertexCount => _vertices.Count;

        public int IndexCount => _indices.Count;

        public void AddVertex(SpineAnimationBatchVertex vertex) =>
            _vertices.Add(vertex);

        public void AddTriangle(int a, int b, int c)
        {
            _indices.Add(a);
            _indices.Add(b);
            _indices.Add(c);
        }

        public void RemoveVerticesFrom(int startIndex) =>
            _vertices.RemoveRange(startIndex, _vertices.Count - startIndex);

        public SpineAnimationVertexBatch Build() =>
            new(PageName, _vertices.ToArray(), _indices.ToArray());
    }

    private readonly record struct SpineAnimationVertexTint(
        float R,
        float G,
        float B,
        float A);

    private readonly record struct SpineRegionUv(
        float Left,
        float Top,
        float Right,
        float Bottom);
}

internal sealed record SpineAnimationTexturePageSize(
    int Width,
    int Height)
{
    public bool IsValid => Width > 0 && Height > 0;
}

internal sealed record SpineAnimationVertexBatch(
    string PageName,
    IReadOnlyList<SpineAnimationBatchVertex> Vertices,
    IReadOnlyList<int> Indices);

internal readonly record struct SpineAnimationBatchVertex(
    float X,
    float Y,
    float U,
    float V,
    float R,
    float G,
    float B,
    float A);
