using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetPreviewDisplayFormatterTests
{
    [Fact]
    public void BuildSpritePreviewOverlayDisplayState_ShouldProjectOverlayGeometry_AndBadgeText()
    {
        var formatter = new AssetPreviewDisplayFormatter(new TestLocalizationService());
        var preview = new AssetPreviewDto(
            WorkspaceId: new WorkspaceId("workspace-1"),
            BundleId: new BundleId("bundle-1"),
            AssetId: new AssetId("asset-1"),
            Kind: AssetPreviewKind.Image,
            ContentType: "image/png",
            BinaryContent: [0x89, 0x50, 0x4E, 0x47],
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: true,
            GeneratedAtUtc: new DateTimeOffset(2026, 4, 29, 8, 0, 0, TimeSpan.Zero),
            Diagnostic: new AssetPreviewDiagnosticDto(
                CacheStatus: AssetPreviewCacheStatusKind.Hit,
                FallbackReason: AssetPreviewFallbackReasonKind.None),
            ImageOverlay: new AssetPreviewImageOverlayDto(
                CanvasPixelWidth: 256,
                CanvasPixelHeight: 128,
                HighlightRegion: new AssetPreviewImageRegionDto(
                    X: 24,
                    Y: 36,
                    Width: 80,
                    Height: 48),
                PivotPoint: new AssetPreviewImagePointDto(64, 60),
                NineSliceBorder: new AssetPreviewImageBorderDto(8, 10, 12, 14)));

        var state = formatter.BuildSpritePreviewOverlayDisplayState(preview);

        Assert.NotNull(state);
        Assert.Equal(256d, state!.CanvasWidth);
        Assert.Equal(128d, state.CanvasHeight);
        Assert.Equal(24d, state.RegionX);
        Assert.Equal(36d, state.RegionY);
        Assert.Equal(80d, state.RegionWidth);
        Assert.Equal(48d, state.RegionHeight);
        Assert.Equal(64d, state.PivotX);
        Assert.Equal(60d, state.PivotY);
        Assert.Equal(8d, state.BorderLeft);
        Assert.Equal(10d, state.BorderTop);
        Assert.Equal(12d, state.BorderRight);
        Assert.Equal(14d, state.BorderBottom);
        Assert.Equal(
            ResourceKeys.InspectorSpritePreviewOverlayBadgeWithPivotFormat + ":24|36|80|48|64|60",
            state.BadgeText);
    }

    [Fact]
    public void BuildMeshPreviewDisplayState_ShouldProjectBadgeText()
    {
        var formatter = new AssetPreviewDisplayFormatter(new TestLocalizationService());
        var meshGeometry = new AssetPreviewMeshGeometryDto(
            SchemaVersion: 3,
            SourceVertexCount: 4,
            SourceIndexCount: 6,
            SourceTriangleCount: 2,
            IsGeometryTruncated: false,
            Vertices:
            [
                new AssetPreviewMeshVertexDto(0, 0, 0),
                new AssetPreviewMeshVertexDto(1, 0, 0),
                new AssetPreviewMeshVertexDto(1, 1, 0),
                new AssetPreviewMeshVertexDto(0, 1, 0)
            ],
            Edges:
            [
                new AssetPreviewMeshEdgeDto(0, 1),
                new AssetPreviewMeshEdgeDto(1, 2),
                new AssetPreviewMeshEdgeDto(2, 3),
                new AssetPreviewMeshEdgeDto(0, 3)
            ],
            SubMeshes: Array.Empty<AssetPreviewMeshSubMeshDto>(),
            Bounds: null,
            Uv0:
            [
                new AssetPreviewMeshUvDto(0, 0),
                new AssetPreviewMeshUvDto(1, 0),
                new AssetPreviewMeshUvDto(1, 1),
                new AssetPreviewMeshUvDto(0, 1)
            ],
            Channels:
            [
                new AssetPreviewMeshChannelDto(0, 0, 0, 0, 3, true),
                new AssetPreviewMeshChannelDto(4, 0, 12, 0, 2, true)
            ],
            Triangles:
            [
                new AssetPreviewMeshTriangleDto(0, 1, 2, 0),
                new AssetPreviewMeshTriangleDto(0, 2, 3, 0)
            ]);
        var details = new AssetDetailsDto(
            WorkspaceId: new WorkspaceId("workspace-1"),
            BundleId: new BundleId("bundle-1"),
            AssetId: new AssetId("asset-1"),
            Name: "Quad",
            AssetPath: "Assets/Quad.mesh",
            ContainerPath: "Assets",
            AssetType: AssetType.Mesh,
            SizeInBytes: 256,
            PreferredDisplaySizeInBytes: null,
            PathId: 100,
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
            Dependencies:
            [
                new AssetDependencyDto(
                    new AssetId("mat-1"),
                    new BundleId("bundle-1"),
                    "HeroMat",
                    AssetType.Material,
                    AssetTypeDisplayName: "Material"),
                new AssetDependencyDto(
                    new AssetId("tex-1"),
                    new BundleId("bundle-1"),
                    "HeroTex",
                    AssetType.Texture2D,
                    AssetTypeDisplayName: "Texture2D")
            ]);
        var preview = new AssetPreviewDto(
            WorkspaceId: new WorkspaceId("workspace-1"),
            BundleId: new BundleId("bundle-1"),
            AssetId: new AssetId("asset-1"),
            Kind: AssetPreviewKind.Mesh,
            ContentType: "application/vnd.assestbundletools.mesh-preview+json",
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: true,
            GeneratedAtUtc: new DateTimeOffset(2026, 5, 3, 8, 0, 0, TimeSpan.Zero),
            Diagnostic: new AssetPreviewDiagnosticDto(
                CacheStatus: AssetPreviewCacheStatusKind.Hit,
                FallbackReason: AssetPreviewFallbackReasonKind.None),
            MeshGeometry: meshGeometry);

        var state = formatter.BuildMeshPreviewDisplayState(preview, details);

        Assert.Equal(ResourceKeys.InspectorMeshPreviewBadgeFormat + ":4|4", state.BadgeText);
        Assert.Equal(ResourceKeys.InspectorMeshPreviewUvBadgeFormat + ":4|4", state.UvBadgeText);
        Assert.Equal(
            ResourceKeys.InspectorMeshPreviewTextureAssociationFormat + ":" +
            ResourceKeys.InspectorMeshPreviewTextureAssociationItemFormat + ":HeroMat|Material, " +
            ResourceKeys.InspectorMeshPreviewTextureAssociationItemFormat + ":HeroTex|Texture2D",
            state.TextureAssociationText);
    }

    [Fact]
    public void BuildMeshPreviewDisplayState_ShouldPreferTextureAssociations_WhenAvailable()
    {
        var formatter = new AssetPreviewDisplayFormatter(new TestLocalizationService());
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
            SubMeshes: Array.Empty<AssetPreviewMeshSubMeshDto>(),
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
            ]);
        var details = new AssetDetailsDto(
            WorkspaceId: new WorkspaceId("workspace-1"),
            BundleId: new BundleId("bundle-1"),
            AssetId: new AssetId("mesh-1"),
            Name: "Mesh",
            AssetPath: "mesh",
            ContainerPath: "",
            AssetType: AssetType.Mesh,
            SizeInBytes: 1,
            PreferredDisplaySizeInBytes: null,
            PathId: 1,
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
            Dependencies:
            [
                new AssetDependencyDto(
                    new AssetId("fallback-texture"),
                    new BundleId("bundle-1"),
                    "FallbackTexture",
                    AssetType.Texture2D,
                    AssetTypeDisplayName: "Texture2D")
            ],
            TextureAssociations:
            [
                new AssetDependencyDto(
                    new AssetId("renderer-texture"),
                    new BundleId("bundle-1"),
                    "RendererTexture",
                    AssetType.Texture2D,
                    AssetTypeDisplayName: "Texture2D")
            ]);
        var preview = new AssetPreviewDto(
            WorkspaceId: details.WorkspaceId,
            BundleId: details.BundleId,
            AssetId: details.AssetId,
            Kind: AssetPreviewKind.Mesh,
            ContentType: "application/vnd.assestbundletools.mesh-preview+json",
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: true,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: new AssetPreviewDiagnosticDto(
                CacheStatus: AssetPreviewCacheStatusKind.Hit,
                FallbackReason: AssetPreviewFallbackReasonKind.None),
            MeshGeometry: meshGeometry);

        var state = formatter.BuildMeshPreviewDisplayState(preview, details);

        Assert.Equal(
            ResourceKeys.InspectorMeshPreviewTextureAssociationFormat + ":" +
            ResourceKeys.InspectorMeshPreviewTextureAssociationItemFormat + ":RendererTexture|Texture2D",
            state.TextureAssociationText);
    }
}
