using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SpineAnimationVertexBatchProjectorTests
{
    [Fact]
    public void Project_ShouldPreserveDrawOrderAcrossTextureRuns()
    {
        var frame = new SpineAnimationFrameDto(
            0d,
            [
                CreateRegionCommand("slot-a", "page-a.png"),
                CreateRegionCommand("slot-b", "page-b.png"),
                CreateRegionCommand("slot-c", "page-a.png")
            ]);

        var batches = SpineAnimationVertexBatchProjector.Project(frame, CreateTexturePages());

        Assert.Equal(["page-a.png", "page-b.png", "page-a.png"], batches.Select(static batch => batch.PageName));
        Assert.All(batches, static batch =>
        {
            Assert.Equal(4, batch.Vertices.Count);
            Assert.Equal([0, 1, 2, 0, 2, 3], batch.Indices);
        });
    }

    [Fact]
    public void Project_ShouldProjectRegionTransformUvAndTint()
    {
        var command = CreateRegionCommand("slot", "page-a.png") with
        {
            SourceX = 10,
            SourceY = 20,
            SourceWidth = 30,
            SourceHeight = 40,
            Width = 2d,
            Height = 4d,
            TransformM31 = 5d,
            TransformM32 = 6d,
            TintR = 0.2d,
            TintG = 0.3d,
            TintB = 0.4d,
            TintA = 0.5d
        };
        var frame = new SpineAnimationFrameDto(0d, [command]);

        var batch = Assert.Single(SpineAnimationVertexBatchProjector.Project(frame, CreateTexturePages()));

        Assert.Equal([0, 1, 2, 0, 2, 3], batch.Indices);
        AssertVertex(batch.Vertices[0], x: 4f, y: 4f, u: 0.1f, v: 0.1f);
        AssertVertex(batch.Vertices[1], x: 6f, y: 4f, u: 0.4f, v: 0.1f);
        AssertVertex(batch.Vertices[2], x: 6f, y: 8f, u: 0.4f, v: 0.3f);
        AssertVertex(batch.Vertices[3], x: 4f, y: 8f, u: 0.1f, v: 0.3f);
        Assert.All(batch.Vertices, static vertex =>
        {
            Assert.Equal(0.2f, vertex.R, precision: 6);
            Assert.Equal(0.3f, vertex.G, precision: 6);
            Assert.Equal(0.4f, vertex.B, precision: 6);
            Assert.Equal(0.5f, vertex.A, precision: 6);
        });
    }

    [Fact]
    public void Project_ShouldUseMeshVerticesAndSkipInvalidTriangles()
    {
        var command = CreateRegionCommand("mesh-slot", "page-a.png") with
        {
            MeshVertices =
            [
                new SpineAnimationMeshVertexDto(1d, 2d, 0.1d, 0.2d),
                new SpineAnimationMeshVertexDto(3d, 4d, 0.3d, 0.4d),
                new SpineAnimationMeshVertexDto(5d, 6d, 0.5d, 0.6d)
            ],
            MeshTriangles =
            [
                new SpineAnimationMeshTriangleDto(0, 1, 2),
                new SpineAnimationMeshTriangleDto(0, 2, 7)
            ],
            TintA = 0.25d
        };
        var frame = new SpineAnimationFrameDto(0d, [command]);

        var batch = Assert.Single(SpineAnimationVertexBatchProjector.Project(frame, CreateTexturePages()));

        Assert.Equal([0, 1, 2], batch.Indices);
        Assert.Equal(3, batch.Vertices.Count);
        AssertVertex(batch.Vertices[0], x: 1f, y: 2f, u: 0.1f, v: 0.2f);
        Assert.Equal(0.25f, batch.Vertices[0].A, precision: 6);
    }

    [Fact]
    public void ProjectClipSpace_ShouldCenterProjectedGeometryAndKeepTint()
    {
        var sourceBatch = new SpineAnimationVertexBatch(
            "page-a.png",
            [
                new SpineAnimationBatchVertex(-1f, -1f, 0f, 0f, 0.2f, 0.3f, 0.4f, 0.5f),
                new SpineAnimationBatchVertex(1f, 1f, 1f, 1f, 0.2f, 0.3f, 0.4f, 0.5f)
            ],
            [0, 1, 1]);

        var batch = Assert.Single(SpineAnimationClipSpaceProjector.Project([sourceBatch], 100f, 100f));

        Assert.Equal("page-a.png", batch.PageName);
        Assert.Equal([0, 1, 1], batch.Indices);
        AssertClipVertex(batch.Vertices[0], x: -0.72f, y: -0.72f, u: 0f, v: 0f);
        AssertClipVertex(batch.Vertices[1], x: 0.72f, y: 0.72f, u: 1f, v: 1f);
        Assert.All(batch.Vertices, static vertex =>
        {
            Assert.Equal(0.2f, vertex.R, precision: 6);
            Assert.Equal(0.3f, vertex.G, precision: 6);
            Assert.Equal(0.4f, vertex.B, precision: 6);
            Assert.Equal(0.5f, vertex.A, precision: 6);
        });
    }

    [Fact]
    public void ProjectClipSpace_ShouldReturnEmpty_WhenViewportIsInvalid()
    {
        var sourceBatch = new SpineAnimationVertexBatch(
            "page-a.png",
            [new SpineAnimationBatchVertex(0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f)],
            [0, 0, 0]);

        var batches = SpineAnimationClipSpaceProjector.Project([sourceBatch], 1f, 100f);

        Assert.Empty(batches);
    }

    private static SpineAnimationDrawCommandDto CreateRegionCommand(string slotName, string pageName) =>
        new(
            slotName,
            "attachment",
            "region",
            pageName,
            SourceX: 0,
            SourceY: 0,
            SourceWidth: 10,
            SourceHeight: 10,
            Width: 10d,
            Height: 10d,
            TransformM11: 1d,
            TransformM12: 0d,
            TransformM21: 0d,
            TransformM22: 1d,
            TransformM31: 0d,
            TransformM32: 0d);

    private static IReadOnlyDictionary<string, SpineAnimationTexturePageSize> CreateTexturePages() =>
        new Dictionary<string, SpineAnimationTexturePageSize>(StringComparer.Ordinal)
        {
            ["page-a.png"] = new(100, 200),
            ["page-b.png"] = new(100, 200)
        };

    private static void AssertVertex(
        SpineAnimationBatchVertex vertex,
        float x,
        float y,
        float u,
        float v)
    {
        Assert.Equal(x, vertex.X, precision: 6);
        Assert.Equal(y, vertex.Y, precision: 6);
        Assert.Equal(u, vertex.U, precision: 6);
        Assert.Equal(v, vertex.V, precision: 6);
    }

    private static void AssertClipVertex(
        SpineAnimationClipSpaceVertex vertex,
        float x,
        float y,
        float u,
        float v)
    {
        Assert.Equal(x, vertex.X, precision: 6);
        Assert.Equal(y, vertex.Y, precision: 6);
        Assert.Equal(u, vertex.U, precision: 6);
        Assert.Equal(v, vertex.V, precision: 6);
    }
}
