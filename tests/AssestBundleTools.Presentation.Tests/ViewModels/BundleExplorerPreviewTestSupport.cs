using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

internal static class BundleExplorerPreviewTestSupport
{
    public static WorkspaceSummaryDto CreateWorkspaceSummary(
        WorkspaceId workspaceId,
        WorkspaceNavigationRequest workspaceRequest)
    {
        return new WorkspaceSummaryDto(
            workspaceId,
            workspaceRequest.WorkspaceDisplayName,
            workspaceRequest.SourcePath,
            WorkspaceMode.SinglePane,
            PaneId.New(),
            BundleCount: 1,
            AssetCount: 1,
            OpenedAtUtc: DateTimeOffset.UtcNow);
    }

    public static AssetDocumentTabViewModel CreateAssetTab(
        BundleId bundleId,
        AssetId assetId,
        TestLocalizationService localizationService)
    {
        return new AssetDocumentTabViewModel(
            bundleId,
            assetId,
            "Button",
            InspectorInformationTextSetFactory.Create(localizationService));
    }

    public static Result<AssetPreviewDto> CreateSummaryPreviewResult(GetAssetPreviewQuery query)
    {
        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Summary,
            ContentType: "text/plain",
            TextContent: "summary",
            BinaryContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None)));
    }

    public static Result<AssetPreviewDto> CreateMeshPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\mesh.json")
    {
        var meshGeometry = new AssetPreviewMeshGeometryDto(
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
            Edges:
            [
                new AssetPreviewMeshEdgeDto(0, 1),
                new AssetPreviewMeshEdgeDto(1, 2),
                new AssetPreviewMeshEdgeDto(0, 2)
            ],
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
            Bounds: new AssetPreviewMeshBoundsDto(
                new AssetPreviewMeshVertexDto(0.5, 0.5, 0),
                new AssetPreviewMeshVertexDto(0.5, 0.5, 0)),
            Uv0:
            [
                new AssetPreviewMeshUvDto(0, 0),
                new AssetPreviewMeshUvDto(1, 0),
                new AssetPreviewMeshUvDto(0, 1)
            ],
            Channels:
            [
                new AssetPreviewMeshChannelDto(
                    Index: 0,
                    Stream: 0,
                    Offset: 0,
                    Format: 0,
                    Dimension: 3,
                    IsPreviewReadable: true),
                new AssetPreviewMeshChannelDto(
                    Index: 4,
                    Stream: 0,
                    Offset: 12,
                    Format: 0,
                    Dimension: 2,
                    IsPreviewReadable: true)
            ],
            Triangles:
            [
                new AssetPreviewMeshTriangleDto(0, 1, 2, 0)
            ]);

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Mesh,
            ContentType: "application/vnd.assestbundletools.mesh-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.MeshVertexCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: meshGeometry.SourceVertexCount)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            MeshGeometry: meshGeometry));
    }

    public static Result<AssetPreviewDto> CreateMaterialPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\material.json")
    {
        var material = new AssetPreviewMaterialDto(
            SchemaVersion: 1,
            Shader: new AssetPreviewMaterialShaderDto(
                FileId: 0,
                PathId: 100,
                Name: "Particles/Additive",
                SerializedFileName: "materials.assets",
                ExternalReferencePath: null,
                IsExternal: false),
            TextureProperties:
            [
                new AssetPreviewMaterialTexturePropertyDto(
                    Name: "_MainTex",
                    FileId: 5,
                    PathId: 200,
                    SerializedFileName: "CAB-textures",
                    ExternalReferencePath: "archive:/CAB-textures/CAB-textures",
                    IsExternal: true,
                    ScaleX: 1,
                    ScaleY: 1,
                    OffsetX: 0,
                    OffsetY: 0)
            ],
            FloatProperties:
            [
                new AssetPreviewMaterialFloatPropertyDto("_Glossiness", 1)
            ],
            IntegerProperties:
            [
                new AssetPreviewMaterialIntegerPropertyDto("_Cull", 2)
            ],
            ColorProperties:
            [
                new AssetPreviewMaterialColorPropertyDto("_Color", 1, 0, 0, 1)
            ]);

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Material,
            ContentType: "application/vnd.assestbundletools.material-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.MaterialTexturePropertyCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: material.TextureProperties.Count)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            Material: material));
    }

    public static Result<AssetPreviewDto> CreateAnimationClipPreviewResult(
        GetAssetPreviewQuery query,
        string previewPayloadFilePath = @"C:\PreviewCache\animation-clip.json")
    {
        var animationClip = new AssetPreviewAnimationClipDto(
            SchemaVersion: 1,
            SampleRate: 30,
            StartTime: 0,
            StopTime: 1.5,
            Duration: 1.5,
            EstimatedFrameCount: 45,
            IsLegacy: false,
            LoopTime: true,
            LoopBlend: false,
            LoopBlendOrientation: true,
            LoopBlendPositionY: false,
            LoopBlendPositionXZ: true,
            FloatCurveCount: 8,
            PositionCurveCount: 2,
            RotationCurveCount: 3,
            ScaleCurveCount: 1,
            PPtrCurveCount: 0,
            Events:
            [
                new AssetPreviewAnimationEventDto(
                    Time: 0.25,
                    FunctionName: "OnStep",
                    StringParameter: "left",
                    FloatParameter: 0.5,
                    IntParameter: 2)
            ]);

        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.AnimationClip,
            ContentType: "application/vnd.assestbundletools.animation-clip-preview+json",
            TextContent: null,
            BinaryContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.AnimationClipEventCount,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: animationClip.Events.Count)
            ],
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None),
            AnimationClip: animationClip));
    }

    public static Result<AssetPreviewDto> CreateTexturePreviewResult(
        GetAssetPreviewQuery query,
        byte[]? binaryContent = null)
    {
        return Result<AssetPreviewDto>.Success(new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Image,
            ContentType: "image/png",
            TextContent: null,
            BinaryContent: binaryContent ?? [0x89, 0x50, 0x4E, 0x47],
            PreviewPayloadFilePath: @"C:\PreviewCache\texture.png",
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None)));
    }

    public static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!predicate())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException();
            }

            await Task.Delay(10);
        }
    }

    public static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId)
    {
        return new AssetDetailsDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: "Button",
            AssetPath: "Assets/UI/Button.png",
            ContainerPath: "Assets/UI",
            AssetType: AssetType.Texture2D,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: null,
            PathId: 1001,
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: "Texture2D");
    }
}
