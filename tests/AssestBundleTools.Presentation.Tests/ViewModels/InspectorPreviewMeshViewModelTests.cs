using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPreviewMeshViewModelTests
{
    [Fact]
    public void ToggleDisplayMode_ShouldSwitchBetweenWireframeAndUv_WhenUvExists()
    {
        var viewModel = new InspectorPreviewMeshViewModel();
        var geometry = CreateMeshGeometry(hasUv: true);

        viewModel.Apply(
            geometry,
            new MeshPreviewDisplayState(
                BadgeText: "wire",
                UvBadgeText: "uv",
                TextureAssociationText: "links"));

        Assert.Equal(Visibility.Visible, viewModel.DisplayModeSwitchVisibility);
        Assert.True(viewModel.IsWireframeSelected);
        Assert.Equal("wire", viewModel.BadgeText);
        Assert.Equal("links", viewModel.TextureAssociationText);
        Assert.Equal(Visibility.Visible, viewModel.TextureAssociationVisibility);

        viewModel.ToggleDisplayMode();

        Assert.True(viewModel.IsUvSelected);
        Assert.Equal("uv", viewModel.BadgeText);

        viewModel.ToggleDisplayMode();

        Assert.True(viewModel.IsWireframeSelected);
        Assert.Equal("wire", viewModel.BadgeText);
    }

    [Fact]
    public void ToggleDisplayMode_ShouldRemainWireframe_WhenUvIsMissing()
    {
        var viewModel = new InspectorPreviewMeshViewModel();

        viewModel.Apply(
            CreateMeshGeometry(hasUv: false),
            new MeshPreviewDisplayState(
                BadgeText: ResourceKeys.InspectorMeshPreviewBadgeFormat,
                UvBadgeText: ResourceKeys.InspectorMeshPreviewUvBadgeFormat));

        viewModel.ToggleDisplayMode();

        Assert.True(viewModel.IsWireframeSelected);
        Assert.False(viewModel.IsUvSelected);
        Assert.Equal(Visibility.Collapsed, viewModel.DisplayModeSwitchVisibility);
        Assert.Equal(ResourceKeys.InspectorMeshPreviewBadgeFormat, viewModel.BadgeText);
    }

    [Fact]
    public void RestoreDisplayOptions_ShouldApplyUvMode_WhenGeometryLoadsLater()
    {
        var viewModel = new InspectorPreviewMeshViewModel();

        viewModel.RestoreDisplayOptions("Uv", null, null, null, null, null);
        viewModel.Apply(
            CreateMeshGeometry(hasUv: true),
            new MeshPreviewDisplayState("wire", "uv"));

        Assert.True(viewModel.IsUvSelected);
        Assert.Equal("Uv", viewModel.DisplayModePersistenceKey);
        Assert.Equal("uv", viewModel.BadgeText);
    }

    [Fact]
    public void RestoreDisplayOptions_ShouldFallBackToWireframe_WhenGeometryHasNoUv()
    {
        var viewModel = new InspectorPreviewMeshViewModel();

        viewModel.RestoreDisplayOptions("Uv", null, null, null, null, null);
        viewModel.Apply(
            CreateMeshGeometry(hasUv: false),
            new MeshPreviewDisplayState("wire", "uv"));

        Assert.True(viewModel.IsWireframeSelected);
        Assert.Equal("Wireframe", viewModel.DisplayModePersistenceKey);
        Assert.Equal("wire", viewModel.BadgeText);
    }

    [Fact]
    public void RestoreDisplayOptions_ShouldApplyToolbarState_WhenGeometryAndTextureLoadLater()
    {
        var viewModel = new InspectorPreviewMeshViewModel();

        viewModel.RestoreDisplayOptions(
            "Uv",
            textureOverlaySelected: false,
            meshGridSelected: false,
            zoom: 1.5d,
            rotationX: 24d,
            rotationY: -72d);
        viewModel.Apply(
            CreateMeshGeometry(hasUv: true),
            new MeshPreviewDisplayState("wire", "uv"));
        viewModel.ApplyTextureOverlay([1, 2, 3, 4]);

        Assert.True(viewModel.IsUvSelected);
        Assert.False(viewModel.IsTextureOverlaySelected);
        Assert.False(viewModel.IsMeshGridSelected);
        Assert.Equal(1.5d, viewModel.Zoom);
        Assert.Equal(24d, viewModel.RotationX);
        Assert.Equal(-72d, viewModel.RotationY);
        Assert.Equal("X +24\u00B0  Y -72\u00B0", viewModel.RotationStatusText);
        Assert.False(viewModel.ShouldDrawTextureOverlay);
        Assert.True(viewModel.ShouldDrawMeshGrid);
    }

    [Fact]
    public void ToolbarChanges_ShouldRaiseDisplayOptionsChanged_ButRestoreShouldNot()
    {
        var viewModel = new InspectorPreviewMeshViewModel();
        var changeCount = 0;
        viewModel.DisplayOptionsChanged += (_, _) => changeCount++;

        viewModel.Apply(
            CreateMeshGeometry(hasUv: true),
            new MeshPreviewDisplayState("wire", "uv"));
        viewModel.ApplyTextureOverlay([1, 2, 3, 4]);
        viewModel.Rotate(10d, -12d);
        viewModel.ToggleDisplayMode();
        viewModel.ToggleMeshGrid();
        viewModel.ToggleTextureOverlay();
        viewModel.ZoomBy(1.5d);
        viewModel.ResetView();
        viewModel.RestoreDisplayOptions("Wireframe", true, true, 1d, -18d, 32d);

        Assert.Equal(8, changeCount);
        Assert.True(viewModel.IsWireframeSelected);
        Assert.Equal(-18d, viewModel.RotationX);
        Assert.Equal(32d, viewModel.RotationY);
    }

    [Fact]
    public void ToggleTextureOverlay_ShouldDrawOverlayInWireframe_WhenTrianglesAndTextureExist()
    {
        var viewModel = new InspectorPreviewMeshViewModel();

        viewModel.Apply(
            CreateMeshGeometry(hasUv: true, hasTriangles: true),
            new MeshPreviewDisplayState("wire", "uv"));
        viewModel.ApplyTextureOverlay([1, 2, 3, 4]);

        Assert.True(viewModel.HasTextureOverlay);
        Assert.True(viewModel.IsTextureOverlaySelected);
        Assert.True(viewModel.CanDrawTexturedMesh);
        Assert.Equal(Visibility.Visible, viewModel.TextureOverlaySwitchVisibility);
        Assert.True(viewModel.ShouldDrawTextureOverlay);
        Assert.Equal(Visibility.Visible, viewModel.MeshGridSwitchVisibility);
        Assert.True(viewModel.IsMeshGridSelected);
        Assert.True(viewModel.ShouldDrawMeshGrid);

        viewModel.ToggleDisplayMode();

        Assert.True(viewModel.IsUvSelected);
        Assert.True(viewModel.ShouldDrawTextureOverlay);
        Assert.Equal(Visibility.Visible, viewModel.MeshGridSwitchVisibility);
        Assert.True(viewModel.ShouldDrawMeshGrid);

        viewModel.ToggleTextureOverlay();

        Assert.False(viewModel.IsTextureOverlaySelected);
        Assert.False(viewModel.ShouldDrawTextureOverlay);
        Assert.Equal(Visibility.Collapsed, viewModel.MeshGridSwitchVisibility);
        Assert.True(viewModel.ShouldDrawMeshGrid);
    }

    [Fact]
    public void ToggleTextureOverlay_ShouldNotDrawWireframeOverlay_WhenTrianglesAreMissing()
    {
        var viewModel = new InspectorPreviewMeshViewModel();

        viewModel.Apply(
            CreateMeshGeometry(hasUv: true, hasTriangles: false),
            new MeshPreviewDisplayState("wire", "uv"));
        viewModel.ApplyTextureOverlay([1, 2, 3, 4]);

        Assert.True(viewModel.HasTextureOverlay);
        Assert.False(viewModel.CanDrawTexturedMesh);
        Assert.False(viewModel.ShouldDrawTextureOverlay);
        Assert.Equal(Visibility.Collapsed, viewModel.MeshGridSwitchVisibility);
        Assert.True(viewModel.ShouldDrawMeshGrid);

        viewModel.ToggleDisplayMode();

        Assert.True(viewModel.IsUvSelected);
        Assert.True(viewModel.ShouldDrawTextureOverlay);
        Assert.Equal(Visibility.Visible, viewModel.MeshGridSwitchVisibility);
    }

    [Fact]
    public void ToggleMeshGrid_ShouldHideStructureLayer_WhenTextureOverlayCanDraw()
    {
        var viewModel = new InspectorPreviewMeshViewModel();

        viewModel.Apply(
            CreateMeshGeometry(hasUv: true, hasTriangles: true),
            new MeshPreviewDisplayState("wire", "uv"));
        viewModel.ApplyTextureOverlay([1, 2, 3, 4]);

        Assert.True(viewModel.IsWireframeSelected);
        Assert.Equal(Visibility.Visible, viewModel.MeshGridSwitchVisibility);
        Assert.True(viewModel.ShouldDrawTextureOverlay);
        Assert.True(viewModel.ShouldDrawMeshGrid);

        viewModel.ToggleMeshGrid();

        Assert.False(viewModel.IsMeshGridSelected);
        Assert.False(viewModel.ShouldDrawMeshGrid);

        viewModel.ToggleDisplayMode();

        Assert.True(viewModel.IsUvSelected);
        Assert.Equal(Visibility.Visible, viewModel.MeshGridSwitchVisibility);
        Assert.False(viewModel.ShouldDrawMeshGrid);

        viewModel.ToggleTextureOverlay();

        Assert.False(viewModel.ShouldDrawTextureOverlay);
        Assert.Equal(Visibility.Collapsed, viewModel.MeshGridSwitchVisibility);
        Assert.True(viewModel.ShouldDrawMeshGrid);
    }

    [Fact]
    public void ToggleMeshGrid_ShouldHideUvStructureLayer_WhenTrianglesAreMissing()
    {
        var viewModel = new InspectorPreviewMeshViewModel();

        viewModel.Apply(
            CreateMeshGeometry(hasUv: true, hasTriangles: false),
            new MeshPreviewDisplayState("wire", "uv"));
        viewModel.ApplyTextureOverlay([1, 2, 3, 4]);

        Assert.Equal(Visibility.Collapsed, viewModel.MeshGridSwitchVisibility);

        viewModel.ToggleDisplayMode();

        Assert.True(viewModel.IsUvSelected);
        Assert.True(viewModel.ShouldDrawTextureOverlay);
        Assert.Equal(Visibility.Visible, viewModel.MeshGridSwitchVisibility);

        viewModel.ToggleMeshGrid();

        Assert.False(viewModel.IsMeshGridSelected);
        Assert.False(viewModel.ShouldDrawMeshGrid);
    }

    private static AssetPreviewMeshGeometryDto CreateMeshGeometry(
        bool hasUv,
        bool hasTriangles = true)
    {
        return new AssetPreviewMeshGeometryDto(
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
            Uv0: hasUv
                ? [
                    new AssetPreviewMeshUvDto(0, 0),
                    new AssetPreviewMeshUvDto(1, 0),
                    new AssetPreviewMeshUvDto(0, 1)
                ]
                : Array.Empty<AssetPreviewMeshUvDto>(),
            Channels: Array.Empty<AssetPreviewMeshChannelDto>(),
            Triangles: hasTriangles
                ? [new AssetPreviewMeshTriangleDto(0, 1, 2, 0)]
                : Array.Empty<AssetPreviewMeshTriangleDto>());
    }
}
