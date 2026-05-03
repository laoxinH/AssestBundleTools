namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Mesh 富预览所需的轻量几何数据。
/// </summary>
public sealed record AssetPreviewMeshGeometryDto(
    int SchemaVersion,
    int SourceVertexCount,
    int SourceIndexCount,
    int SourceTriangleCount,
    bool IsGeometryTruncated,
    IReadOnlyList<AssetPreviewMeshVertexDto> Vertices,
    IReadOnlyList<AssetPreviewMeshEdgeDto> Edges,
    IReadOnlyList<AssetPreviewMeshSubMeshDto> SubMeshes,
    AssetPreviewMeshBoundsDto? Bounds,
    IReadOnlyList<AssetPreviewMeshUvDto>? Uv0 = null,
    IReadOnlyList<AssetPreviewMeshChannelDto>? Channels = null,
    IReadOnlyList<AssetPreviewMeshTriangleDto>? Triangles = null);

public sealed record AssetPreviewMeshVertexDto(
    double X,
    double Y,
    double Z);

public sealed record AssetPreviewMeshEdgeDto(
    int A,
    int B);

public sealed record AssetPreviewMeshTriangleDto(
    int A,
    int B,
    int C,
    int SubMeshIndex);

public sealed record AssetPreviewMeshSubMeshDto(
    int Index,
    int FirstIndexByte,
    int IndexCount,
    int Topology,
    int BaseVertex,
    int FirstVertex,
    int VertexCount);

public sealed record AssetPreviewMeshBoundsDto(
    AssetPreviewMeshVertexDto Center,
    AssetPreviewMeshVertexDto Extents);

public sealed record AssetPreviewMeshUvDto(
    double U,
    double V);

public sealed record AssetPreviewMeshChannelDto(
    int Index,
    int Stream,
    int Offset,
    int Format,
    int Dimension,
    bool IsPreviewReadable);
