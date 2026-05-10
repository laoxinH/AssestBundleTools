using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationMeshAutoFitBoundsResolver
{
    public static IReadOnlyList<Vector2> ResolveTrianglePoints(
        IReadOnlyList<SpineAnimationMeshVertexDto> vertices,
        SpineAnimationMeshTriangleDto triangle,
        SpineAnimationTexturePageSize pageSize) =>
        ResolveTriangleContribution(vertices, triangle, pageSize).BoundsPoints;

    public static SpineAnimationMeshAutoFitContribution ResolveTriangleContribution(
        IReadOnlyList<SpineAnimationMeshVertexDto> vertices,
        SpineAnimationMeshTriangleDto triangle,
        SpineAnimationTexturePageSize pageSize)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        ArgumentNullException.ThrowIfNull(pageSize);

        var trianglePoints = CreateTriangleWorldPoints(vertices, triangle);
        if (pageSize.AlphaMap is null)
        {
            return new SpineAnimationMeshAutoFitContribution(trianglePoints);
        }

        var first = vertices[triangle.A];
        var second = vertices[triangle.B];
        var third = vertices[triangle.C];
        if (!pageSize.AlphaMap.TryResolveVisibleTriangleBounds(
                first.U,
                first.V,
                second.U,
                second.V,
                third.U,
                third.V,
                out var sourceBounds))
        {
            return SpineAnimationMeshAutoFitContribution.Empty;
        }

        if (!TryCreateSourceToWorldTransform(first, second, third, pageSize, out var sourceToWorld))
        {
            return new SpineAnimationMeshAutoFitContribution(trianglePoints);
        }

        return new SpineAnimationMeshAutoFitContribution(
            CreateVisibleSourceBoundsCorners(sourceBounds, sourceToWorld));
    }

    private static Vector2[] CreateTriangleWorldPoints(
        IReadOnlyList<SpineAnimationMeshVertexDto> vertices,
        SpineAnimationMeshTriangleDto triangle) =>
        [
            ToWorldPoint(vertices[triangle.A]),
            ToWorldPoint(vertices[triangle.B]),
            ToWorldPoint(vertices[triangle.C])
        ];

    private static Vector2[] CreateVisibleSourceBoundsCorners(
        SpineAnimationTextureSourceAlphaBounds sourceBounds,
        Matrix3x2 sourceToWorld) =>
        [
            Vector2.Transform(new Vector2(sourceBounds.Left, sourceBounds.Top), sourceToWorld),
            Vector2.Transform(new Vector2(sourceBounds.Right, sourceBounds.Top), sourceToWorld),
            Vector2.Transform(new Vector2(sourceBounds.Right, sourceBounds.Bottom), sourceToWorld),
            Vector2.Transform(new Vector2(sourceBounds.Left, sourceBounds.Bottom), sourceToWorld)
        ];

    private static bool TryCreateSourceToWorldTransform(
        SpineAnimationMeshVertexDto first,
        SpineAnimationMeshVertexDto second,
        SpineAnimationMeshVertexDto third,
        SpineAnimationTexturePageSize pageSize,
        out Matrix3x2 sourceToWorld)
    {
        var sourceA = ToTexturePoint(first, pageSize);
        var sourceB = ToTexturePoint(second, pageSize);
        var sourceC = ToTexturePoint(third, pageSize);
        var worldA = ToWorldPoint(first);
        var worldB = ToWorldPoint(second);
        var worldC = ToWorldPoint(third);
        var sourceU = sourceB - sourceA;
        var sourceV = sourceC - sourceA;
        var determinant = Cross(sourceU, sourceV);
        if (Math.Abs(determinant) < 0.0001f)
        {
            sourceToWorld = default;
            return false;
        }

        var destinationU = worldB - worldA;
        var destinationV = worldC - worldA;
        var m11 = ((destinationU.X * sourceV.Y) - (destinationV.X * sourceU.Y)) / determinant;
        var m12 = ((destinationU.Y * sourceV.Y) - (destinationV.Y * sourceU.Y)) / determinant;
        var m21 = ((destinationV.X * sourceU.X) - (destinationU.X * sourceV.X)) / determinant;
        var m22 = ((destinationV.Y * sourceU.X) - (destinationU.Y * sourceV.X)) / determinant;
        var translation = worldA - TransformPointNoTranslation(sourceA, m11, m12, m21, m22);
        sourceToWorld = new Matrix3x2(m11, m12, m21, m22, translation.X, translation.Y);
        return true;
    }

    private static Vector2 ToTexturePoint(
        SpineAnimationMeshVertexDto vertex,
        SpineAnimationTexturePageSize pageSize) =>
        new((float)(vertex.U * pageSize.Width), (float)(vertex.V * pageSize.Height));

    private static Vector2 ToWorldPoint(SpineAnimationMeshVertexDto vertex) =>
        new((float)vertex.X, (float)vertex.Y);

    private static Vector2 TransformPointNoTranslation(
        Vector2 point,
        float m11,
        float m12,
        float m21,
        float m22) =>
        new(
            (point.X * m11) + (point.Y * m21),
            (point.X * m12) + (point.Y * m22));

    private static float Cross(Vector2 left, Vector2 right) =>
        (left.X * right.Y) - (left.Y * right.X);
}

internal readonly record struct SpineAnimationMeshAutoFitContribution(
    IReadOnlyList<Vector2> BoundsPoints)
{
    public static SpineAnimationMeshAutoFitContribution Empty { get; } =
        new(Array.Empty<Vector2>());
}
