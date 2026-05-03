using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static class AssetsToolsMeshTopologyBuilderTests
{
    public static void BuildTriangles_ShouldReadTriangleAndQuadTopology()
    {
        var indexBytes = new byte[]
        {
            0, 0,
            1, 0,
            2, 0,
            2, 0,
            3, 0,
            4, 0,
            5, 0
        };
        var subMeshes = new[]
        {
            new AssetPreviewMeshSubMeshDto(
                Index: 0,
                FirstIndexByte: 0,
                IndexCount: 3,
                Topology: 0,
                BaseVertex: 0,
                FirstVertex: 0,
                VertexCount: 3),
            new AssetPreviewMeshSubMeshDto(
                Index: 1,
                FirstIndexByte: 6,
                IndexCount: 4,
                Topology: 2,
                BaseVertex: 0,
                FirstVertex: 2,
                VertexCount: 4)
        };

        var triangles = AssetsToolsMeshTopologyBuilder.BuildTriangles(
            indexBytes,
            indexByteSize: 2,
            subMeshes,
            previewVertexCount: 6,
            maxPreviewTriangleCount: 8);

        TestAssert.Equal(3, triangles.Count, "Triangle topology should remain one face and quad topology should split into two faces.");
        TestAssert.Equal(new AssetPreviewMeshTriangleDto(0, 1, 2, 0), triangles[0], "Triangle indices should be read in order.");
        TestAssert.Equal(new AssetPreviewMeshTriangleDto(2, 3, 4, 1), triangles[1], "Quad first triangle should use a-b-c.");
        TestAssert.Equal(new AssetPreviewMeshTriangleDto(2, 4, 5, 1), triangles[2], "Quad second triangle should use a-c-d.");

        var sourceTriangleCount = AssetsToolsMeshTopologyBuilder.CountTriangles(subMeshes);

        TestAssert.Equal(3, sourceTriangleCount, "Triangle count should include quad topology as two triangles.");
    }

    public static void BuildTriangles_ShouldSkipIndicesOutsidePreviewVertexRange()
    {
        var indexBytes = new byte[]
        {
            0, 0,
            1, 0,
            2, 0,
            2, 0,
            3, 0,
            9, 0
        };
        var subMeshes = new[]
        {
            new AssetPreviewMeshSubMeshDto(
                Index: 0,
                FirstIndexByte: 0,
                IndexCount: 6,
                Topology: 0,
                BaseVertex: 0,
                FirstVertex: 0,
                VertexCount: 4)
        };

        var triangles = AssetsToolsMeshTopologyBuilder.BuildTriangles(
            indexBytes,
            indexByteSize: 2,
            subMeshes,
            previewVertexCount: 4,
            maxPreviewTriangleCount: 8);

        TestAssert.Equal(1, triangles.Count, "Triangles that reference vertices outside the preview subset should be skipped.");
        TestAssert.Equal(new AssetPreviewMeshTriangleDto(0, 1, 2, 0), triangles[0], "Valid triangle should remain available for textured preview.");
    }
}
