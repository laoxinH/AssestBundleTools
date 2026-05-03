using System.Globalization;
using System.Text.Json;
using AssetsTools.NET;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsMeshPreviewBuilder
{
    private const string MeshPreviewContentType = "application/vnd.assestbundletools.mesh-preview+json";
    private const int MeshPreviewSchemaVersion = 3;
    private const int PositionChannelIndex = 0;
    private const int Uv0ChannelIndex = 4;
    private const int MaxPreviewVertexCount = 12000;
    private const int MaxPreviewEdgeCount = 40000;
    private const int MaxPreviewTriangleCount = 20000;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildMeshPreview(
        AssetTypeValueField meshField,
        long serializedObjectSizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        if (!TryReadGeometry(meshField, out var geometry))
        {
            return false;
        }

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Mesh,
            ContentType: MeshPreviewContentType,
            Content: JsonSerializer.SerializeToUtf8Bytes(geometry, JsonSerializerOptions),
            PreviewInfoItems: BuildPreviewInfoItems(meshField, geometry, serializedObjectSizeInBytes),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static bool TryReadGeometry(
        AssetTypeValueField meshField,
        out AssetPreviewMeshGeometryDto geometry)
    {
        geometry = null!;

        var vertexDataField = AssetsToolsFieldReader.TryGetField(meshField, "m_VertexData");
        var sourceVertexCount = NormalizeCount(AssetsToolsFieldReader.ReadUInt32(
            AssetsToolsFieldReader.TryGetField(vertexDataField, "m_VertexCount")));
        if (sourceVertexCount <= 0)
        {
            return false;
        }

        var vertexBytes = AssetsToolsFieldReader.ReadByteArray(
            AssetsToolsFieldReader.TryGetField(vertexDataField, "m_DataSize"));
        if (vertexBytes is not { Length: > 0 })
        {
            return false;
        }

        var channels = AssetsToolsMeshVertexDataReader.ReadChannels(vertexDataField);
        if (channels.Count <= PositionChannelIndex)
        {
            return false;
        }

        var positionChannel = channels[PositionChannelIndex];
        if (positionChannel.Dimension < 3 || !positionChannel.CanReadAsFloat)
        {
            return false;
        }

        var streamLayouts = AssetsToolsMeshVertexDataReader.BuildStreamLayouts(channels, sourceVertexCount);
        if (!streamLayouts.TryGetValue(positionChannel.Stream, out var positionStreamLayout))
        {
            return false;
        }

        var previewVertexCount = Math.Min(sourceVertexCount, MaxPreviewVertexCount);
        var vertices = new List<AssetPreviewMeshVertexDto>(previewVertexCount);
        for (var vertexIndex = 0; vertexIndex < previewVertexCount; vertexIndex++)
        {
            if (!AssetsToolsMeshVertexDataReader.TryReadPosition(
                    vertexBytes,
                    positionStreamLayout,
                    positionChannel,
                    vertexIndex,
                    out var vertex))
            {
                return false;
            }

            vertices.Add(vertex);
        }

        var uv0 = AssetsToolsMeshVertexDataReader.ReadUvChannel(
            vertexBytes,
            streamLayouts,
            channels,
            Uv0ChannelIndex,
            previewVertexCount);

        var subMeshes = ReadSubMeshes(meshField, sourceVertexCount);
        var indexBytes = AssetsToolsFieldReader.ReadByteArray(
            AssetsToolsFieldReader.TryGetField(meshField, "m_IndexBuffer", "Array"));
        var indexFormat = AssetsToolsFieldReader.ReadInt32(
            AssetsToolsFieldReader.TryGetField(meshField, "m_IndexFormat")) ?? 0;
        var indexByteSize = indexFormat == 1 ? 4 : 2;
        var sourceIndexCount = indexBytes is { Length: > 0 }
            ? indexBytes.Length / indexByteSize
            : 0;

        var edges = indexBytes is { Length: > 0 } && subMeshes.Count > 0
            ? AssetsToolsMeshTopologyBuilder.BuildEdges(indexBytes, indexByteSize, subMeshes, vertices.Count, MaxPreviewEdgeCount)
            : Array.Empty<AssetPreviewMeshEdgeDto>();
        var triangles = indexBytes is { Length: > 0 } && subMeshes.Count > 0
            ? AssetsToolsMeshTopologyBuilder.BuildTriangles(indexBytes, indexByteSize, subMeshes, vertices.Count, MaxPreviewTriangleCount)
            : Array.Empty<AssetPreviewMeshTriangleDto>();
        var sourceTriangleCount = AssetsToolsMeshTopologyBuilder.CountTriangles(subMeshes);
        var bounds = ReadBounds(meshField) ?? CalculateBounds(vertices);
        var isGeometryTruncated =
            sourceVertexCount > vertices.Count ||
            edges.Count >= MaxPreviewEdgeCount ||
            triangles.Count >= MaxPreviewTriangleCount;

        geometry = new AssetPreviewMeshGeometryDto(
            SchemaVersion: MeshPreviewSchemaVersion,
            SourceVertexCount: sourceVertexCount,
            SourceIndexCount: sourceIndexCount,
            SourceTriangleCount: sourceTriangleCount,
            IsGeometryTruncated: isGeometryTruncated,
            Vertices: vertices,
            Edges: edges,
            SubMeshes: subMeshes,
            Bounds: bounds,
            Uv0: uv0,
            Channels: channels.Select(MapChannel).ToArray(),
            Triangles: triangles);
        return true;
    }

    private static IReadOnlyList<AssetPreviewMeshSubMeshDto> ReadSubMeshes(
        AssetTypeValueField meshField,
        int sourceVertexCount)
    {
        var subMeshArrayField = AssetsToolsFieldReader.TryGetField(meshField, "m_SubMeshes", "Array");
        if (subMeshArrayField?.Children is not { Count: > 0 } children)
        {
            return Array.Empty<AssetPreviewMeshSubMeshDto>();
        }

        var subMeshes = new List<AssetPreviewMeshSubMeshDto>(children.Count);
        for (var index = 0; index < children.Count; index++)
        {
            var subMeshField = children[index];
            var firstByte = NormalizeCount(AssetsToolsFieldReader.ReadUInt32(
                AssetsToolsFieldReader.TryGetField(subMeshField, "firstByte")));
            var indexCount = NormalizeCount(AssetsToolsFieldReader.ReadUInt32(
                AssetsToolsFieldReader.TryGetField(subMeshField, "indexCount")));
            var topology = AssetsToolsFieldReader.ReadInt32(
                AssetsToolsFieldReader.TryGetField(subMeshField, "topology")) ?? 0;
            var baseVertex = NormalizeCount(AssetsToolsFieldReader.ReadUInt32(
                AssetsToolsFieldReader.TryGetField(subMeshField, "baseVertex")));
            var firstVertex = NormalizeCount(AssetsToolsFieldReader.ReadUInt32(
                AssetsToolsFieldReader.TryGetField(subMeshField, "firstVertex")));
            var vertexCount = NormalizeCount(AssetsToolsFieldReader.ReadUInt32(
                AssetsToolsFieldReader.TryGetField(subMeshField, "vertexCount")));

            subMeshes.Add(new AssetPreviewMeshSubMeshDto(
                Index: index,
                FirstIndexByte: firstByte,
                IndexCount: indexCount,
                Topology: topology,
                BaseVertex: baseVertex,
                FirstVertex: firstVertex,
                VertexCount: vertexCount <= 0 ? sourceVertexCount : vertexCount));
        }

        return subMeshes;
    }

    private static AssetPreviewMeshBoundsDto? ReadBounds(AssetTypeValueField meshField)
    {
        var aabbField = AssetsToolsFieldReader.TryGetField(meshField, "m_LocalAABB");
        var center = ReadVector3(AssetsToolsFieldReader.TryGetField(aabbField, "m_Center"));
        var extents = ReadVector3(AssetsToolsFieldReader.TryGetField(aabbField, "m_Extent"));
        return center is null || extents is null
            ? null
            : new AssetPreviewMeshBoundsDto(center, extents);
    }

    private static AssetPreviewMeshVertexDto? ReadVector3(AssetTypeValueField? vectorField)
    {
        var x = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "x"));
        var y = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "y"));
        var z = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(vectorField, "z"));
        return x.HasValue && y.HasValue && z.HasValue
            ? new AssetPreviewMeshVertexDto(x.Value, y.Value, z.Value)
            : null;
    }

    private static AssetPreviewMeshBoundsDto? CalculateBounds(
        IReadOnlyList<AssetPreviewMeshVertexDto> vertices)
    {
        if (vertices.Count == 0)
        {
            return null;
        }

        var minX = vertices[0].X;
        var minY = vertices[0].Y;
        var minZ = vertices[0].Z;
        var maxX = vertices[0].X;
        var maxY = vertices[0].Y;
        var maxZ = vertices[0].Z;

        foreach (var vertex in vertices)
        {
            minX = Math.Min(minX, vertex.X);
            minY = Math.Min(minY, vertex.Y);
            minZ = Math.Min(minZ, vertex.Z);
            maxX = Math.Max(maxX, vertex.X);
            maxY = Math.Max(maxY, vertex.Y);
            maxZ = Math.Max(maxZ, vertex.Z);
        }

        return new AssetPreviewMeshBoundsDto(
            Center: new AssetPreviewMeshVertexDto(
                (minX + maxX) / 2d,
                (minY + maxY) / 2d,
                (minZ + maxZ) / 2d),
            Extents: new AssetPreviewMeshVertexDto(
                (maxX - minX) / 2d,
                (maxY - minY) / 2d,
                (maxZ - minZ) / 2d));
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AssetTypeValueField meshField,
        AssetPreviewMeshGeometryDto geometry,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshVertexCount, geometry.SourceVertexCount);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshIndexCount, geometry.SourceIndexCount);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshTriangleCount, geometry.SourceTriangleCount);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshSubMeshCount, geometry.SubMeshes.Count);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshBlendShapeCount, CountArray(meshField, "m_Shapes", "channels"));
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshBoneNameHashCount, CountArray(meshField, "m_BoneNameHashes"));
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.MeshBoundsCenter, FormatVector(geometry.Bounds?.Center));
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.MeshBoundsExtents, FormatVector(geometry.Bounds?.Extents));
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshPreviewVertexCount, geometry.Vertices.Count);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshPreviewEdgeCount, geometry.Edges.Count);
        AssetsToolsImagePreviewSupport.AddBooleanInfo(items, AssetPreviewInfoKey.MeshPreviewGeometryTruncated, geometry.IsGeometryTruncated);
        if (geometry.Uv0 is { Count: > 0 } uv0)
        {
            AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.MeshPreviewUvCount, uv0.Count);
        }

        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.MeshVertexChannelSummary, FormatChannelSummary(geometry.Channels));
        return items;
    }

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }

    private static AssetPreviewMeshChannelDto MapChannel(AssetsToolsMeshChannelInfo channel) =>
        new(
            Index: channel.Index,
            Stream: channel.Stream,
            Offset: channel.Offset,
            Format: channel.Format,
            Dimension: channel.Dimension,
            IsPreviewReadable: channel.CanReadAsFloat);

    private static string? FormatChannelSummary(IReadOnlyList<AssetPreviewMeshChannelDto>? channels)
    {
        if (channels is not { Count: > 0 })
        {
            return null;
        }

        return string.Join(
            ", ",
            channels
                .Where(static channel => channel.Dimension > 0)
                .Select(static channel => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{channel.Index}[s{channel.Stream}, o{channel.Offset}, f{channel.Format}, d{channel.Dimension}]")));
    }

    private static int NormalizeCount(uint? value) =>
        value.HasValue && value.Value <= int.MaxValue
            ? (int)value.Value
            : 0;

    private static string? FormatVector(AssetPreviewMeshVertexDto? vertex) =>
        vertex is null
            ? null
            : string.Create(
                CultureInfo.InvariantCulture,
                $"x={vertex.X:0.###}, y={vertex.Y:0.###}, z={vertex.Z:0.###}");
}
