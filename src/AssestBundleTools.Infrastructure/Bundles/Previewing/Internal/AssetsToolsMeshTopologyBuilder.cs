using System.Buffers.Binary;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsMeshTopologyBuilder
{
    public static IReadOnlyList<AssetPreviewMeshEdgeDto> BuildEdges(
        byte[] indexBytes,
        int indexByteSize,
        IReadOnlyList<AssetPreviewMeshSubMeshDto> subMeshes,
        int previewVertexCount,
        int maxPreviewEdgeCount)
    {
        var edges = new HashSet<AssetPreviewMeshEdgeDto>();
        foreach (var subMesh in subMeshes)
        {
            if (edges.Count >= maxPreviewEdgeCount)
            {
                break;
            }

            switch (subMesh.Topology)
            {
                case 0:
                    AddTriangleEdges(indexBytes, indexByteSize, subMesh, previewVertexCount, maxPreviewEdgeCount, edges);
                    break;
                case 2:
                    AddQuadEdges(indexBytes, indexByteSize, subMesh, previewVertexCount, maxPreviewEdgeCount, edges);
                    break;
                case 3:
                    AddLineEdges(indexBytes, indexByteSize, subMesh, previewVertexCount, maxPreviewEdgeCount, edges);
                    break;
                case 4:
                    AddLineStripEdges(indexBytes, indexByteSize, subMesh, previewVertexCount, maxPreviewEdgeCount, edges);
                    break;
            }
        }

        return edges.ToArray();
    }

    public static IReadOnlyList<AssetPreviewMeshTriangleDto> BuildTriangles(
        byte[] indexBytes,
        int indexByteSize,
        IReadOnlyList<AssetPreviewMeshSubMeshDto> subMeshes,
        int previewVertexCount,
        int maxPreviewTriangleCount)
    {
        var triangles = new List<AssetPreviewMeshTriangleDto>();
        foreach (var subMesh in subMeshes)
        {
            if (triangles.Count >= maxPreviewTriangleCount)
            {
                break;
            }

            switch (subMesh.Topology)
            {
                case 0:
                    AddTriangles(indexBytes, indexByteSize, subMesh, previewVertexCount, maxPreviewTriangleCount, triangles);
                    break;
                case 2:
                    AddQuadTriangles(indexBytes, indexByteSize, subMesh, previewVertexCount, maxPreviewTriangleCount, triangles);
                    break;
            }
        }

        return triangles.ToArray();
    }

    public static int CountTriangles(IReadOnlyList<AssetPreviewMeshSubMeshDto> subMeshes)
    {
        var triangleCount = 0;
        foreach (var subMesh in subMeshes)
        {
            switch (subMesh.Topology)
            {
                case 0:
                    triangleCount += subMesh.IndexCount / 3;
                    break;
                case 2:
                    triangleCount += (subMesh.IndexCount / 4) * 2;
                    break;
            }
        }

        return triangleCount;
    }

    private static void AddTriangles(
        byte[] indexBytes,
        int indexByteSize,
        AssetPreviewMeshSubMeshDto subMesh,
        int previewVertexCount,
        int maxPreviewTriangleCount,
        List<AssetPreviewMeshTriangleDto> triangles)
    {
        var indexStartByte = subMesh.FirstIndexByte;
        var indexEndByte = Math.Min(indexBytes.Length, indexStartByte + (subMesh.IndexCount * indexByteSize));
        for (var offset = indexStartByte; offset + (indexByteSize * 3) <= indexEndByte; offset += indexByteSize * 3)
        {
            AddTriangle(
                triangles,
                ReadIndex(indexBytes, offset, indexByteSize) + subMesh.BaseVertex,
                ReadIndex(indexBytes, offset + indexByteSize, indexByteSize) + subMesh.BaseVertex,
                ReadIndex(indexBytes, offset + (indexByteSize * 2), indexByteSize) + subMesh.BaseVertex,
                subMesh.Index,
                previewVertexCount,
                maxPreviewTriangleCount);
            if (triangles.Count >= maxPreviewTriangleCount)
            {
                return;
            }
        }
    }

    private static void AddQuadTriangles(
        byte[] indexBytes,
        int indexByteSize,
        AssetPreviewMeshSubMeshDto subMesh,
        int previewVertexCount,
        int maxPreviewTriangleCount,
        List<AssetPreviewMeshTriangleDto> triangles)
    {
        var indexStartByte = subMesh.FirstIndexByte;
        var indexEndByte = Math.Min(indexBytes.Length, indexStartByte + (subMesh.IndexCount * indexByteSize));
        for (var offset = indexStartByte; offset + (indexByteSize * 4) <= indexEndByte; offset += indexByteSize * 4)
        {
            var a = ReadIndex(indexBytes, offset, indexByteSize) + subMesh.BaseVertex;
            var b = ReadIndex(indexBytes, offset + indexByteSize, indexByteSize) + subMesh.BaseVertex;
            var c = ReadIndex(indexBytes, offset + (indexByteSize * 2), indexByteSize) + subMesh.BaseVertex;
            var d = ReadIndex(indexBytes, offset + (indexByteSize * 3), indexByteSize) + subMesh.BaseVertex;
            AddTriangle(triangles, a, b, c, subMesh.Index, previewVertexCount, maxPreviewTriangleCount);
            AddTriangle(triangles, a, c, d, subMesh.Index, previewVertexCount, maxPreviewTriangleCount);
            if (triangles.Count >= maxPreviewTriangleCount)
            {
                return;
            }
        }
    }

    private static void AddTriangleEdges(
        byte[] indexBytes,
        int indexByteSize,
        AssetPreviewMeshSubMeshDto subMesh,
        int previewVertexCount,
        int maxPreviewEdgeCount,
        HashSet<AssetPreviewMeshEdgeDto> edges)
    {
        var indexStartByte = subMesh.FirstIndexByte;
        var indexEndByte = Math.Min(indexBytes.Length, indexStartByte + (subMesh.IndexCount * indexByteSize));
        for (var offset = indexStartByte; offset + (indexByteSize * 3) <= indexEndByte; offset += indexByteSize * 3)
        {
            var a = ReadIndex(indexBytes, offset, indexByteSize) + subMesh.BaseVertex;
            var b = ReadIndex(indexBytes, offset + indexByteSize, indexByteSize) + subMesh.BaseVertex;
            var c = ReadIndex(indexBytes, offset + (indexByteSize * 2), indexByteSize) + subMesh.BaseVertex;
            AddEdge(edges, a, b, previewVertexCount, maxPreviewEdgeCount);
            AddEdge(edges, b, c, previewVertexCount, maxPreviewEdgeCount);
            AddEdge(edges, c, a, previewVertexCount, maxPreviewEdgeCount);
            if (edges.Count >= maxPreviewEdgeCount)
            {
                return;
            }
        }
    }

    private static void AddQuadEdges(
        byte[] indexBytes,
        int indexByteSize,
        AssetPreviewMeshSubMeshDto subMesh,
        int previewVertexCount,
        int maxPreviewEdgeCount,
        HashSet<AssetPreviewMeshEdgeDto> edges)
    {
        var indexStartByte = subMesh.FirstIndexByte;
        var indexEndByte = Math.Min(indexBytes.Length, indexStartByte + (subMesh.IndexCount * indexByteSize));
        for (var offset = indexStartByte; offset + (indexByteSize * 4) <= indexEndByte; offset += indexByteSize * 4)
        {
            var a = ReadIndex(indexBytes, offset, indexByteSize) + subMesh.BaseVertex;
            var b = ReadIndex(indexBytes, offset + indexByteSize, indexByteSize) + subMesh.BaseVertex;
            var c = ReadIndex(indexBytes, offset + (indexByteSize * 2), indexByteSize) + subMesh.BaseVertex;
            var d = ReadIndex(indexBytes, offset + (indexByteSize * 3), indexByteSize) + subMesh.BaseVertex;
            AddEdge(edges, a, b, previewVertexCount, maxPreviewEdgeCount);
            AddEdge(edges, b, c, previewVertexCount, maxPreviewEdgeCount);
            AddEdge(edges, c, d, previewVertexCount, maxPreviewEdgeCount);
            AddEdge(edges, d, a, previewVertexCount, maxPreviewEdgeCount);
            if (edges.Count >= maxPreviewEdgeCount)
            {
                return;
            }
        }
    }

    private static void AddLineEdges(
        byte[] indexBytes,
        int indexByteSize,
        AssetPreviewMeshSubMeshDto subMesh,
        int previewVertexCount,
        int maxPreviewEdgeCount,
        HashSet<AssetPreviewMeshEdgeDto> edges)
    {
        var indexStartByte = subMesh.FirstIndexByte;
        var indexEndByte = Math.Min(indexBytes.Length, indexStartByte + (subMesh.IndexCount * indexByteSize));
        for (var offset = indexStartByte; offset + (indexByteSize * 2) <= indexEndByte; offset += indexByteSize * 2)
        {
            AddEdge(
                edges,
                ReadIndex(indexBytes, offset, indexByteSize) + subMesh.BaseVertex,
                ReadIndex(indexBytes, offset + indexByteSize, indexByteSize) + subMesh.BaseVertex,
                previewVertexCount,
                maxPreviewEdgeCount);
            if (edges.Count >= maxPreviewEdgeCount)
            {
                return;
            }
        }
    }

    private static void AddLineStripEdges(
        byte[] indexBytes,
        int indexByteSize,
        AssetPreviewMeshSubMeshDto subMesh,
        int previewVertexCount,
        int maxPreviewEdgeCount,
        HashSet<AssetPreviewMeshEdgeDto> edges)
    {
        var indexStartByte = subMesh.FirstIndexByte;
        var indexEndByte = Math.Min(indexBytes.Length, indexStartByte + (subMesh.IndexCount * indexByteSize));
        int? previousIndex = null;
        for (var offset = indexStartByte; offset + indexByteSize <= indexEndByte; offset += indexByteSize)
        {
            var currentIndex = ReadIndex(indexBytes, offset, indexByteSize) + subMesh.BaseVertex;
            if (previousIndex.HasValue)
            {
                AddEdge(edges, previousIndex.Value, currentIndex, previewVertexCount, maxPreviewEdgeCount);
            }

            previousIndex = currentIndex;
            if (edges.Count >= maxPreviewEdgeCount)
            {
                return;
            }
        }
    }

    private static void AddTriangle(
        List<AssetPreviewMeshTriangleDto> triangles,
        int a,
        int b,
        int c,
        int subMeshIndex,
        int previewVertexCount,
        int maxPreviewTriangleCount)
    {
        if (a == b ||
            b == c ||
            c == a ||
            a < 0 ||
            b < 0 ||
            c < 0 ||
            a >= previewVertexCount ||
            b >= previewVertexCount ||
            c >= previewVertexCount ||
            triangles.Count >= maxPreviewTriangleCount)
        {
            return;
        }

        triangles.Add(new AssetPreviewMeshTriangleDto(a, b, c, subMeshIndex));
    }

    private static void AddEdge(
        HashSet<AssetPreviewMeshEdgeDto> edges,
        int a,
        int b,
        int previewVertexCount,
        int maxPreviewEdgeCount)
    {
        if (a == b ||
            a < 0 ||
            b < 0 ||
            a >= previewVertexCount ||
            b >= previewVertexCount ||
            edges.Count >= maxPreviewEdgeCount)
        {
            return;
        }

        edges.Add(a < b
            ? new AssetPreviewMeshEdgeDto(a, b)
            : new AssetPreviewMeshEdgeDto(b, a));
    }

    private static int ReadIndex(byte[] indexBytes, int offset, int indexByteSize)
    {
        if (indexByteSize == 4)
        {
            return (int)BinaryPrimitives.ReadUInt32LittleEndian(indexBytes.AsSpan(offset, sizeof(uint)));
        }

        return BinaryPrimitives.ReadUInt16LittleEndian(indexBytes.AsSpan(offset, sizeof(ushort)));
    }
}
