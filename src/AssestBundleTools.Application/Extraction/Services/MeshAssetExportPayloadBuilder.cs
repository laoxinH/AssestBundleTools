using System.Globalization;
using System.IO;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 将 Mesh 预览几何转换为可落盘的静态模型导出 payload。
/// </summary>
internal static class MeshAssetExportPayloadBuilder
{
    public static byte[]? BuildPayload(
        AssetPreviewDto preview,
        AssetDetailsDto targetDetails,
        string suggestedOutputPath,
        Func<byte[]?> buildStructuredJsonPayload)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(targetDetails);
        ArgumentNullException.ThrowIfNull(buildStructuredJsonPayload);

        return ResolveExportFormat(suggestedOutputPath) switch
        {
            MeshExportFormat.Fbx => TryBuildFbxMesh(preview.MeshGeometry, targetDetails),
            MeshExportFormat.StructuredJson => buildStructuredJsonPayload(),
            _ => TryBuildObjMesh(preview.MeshGeometry, targetDetails)
        };
    }

    private static byte[]? TryBuildObjMesh(AssetPreviewMeshGeometryDto? mesh, AssetDetailsDto targetDetails)
    {
        if (mesh is null || mesh.Vertices.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("# AssestBundleTools OBJ export");
        builder.AppendLine($"# source-asset-id: {targetDetails.AssetId.Value}");
        builder.AppendLine($"# source-path-id: {targetDetails.PathId}");
        builder.AppendLine($"# source-vertex-count: {mesh.SourceVertexCount}");
        builder.AppendLine($"# source-triangle-count: {mesh.SourceTriangleCount}");
        if (mesh.IsGeometryTruncated)
        {
            builder.AppendLine("# warning: preview geometry was truncated before export");
        }

        builder.Append("o ");
        builder.AppendLine(SanitizeName(ResolveSourceFileName(targetDetails)));

        foreach (var vertex in mesh.Vertices)
        {
            builder.Append("v ");
            AppendInvariant(builder, vertex.X);
            builder.Append(' ');
            AppendInvariant(builder, vertex.Y);
            builder.Append(' ');
            AppendInvariant(builder, vertex.Z);
            builder.AppendLine();
        }

        var uv0 = mesh.Uv0;
        if (uv0 is { Count: > 0 })
        {
            foreach (var uv in uv0)
            {
                builder.Append("vt ");
                AppendInvariant(builder, uv.U);
                builder.Append(' ');
                AppendInvariant(builder, uv.V);
                builder.AppendLine();
            }
        }

        var hasFaces = AppendObjFaces(builder, mesh, uv0);
        if (!hasFaces)
        {
            AppendObjEdges(builder, mesh);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[]? TryBuildFbxMesh(AssetPreviewMeshGeometryDto? mesh, AssetDetailsDto targetDetails)
    {
        if (mesh is null || mesh.Vertices.Count == 0 || mesh.Triangles is not { Count: > 0 })
        {
            return null;
        }

        var triangles = mesh.Triangles
            .Where(triangle =>
                IsValidVertexIndex(triangle.A, mesh.Vertices.Count) &&
                IsValidVertexIndex(triangle.B, mesh.Vertices.Count) &&
                IsValidVertexIndex(triangle.C, mesh.Vertices.Count))
            .ToArray();
        if (triangles.Length == 0)
        {
            return null;
        }

        var objectName = SanitizeName(ResolveSourceFileName(targetDetails));
        var builder = new StringBuilder();
        builder.AppendLine("; FBX 7.4.0 project file");
        builder.AppendLine("; Exported by AssestBundleTools from preview mesh geometry.");
        if (mesh.IsGeometryTruncated)
        {
            builder.AppendLine("; warning: preview geometry was truncated before export.");
        }

        AppendFbxHeader(builder);
        builder.AppendLine("Objects:  {");
        AppendFbxGeometry(builder, objectName, mesh, triangles);
        AppendFbxModel(builder, objectName);
        builder.AppendLine("}");
        builder.AppendLine("Connections:  {");
        builder.AppendLine("\tC: \"OO\",1000001,1000002");
        builder.AppendLine("\tC: \"OO\",1000002,0");
        builder.AppendLine("}");

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendFbxHeader(StringBuilder builder)
    {
        builder.AppendLine("FBXHeaderExtension:  {");
        builder.AppendLine("\tFBXHeaderVersion: 1003");
        builder.AppendLine("\tFBXVersion: 7400");
        builder.AppendLine("\tCreator: \"AssestBundleTools\"");
        builder.AppendLine("}");
        builder.AppendLine("GlobalSettings:  {");
        builder.AppendLine("\tVersion: 1000");
        builder.AppendLine("\tProperties70:  {");
        builder.AppendLine("\t\tP: \"UpAxis\", \"int\", \"Integer\", \"\",1");
        builder.AppendLine("\t\tP: \"UpAxisSign\", \"int\", \"Integer\", \"\",1");
        builder.AppendLine("\t\tP: \"FrontAxis\", \"int\", \"Integer\", \"\",2");
        builder.AppendLine("\t\tP: \"FrontAxisSign\", \"int\", \"Integer\", \"\",1");
        builder.AppendLine("\t\tP: \"CoordAxis\", \"int\", \"Integer\", \"\",0");
        builder.AppendLine("\t\tP: \"CoordAxisSign\", \"int\", \"Integer\", \"\",1");
        builder.AppendLine("\t\tP: \"UnitScaleFactor\", \"double\", \"Number\", \"\",1");
        builder.AppendLine("\t}");
        builder.AppendLine("}");
        builder.AppendLine("Definitions:  {");
        builder.AppendLine("\tVersion: 100");
        builder.AppendLine("\tCount: 2");
        builder.AppendLine("\tObjectType: \"Geometry\" {");
        builder.AppendLine("\t\tCount: 1");
        builder.AppendLine("\t}");
        builder.AppendLine("\tObjectType: \"Model\" {");
        builder.AppendLine("\t\tCount: 1");
        builder.AppendLine("\t}");
        builder.AppendLine("}");
    }

    private static void AppendFbxGeometry(
        StringBuilder builder,
        string objectName,
        AssetPreviewMeshGeometryDto mesh,
        IReadOnlyList<AssetPreviewMeshTriangleDto> triangles)
    {
        builder.Append("\tGeometry: 1000001, \"Geometry::");
        builder.Append(objectName);
        builder.AppendLine("\", \"Mesh\" {");
        AppendFbxVertices(builder, mesh);
        AppendFbxPolygonIndices(builder, triangles);
        builder.AppendLine("\t\tGeometryVersion: 124");
        AppendFbxUvLayer(builder, mesh, triangles);
        builder.AppendLine("\t\tLayer: 0 {");
        builder.AppendLine("\t\t\tVersion: 100");
        if (CanWriteFbxUvLayer(mesh, triangles))
        {
            builder.AppendLine("\t\t\tLayerElement:  {");
            builder.AppendLine("\t\t\t\tType: \"LayerElementUV\"");
            builder.AppendLine("\t\t\t\tTypedIndex: 0");
            builder.AppendLine("\t\t\t}");
        }

        builder.AppendLine("\t\t}");
        builder.AppendLine("\t}");
    }

    private static void AppendFbxModel(StringBuilder builder, string objectName)
    {
        builder.Append("\tModel: 1000002, \"Model::");
        builder.Append(objectName);
        builder.AppendLine("\", \"Mesh\" {");
        builder.AppendLine("\t\tVersion: 232");
        builder.AppendLine("\t\tProperties70:  {");
        builder.AppendLine("\t\t\tP: \"Lcl Translation\", \"Lcl Translation\", \"\", \"A\",0,0,0");
        builder.AppendLine("\t\t\tP: \"Lcl Rotation\", \"Lcl Rotation\", \"\", \"A\",0,0,0");
        builder.AppendLine("\t\t\tP: \"Lcl Scaling\", \"Lcl Scaling\", \"\", \"A\",1,1,1");
        builder.AppendLine("\t\t}");
        builder.AppendLine("\t\tShading: T");
        builder.AppendLine("\t\tCulling: \"CullingOff\"");
        builder.AppendLine("\t}");
    }

    private static bool AppendObjFaces(
        StringBuilder builder,
        AssetPreviewMeshGeometryDto mesh,
        IReadOnlyList<AssetPreviewMeshUvDto>? uv0)
    {
        if (mesh.Triangles is not { Count: > 0 })
        {
            return false;
        }

        var activeSubMeshIndex = int.MinValue;
        var hasFaces = false;
        foreach (var triangle in mesh.Triangles)
        {
            if (!IsValidVertexIndex(triangle.A, mesh.Vertices.Count) ||
                !IsValidVertexIndex(triangle.B, mesh.Vertices.Count) ||
                !IsValidVertexIndex(triangle.C, mesh.Vertices.Count))
            {
                continue;
            }

            if (triangle.SubMeshIndex != activeSubMeshIndex)
            {
                activeSubMeshIndex = triangle.SubMeshIndex;
                builder.Append("g submesh_");
                builder.Append(activeSubMeshIndex);
                builder.AppendLine();
            }

            builder.Append("f ");
            AppendObjFaceIndex(builder, triangle.A, uv0);
            builder.Append(' ');
            AppendObjFaceIndex(builder, triangle.B, uv0);
            builder.Append(' ');
            AppendObjFaceIndex(builder, triangle.C, uv0);
            builder.AppendLine();
            hasFaces = true;
        }

        return hasFaces;
    }

    private static void AppendObjEdges(StringBuilder builder, AssetPreviewMeshGeometryDto mesh)
    {
        if (mesh.Edges.Count == 0)
        {
            return;
        }

        builder.AppendLine("g edges");
        foreach (var edge in mesh.Edges)
        {
            if (!IsValidVertexIndex(edge.A, mesh.Vertices.Count) ||
                !IsValidVertexIndex(edge.B, mesh.Vertices.Count))
            {
                continue;
            }

            builder.Append("l ");
            builder.Append(edge.A + 1);
            builder.Append(' ');
            builder.Append(edge.B + 1);
            builder.AppendLine();
        }
    }

    private static void AppendFbxVertices(StringBuilder builder, AssetPreviewMeshGeometryDto mesh)
    {
        builder.Append("\t\tVertices: *");
        builder.Append(mesh.Vertices.Count * 3);
        builder.AppendLine(" {");
        builder.Append("\t\t\ta: ");
        var index = 0;
        foreach (var vertex in mesh.Vertices)
        {
            AppendFbxDouble(builder, vertex.X, index++);
            AppendFbxDouble(builder, vertex.Y, index++);
            AppendFbxDouble(builder, vertex.Z, index++);
        }

        builder.AppendLine();
        builder.AppendLine("\t\t}");
    }

    private static void AppendFbxPolygonIndices(
        StringBuilder builder,
        IReadOnlyList<AssetPreviewMeshTriangleDto> triangles)
    {
        builder.Append("\t\tPolygonVertexIndex: *");
        builder.Append(triangles.Count * 3);
        builder.AppendLine(" {");
        builder.Append("\t\t\ta: ");
        var index = 0;
        foreach (var triangle in triangles)
        {
            AppendFbxInt(builder, triangle.A, index++);
            AppendFbxInt(builder, triangle.B, index++);
            AppendFbxInt(builder, -(triangle.C + 1), index++);
        }

        builder.AppendLine();
        builder.AppendLine("\t\t}");
    }

    private static void AppendFbxUvLayer(
        StringBuilder builder,
        AssetPreviewMeshGeometryDto mesh,
        IReadOnlyList<AssetPreviewMeshTriangleDto> triangles)
    {
        if (!CanWriteFbxUvLayer(mesh, triangles))
        {
            return;
        }

        builder.AppendLine("\t\tLayerElementUV: 0 {");
        builder.AppendLine("\t\t\tVersion: 101");
        builder.AppendLine("\t\t\tName: \"UV0\"");
        builder.AppendLine("\t\t\tMappingInformationType: \"ByPolygonVertex\"");
        builder.AppendLine("\t\t\tReferenceInformationType: \"Direct\"");
        builder.Append("\t\t\tUV: *");
        builder.Append(triangles.Count * 6);
        builder.AppendLine(" {");
        builder.Append("\t\t\t\ta: ");
        var index = 0;
        foreach (var triangle in triangles)
        {
            AppendFbxUv(builder, mesh.Uv0!, triangle.A, ref index);
            AppendFbxUv(builder, mesh.Uv0!, triangle.B, ref index);
            AppendFbxUv(builder, mesh.Uv0!, triangle.C, ref index);
        }

        builder.AppendLine();
        builder.AppendLine("\t\t\t}");
        builder.AppendLine("\t\t}");
    }

    private static bool CanWriteFbxUvLayer(
        AssetPreviewMeshGeometryDto mesh,
        IReadOnlyList<AssetPreviewMeshTriangleDto> triangles) =>
        mesh.Uv0 is { Count: > 0 } &&
        triangles.All(triangle =>
            triangle.A < mesh.Uv0.Count &&
            triangle.B < mesh.Uv0.Count &&
            triangle.C < mesh.Uv0.Count);

    private static void AppendObjFaceIndex(
        StringBuilder builder,
        int vertexIndex,
        IReadOnlyList<AssetPreviewMeshUvDto>? uv0)
    {
        var objIndex = vertexIndex + 1;
        builder.Append(objIndex);
        if (uv0 is not null && vertexIndex < uv0.Count)
        {
            builder.Append('/');
            builder.Append(objIndex);
        }
    }

    private static void AppendFbxUv(
        StringBuilder builder,
        IReadOnlyList<AssetPreviewMeshUvDto> uv0,
        int vertexIndex,
        ref int index)
    {
        var uv = uv0[vertexIndex];
        AppendFbxDouble(builder, uv.U, index++);
        AppendFbxDouble(builder, uv.V, index++);
    }

    private static void AppendFbxDouble(StringBuilder builder, double value, int index)
    {
        if (index > 0)
        {
            builder.Append(',');
        }

        AppendInvariant(builder, value);
    }

    private static void AppendFbxInt(StringBuilder builder, int value, int index)
    {
        if (index > 0)
        {
            builder.Append(',');
        }

        builder.Append(value);
    }

    private static bool IsValidVertexIndex(int index, int vertexCount) =>
        index >= 0 && index < vertexCount;

    private static void AppendInvariant(StringBuilder builder, double value)
    {
        builder.Append(value.ToString("G17", CultureInfo.InvariantCulture));
    }

    private static string ResolveSourceFileName(AssetDetailsDto targetDetails)
    {
        var fileName = ResolveFileNameFromPath(targetDetails.AssetPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        fileName = ResolveFileNameFromPath(targetDetails.ContainerPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        return string.IsNullOrWhiteSpace(targetDetails.Name)
            ? targetDetails.AssetId.Value
            : targetDetails.Name.Trim();
    }

    private static string? ResolveFileNameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Trim().Replace('\\', '/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        var fileName = lastSeparatorIndex >= 0
            ? normalizedPath[(lastSeparatorIndex + 1)..]
            : normalizedPath;
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim();
    }

    private static string SanitizeName(string name)
    {
        var sanitized = new string(name
            .Trim()
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.'
                ? ch
                : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized)
            ? "asset"
            : sanitized;
    }

    private static MeshExportFormat ResolveExportFormat(string suggestedOutputPath)
    {
        if (suggestedOutputPath.EndsWith(".mesh.json", StringComparison.OrdinalIgnoreCase))
        {
            return MeshExportFormat.StructuredJson;
        }

        return Path.GetExtension(suggestedOutputPath).Trim().ToLowerInvariant() switch
        {
            ".fbx" => MeshExportFormat.Fbx,
            ".json" => MeshExportFormat.StructuredJson,
            _ => MeshExportFormat.Obj
        };
    }

    private enum MeshExportFormat
    {
        Obj,
        Fbx,
        StructuredJson
    }
}
