using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using Runtime = AssestBundleTools.Infrastructure.ThirdParty.Spine.V42;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal sealed partial class V42SpineAnimationRuntimeAdapter
{
    private static SpineAnimationDrawCommandDto? TryCreateMeshDrawCommand(
        Runtime.Slot slot,
        Runtime.MeshAttachment attachment,
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap,
        Runtime.SkeletonClipping clipper)
    {
        if (attachment.Region is not Runtime.AtlasRegion region ||
            !IsDrawableRegion(region) ||
            attachment.WorldVerticesLength < 2)
        {
            return null;
        }

        var geometry = TryCreateMeshGeometry(slot, attachment, clipper);
        if (geometry is null)
        {
            return null;
        }

        AddAttachment(attachmentMap, slot, attachment, region, geometry.Value.Bounds);
        var tint = CreateTint(slot, attachment);
        return new SpineAnimationDrawCommandDto(
            slot.Data.Name,
            attachment.Name,
            region.name,
            region.page.name,
            region.x,
            region.y,
            region.packedWidth,
            region.packedHeight,
            geometry.Value.Bounds.Width,
            geometry.Value.Bounds.Height,
            1d,
            0d,
            0d,
            1d,
            geometry.Value.Bounds.CenterX,
            geometry.Value.Bounds.CenterY)
        {
            TintR = tint.R,
            TintG = tint.G,
            TintB = tint.B,
            TintA = tint.A,
            MeshVertices = geometry.Value.Vertices,
            MeshTriangles = geometry.Value.Triangles
        };
    }

    private static SpineRuntimeMeshGeometry? TryCreateMeshGeometry(
        Runtime.Slot slot,
        Runtime.MeshAttachment attachment,
        Runtime.SkeletonClipping clipper)
    {
        if (attachment.UVs is not { Length: > 0 } uvs)
        {
            return null;
        }

        var worldVertices = new float[attachment.WorldVerticesLength];
        attachment.ComputeWorldVertices(slot, 0, attachment.WorldVerticesLength, worldVertices, 0);
        var vertexCount = Math.Min(worldVertices.Length, uvs.Length) / 2;
        if (vertexCount < 3)
        {
            return null;
        }

        var triangles = BuildMeshTriangles(attachment.Triangles, vertexCount);
        if (triangles.Count == 0)
        {
            return null;
        }

        if (clipper.IsClipping)
        {
            return CreateClippedGeometry(clipper, worldVertices, uvs, attachment.Triangles);
        }

        return CreateGeometry(worldVertices, uvs, attachment.Triangles, vertexCount * 2, attachment.Triangles.Length);
    }

    private static SpineRuntimeMeshGeometry? CreateGeometry(
        float[] worldVertices,
        float[] uvs,
        int[] triangleIndices) =>
        CreateGeometry(
            worldVertices,
            uvs,
            triangleIndices,
            Math.Min(worldVertices.Length, uvs.Length),
            triangleIndices.Length);

    private static SpineRuntimeMeshGeometry? CreateGeometry(
        float[] worldVertices,
        float[] uvs,
        int[] triangleIndices,
        int vertexCoordinateCount,
        int triangleIndexCount)
    {
        var normalizedCoordinateCount = Math.Min(
            Math.Min(worldVertices.Length, uvs.Length),
            Math.Max(0, vertexCoordinateCount));
        var vertexCount = normalizedCoordinateCount / 2;
        if (vertexCount < 3)
        {
            return null;
        }

        var vertices = new SpineAnimationMeshVertexDto[vertexCount];
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        for (var index = 0; index < vertexCount; index++)
        {
            var sourceIndex = index * 2;
            var x = worldVertices[sourceIndex];
            var y = worldVertices[sourceIndex + 1];
            var u = uvs[sourceIndex];
            var v = uvs[sourceIndex + 1];
            if (!float.IsFinite(x) || !float.IsFinite(y) ||
                !float.IsFinite(u) || !float.IsFinite(v))
            {
                return null;
            }

            vertices[index] = new SpineAnimationMeshVertexDto(x, y, u, v);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        var triangles = BuildMeshTriangles(
            triangleIndices,
            vertexCount,
            Math.Min(triangleIndices.Length, Math.Max(0, triangleIndexCount)));
        if (triangles.Count == 0)
        {
            return null;
        }

        if (!float.IsFinite(minX) || !float.IsFinite(minY) ||
            !float.IsFinite(maxX) || !float.IsFinite(maxY))
        {
            return null;
        }

        var width = Math.Max(maxX - minX, 1f);
        var height = Math.Max(maxY - minY, 1f);
        return new SpineRuntimeMeshGeometry(
            vertices,
            triangles,
            new SpineRuntimeBounds(
                (minX + maxX) / 2d,
                (minY + maxY) / 2d,
                width,
                height));
    }

    private static IReadOnlyList<SpineAnimationMeshTriangleDto> BuildMeshTriangles(
        int[]? triangleIndices,
        int vertexCount) =>
        BuildMeshTriangles(triangleIndices, vertexCount, triangleIndices?.Length ?? 0);

    private static IReadOnlyList<SpineAnimationMeshTriangleDto> BuildMeshTriangles(
        int[]? triangleIndices,
        int vertexCount,
        int triangleIndexCount)
    {
        if (triangleIndices is not { Length: >= 3 } ||
            triangleIndexCount < 3)
        {
            return Array.Empty<SpineAnimationMeshTriangleDto>();
        }

        var safeIndexCount = Math.Min(triangleIndices.Length, triangleIndexCount);
        var triangles = new List<SpineAnimationMeshTriangleDto>(safeIndexCount / 3);
        for (var index = 0; index < safeIndexCount - 2; index += 3)
        {
            var a = triangleIndices[index];
            var b = triangleIndices[index + 1];
            var c = triangleIndices[index + 2];
            if (IsValidMeshVertexIndex(a, vertexCount) &&
                IsValidMeshVertexIndex(b, vertexCount) &&
                IsValidMeshVertexIndex(c, vertexCount))
            {
                triangles.Add(new SpineAnimationMeshTriangleDto(a, b, c));
            }
        }

        return triangles;
    }

    private static bool IsValidMeshVertexIndex(int index, int vertexCount) =>
        index >= 0 && index < vertexCount;

    private static void AddAttachment(
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap,
        Runtime.Slot slot,
        Runtime.MeshAttachment attachment,
        Runtime.AtlasRegion region,
        SpineRuntimeBounds bounds)
    {
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{slot.Data.Name}\n{attachment.Name}\n{region.page.name}\n{region.name}");
        attachmentMap.TryAdd(key, new SpineAnimationAttachmentDto(
            slot.Data.Name,
            attachment.Name,
            region.name,
            region.page.name,
            region.x,
            region.y,
            region.packedWidth,
            region.packedHeight,
            region.degrees == 90,
            bounds.CenterX,
            bounds.CenterY,
            Math.Max(attachment.Width, bounds.Width),
            Math.Max(attachment.Height, bounds.Height),
            Rotation: 0d,
            ScaleX: 1d,
            ScaleY: 1d));
    }

    private readonly record struct SpineRuntimeMeshGeometry(
        IReadOnlyList<SpineAnimationMeshVertexDto> Vertices,
        IReadOnlyList<SpineAnimationMeshTriangleDto> Triangles,
        SpineRuntimeBounds Bounds);

    private readonly record struct SpineRuntimeBounds(
        double CenterX,
        double CenterY,
        double Width,
        double Height);
}
