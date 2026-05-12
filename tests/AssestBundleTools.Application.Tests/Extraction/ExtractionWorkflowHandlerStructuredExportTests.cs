using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Extraction;

internal static partial class ExtractionWorkflowHandlerTests
{
    public static async Task ExtractAssetHandler_ShouldWriteMeshPayload_AsObjOutput()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.meshes");
        var assetId = new AssetId("asset.mesh");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Mesh,
            "HeroPlane",
            "Assets/Meshes/HeroPlane"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateMeshPreview(workspaceId, bundleId, assetId));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Mesh extraction should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.True(writeRequest.SuggestedOutputPath.EndsWith("HeroPlane.obj", StringComparison.Ordinal), "Mesh extraction should write OBJ output.");
        var obj = Encoding.UTF8.GetString(writeRequest.Content ?? []);
        TestAssert.True(obj.Contains("o HeroPlane", StringComparison.Ordinal), "OBJ output should keep a stable object name.");
        TestAssert.True(obj.Contains("v 1 0 0", StringComparison.Ordinal), "OBJ output should include vertices.");
        TestAssert.True(obj.Contains("vt 1 0", StringComparison.Ordinal), "OBJ output should include UV coordinates.");
        TestAssert.True(obj.Contains("f 1/1 2/2 3/3", StringComparison.Ordinal), "OBJ output should include triangle faces.");
        TestAssert.Equal(AssetPreviewKind.Mesh, result.Value?.OutputKind, "Mesh extraction should report mesh output kind.");
    }

    public static async Task ExtractAssetHandler_ShouldWriteMeshPayload_AsFbx_WhenSelectedOutputPathUsesFbx()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.meshes");
        var assetId = new AssetId("asset.mesh");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Mesh,
            "HeroPlane",
            "Assets/Meshes/HeroPlane"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateMeshPreview(workspaceId, bundleId, assetId));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());
        const string selectedOutputPath = @"D:\Selected\HeroPlane.fbx";

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath,
            selectedOutputPath,
            PreserveSuggestedFileName: true));

        TestAssert.True(result.IsSuccess, "Mesh FBX extraction should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.Equal(selectedOutputPath, writeRequest.SuggestedOutputPath, "FBX extraction should preserve the selected output path.");
        var fbx = Encoding.UTF8.GetString(writeRequest.Content ?? []);
        TestAssert.True(fbx.Contains("FBXHeaderExtension", StringComparison.Ordinal), "FBX output should include the FBX header.");
        TestAssert.True(fbx.Contains("\"Geometry::HeroPlane\"", StringComparison.Ordinal), "FBX output should include a mesh geometry object.");
        TestAssert.True(fbx.Contains("PolygonVertexIndex", StringComparison.Ordinal), "FBX output should include polygon indices.");
        TestAssert.True(fbx.Contains("LayerElementUV", StringComparison.Ordinal), "FBX output should include UV0 when preview UV data is available.");
        TestAssert.Equal(AssetPreviewKind.Mesh, result.Value?.OutputKind, "FBX extraction should report mesh output kind.");
    }

    public static async Task ExtractAssetHandler_ShouldWriteStructuredSummaryPayload_AsJsonOutput()
    {
        var workspaceId = new WorkspaceId("workspace-session-1");
        var bundleId = new BundleId("bundle.materials");
        var assetId = new AssetId("asset.material");
        var registry = new TestWorkspaceSessionRegistry(workspaceId);
        registry.RegisterAsset(CreateAssetDetails(
            workspaceId,
            bundleId,
            assetId,
            AssetType.Material,
            "HeroMaterial",
            "Assets/Materials/HeroMaterial"));
        var previewHandler = new TestAssetPreviewHandler();
        previewHandler.RegisterPreview(CreateMaterialPreview(workspaceId, bundleId, assetId));
        var fileStore = new TestAssetExtractionFileStore();
        var handler = CreateHandler(registry, previewHandler, fileStore, new TestWorkspaceChangeStore());

        var result = await handler.HandleAsync(new ExtractAssetCommand(
            workspaceId,
            bundleId,
            assetId,
            WorkspaceKey,
            WorkspaceDataRootPath));

        TestAssert.True(result.IsSuccess, "Structured material extraction should succeed.");
        var writeRequest = TestAssert.Single(fileStore.Requests);
        TestAssert.True(writeRequest.SuggestedOutputPath.EndsWith("HeroMaterial.material.json", StringComparison.Ordinal), "Material extraction should write JSON summary output.");
        var json = Encoding.UTF8.GetString(writeRequest.Content ?? []);
        TestAssert.True(json.Contains("\"schemaVersion\"", StringComparison.Ordinal), "Structured JSON output should include schema metadata.");
        TestAssert.True(json.Contains("\"textureProperties\"", StringComparison.Ordinal), "Structured JSON output should include material texture properties.");
        TestAssert.Equal(AssetPreviewKind.Material, result.Value?.OutputKind, "Material extraction should report material output kind.");
    }

    private static AssetPreviewDto CreateMeshPreview(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId) =>
        new(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Kind: AssetPreviewKind.Mesh,
            ContentType: "model/obj",
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: CreateDiagnostic(),
            MeshGeometry: new AssetPreviewMeshGeometryDto(
                SchemaVersion: 3,
                SourceVertexCount: 3,
                SourceIndexCount: 3,
                SourceTriangleCount: 1,
                IsGeometryTruncated: false,
                Vertices:
                [
                    new AssetPreviewMeshVertexDto(0, 0, 0),
                    new AssetPreviewMeshVertexDto(1, 0, 0),
                    new AssetPreviewMeshVertexDto(0, 1, 0)
                ],
                Edges: Array.Empty<AssetPreviewMeshEdgeDto>(),
                SubMeshes:
                [
                    new AssetPreviewMeshSubMeshDto(
                        Index: 0,
                        FirstIndexByte: 0,
                        IndexCount: 3,
                        Topology: 0,
                        BaseVertex: 0,
                        FirstVertex: 0,
                        VertexCount: 3)
                ],
                Bounds: null,
                Uv0:
                [
                    new AssetPreviewMeshUvDto(0, 0),
                    new AssetPreviewMeshUvDto(1, 0),
                    new AssetPreviewMeshUvDto(0, 1)
                ],
                Channels: Array.Empty<AssetPreviewMeshChannelDto>(),
                Triangles:
                [
                    new AssetPreviewMeshTriangleDto(0, 1, 2, 0)
                ]));

    private static AssetPreviewDto CreateMaterialPreview(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId) =>
        new(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Kind: AssetPreviewKind.Material,
            ContentType: "application/json",
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: CreateDiagnostic(),
            Material: new AssetPreviewMaterialDto(
                SchemaVersion: 1,
                Shader: null,
                TextureProperties:
                [
                    new AssetPreviewMaterialTexturePropertyDto(
                        Name: "_MainTex",
                        FileId: 0,
                        PathId: 42,
                        SerializedFileName: "bundle.assets",
                        ExternalReferencePath: null,
                        IsExternal: false,
                        ScaleX: 1,
                        ScaleY: 1,
                        OffsetX: 0,
                        OffsetY: 0)
                ],
                FloatProperties: Array.Empty<AssetPreviewMaterialFloatPropertyDto>(),
                IntegerProperties: Array.Empty<AssetPreviewMaterialIntegerPropertyDto>(),
                ColorProperties:
                [
                    new AssetPreviewMaterialColorPropertyDto("_Color", 1, 1, 1, 1)
                ]));
}
