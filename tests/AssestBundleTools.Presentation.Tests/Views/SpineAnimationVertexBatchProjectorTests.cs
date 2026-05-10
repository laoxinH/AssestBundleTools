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
    public void ProjectClipSpace_ShouldIgnoreFullyTransparentVerticesForAutoFit()
    {
        var invisibleBatch = new SpineAnimationVertexBatch(
            "page-a.png",
            [
                new SpineAnimationBatchVertex(-100f, -100f, 0f, 0f, 1f, 1f, 1f, 0f),
                new SpineAnimationBatchVertex(100f, 100f, 1f, 1f, 1f, 1f, 1f, 0f)
            ],
            [0, 1, 1]);
        var visibleBatch = new SpineAnimationVertexBatch(
            "page-a.png",
            [
                new SpineAnimationBatchVertex(-1f, -1f, 0f, 0f, 1f, 1f, 1f, 1f),
                new SpineAnimationBatchVertex(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f)
            ],
            [0, 1, 1]);

        var batches = SpineAnimationClipSpaceProjector.Project([invisibleBatch, visibleBatch], 100f, 100f);

        Assert.Equal(2, batches.Count);
        var batch = batches[1];
        AssertClipVertex(batch.Vertices[0], x: -0.72f, y: -0.72f, u: 0f, v: 0f);
        AssertClipVertex(batch.Vertices[1], x: 0.72f, y: 0.72f, u: 1f, v: 1f);
    }

    [Fact]
    public void ProjectClipSpace_ShouldFitWideGeometryAgainstViewportWidth()
    {
        var sourceBatch = new SpineAnimationVertexBatch(
            "page-a.png",
            [
                new SpineAnimationBatchVertex(-2f, -1f, 0f, 0f, 1f, 1f, 1f, 1f),
                new SpineAnimationBatchVertex(2f, 1f, 1f, 1f, 1f, 1f, 1f, 1f)
            ],
            [0, 1, 1]);

        var batch = Assert.Single(SpineAnimationClipSpaceProjector.Project([sourceBatch], 200f, 100f));

        AssertClipVertex(batch.Vertices[0], x: -0.72f, y: -0.72f, u: 0f, v: 0f);
        AssertClipVertex(batch.Vertices[1], x: 0.72f, y: 0.72f, u: 1f, v: 1f);
    }

    [Fact]
    public void ProjectClipSpace_ShouldCenterRegionByVisibleTextureAlphaBounds()
    {
        var frame = new SpineAnimationFrameDto(
            0d,
            [
                CreateRegionCommand("slot", "page-a.png") with
                {
                    SourceWidth = 100,
                    SourceHeight = 100,
                    Width = 100d,
                    Height = 100d
                }
            ]);
        var texturePages = new Dictionary<string, SpineAnimationTexturePageSize>(StringComparer.Ordinal)
        {
            ["page-a.png"] = CreateTexturePageWithAlpha(
                width: 100,
                height: 100,
                visibleLeft: 0,
                visibleTop: 0,
                visibleRight: 50,
                visibleBottom: 50)
        };

        var vertexBatches = SpineAnimationVertexBatchProjector.Project(frame, texturePages);
        var batch = Assert.Single(SpineAnimationClipSpaceProjector.Project(vertexBatches, 100f, 100f));

        AssertClipVertex(batch.Vertices[0], x: -0.72f, y: -0.72f, u: 0f, v: 0f);
        AssertClipVertex(batch.Vertices[2], x: 2.16f, y: 2.16f, u: 1f, v: 1f);
    }

    [Fact]
    public void ProjectClipSpace_ShouldIgnoreMeshTrianglesWithTransparentTextureAlphaBounds()
    {
        var frame = new SpineAnimationFrameDto(
            0d,
            [
                CreateRegionCommand("mesh-slot", "page-a.png") with
                {
                    MeshVertices =
                    [
                        new SpineAnimationMeshVertexDto(-100d, -100d, 0d, 0d),
                        new SpineAnimationMeshVertexDto(100d, -100d, 0.4d, 0d),
                        new SpineAnimationMeshVertexDto(100d, 100d, 0.4d, 0.4d),
                        new SpineAnimationMeshVertexDto(-1d, -1d, 0.8d, 0.8d),
                        new SpineAnimationMeshVertexDto(1d, -1d, 1d, 0.8d),
                        new SpineAnimationMeshVertexDto(1d, 1d, 1d, 1d)
                    ],
                    MeshTriangles =
                    [
                        new SpineAnimationMeshTriangleDto(0, 1, 2),
                        new SpineAnimationMeshTriangleDto(3, 4, 5)
                    ]
                }
            ]);
        var texturePages = new Dictionary<string, SpineAnimationTexturePageSize>(StringComparer.Ordinal)
        {
            ["page-a.png"] = CreateTexturePageWithAlpha(
                width: 100,
                height: 100,
                visibleLeft: 80,
                visibleTop: 80,
                visibleRight: 100,
                visibleBottom: 100)
        };

        var vertexBatches = SpineAnimationVertexBatchProjector.Project(frame, texturePages);
        var batch = Assert.Single(SpineAnimationClipSpaceProjector.Project(vertexBatches, 100f, 100f));

        Assert.Equal(0.8f, batch.Vertices[3].U, precision: 6);
        Assert.Equal(0.8f, batch.Vertices[3].V, precision: 6);
        Assert.Equal(1f, batch.Vertices[5].U, precision: 6);
        Assert.Equal(1f, batch.Vertices[5].V, precision: 6);
        Assert.True(batch.Vertices[3].X < -0.2f, "Transparent mesh triangles should not keep the visible triangle pinned near the center.");
        Assert.True(batch.Vertices[5].X > 0.2f, "Transparent mesh triangles should not keep the visible triangle pinned near the center.");
    }

    [Fact]
    public void ProjectClipSpace_ShouldCenterMeshByVisibleTriangleAlphaBounds()
    {
        var frame = new SpineAnimationFrameDto(
            0d,
            [
                CreateRegionCommand("mesh-slot", "page-a.png") with
                {
                    MeshVertices =
                    [
                        new SpineAnimationMeshVertexDto(-100d, -100d, 0d, 0d),
                        new SpineAnimationMeshVertexDto(100d, -100d, 1d, 0d),
                        new SpineAnimationMeshVertexDto(-100d, 100d, 0d, 1d)
                    ],
                    MeshTriangles =
                    [
                        new SpineAnimationMeshTriangleDto(0, 1, 2)
                    ]
                }
            ]);
        var texturePages = new Dictionary<string, SpineAnimationTexturePageSize>(StringComparer.Ordinal)
        {
            ["page-a.png"] = CreateTexturePageWithAlpha(
                width: 100,
                height: 100,
                visibleLeft: 0,
                visibleTop: 0,
                visibleRight: 20,
                visibleBottom: 20)
        };

        var vertexBatches = SpineAnimationVertexBatchProjector.Project(frame, texturePages);
        var batch = Assert.Single(SpineAnimationClipSpaceProjector.Project(vertexBatches, 100f, 100f));

        AssertClipVertex(batch.Vertices[0], x: -0.72f, y: -0.72f, u: 0f, v: 0f);
        AssertClipVertex(batch.Vertices[1], x: 6.48f, y: -0.72f, u: 1f, v: 0f);
        AssertClipVertex(batch.Vertices[2], x: -0.72f, y: 6.48f, u: 0f, v: 1f);
    }

    [Fact]
    public void TextureAlphaMap_ShouldKeepFaintVisibleBounds()
    {
        var pageSize = CreateTexturePageWithFaintBoundsAndOpaqueArea();

        Assert.True(pageSize.AlphaMap!.TryResolveVisibleBounds(
            sourceX: 0,
            sourceY: 0,
            sourceWidth: 100,
            sourceHeight: 100,
            out var bounds));

        Assert.Equal(0, bounds.Left);
        Assert.Equal(0, bounds.Top);
        Assert.Equal(100, bounds.Right);
        Assert.Equal(100, bounds.Bottom);
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

    [Fact]
    public void ProjectClipSpace_ShouldApplyPreviewViewport()
    {
        var sourceBatch = new SpineAnimationVertexBatch(
            "page-a.png",
            [
                new SpineAnimationBatchVertex(-1f, -1f, 0f, 0f, 1f, 1f, 1f, 1f),
                new SpineAnimationBatchVertex(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f)
            ],
            [0, 1, 1]);

        var batch = Assert.Single(SpineAnimationClipSpaceProjector.Project(
            [sourceBatch],
            100f,
            100f,
            new SpineAnimationPreviewViewport(2d, 10d, -5d)));

        AssertClipVertex(batch.Vertices[0], x: -1.24f, y: -1.34f, u: 0f, v: 0f);
        AssertClipVertex(batch.Vertices[1], x: 1.64f, y: 1.54f, u: 1f, v: 1f);
    }

    [Fact]
    public void ProjectClipSpace_ShouldUseStableAutoFitBounds()
    {
        var sourceBatch = new SpineAnimationVertexBatch(
            "page-a.png",
            [
                new SpineAnimationBatchVertex(0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f),
                new SpineAnimationBatchVertex(2f, 2f, 1f, 1f, 1f, 1f, 1f, 1f)
            ],
            [0, 1, 1]);
        var stableBounds = new SpineAnimationAutoFitBounds(true, -1d, -1d, 1d, 1d);

        var batch = Assert.Single(SpineAnimationClipSpaceProjector.Project(
            [sourceBatch],
            100f,
            100f,
            autoFitBounds: stableBounds));

        AssertClipVertex(batch.Vertices[0], x: 0f, y: 0f, u: 0f, v: 0f);
        AssertClipVertex(batch.Vertices[1], x: 1.44f, y: 1.44f, u: 1f, v: 1f);
    }

    [Fact]
    public void StableAutoFitBounds_ShouldUseReferenceFrameUnionUntilReset()
    {
        var firstFrame = new SpineAnimationFrameDto(0d, []);
        var secondFrame = new SpineAnimationFrameDto(0.5d, []);
        var stableBounds = new SpineAnimationStableAutoFitBounds();
        stableBounds.SetReferenceFrames([firstFrame, secondFrame]);

        var changed = stableBounds.TryResolveBounds(
            [CreateBatch(100f, 100f, 102f, 102f)],
            frame => ReferenceEquals(frame, firstFrame)
                ? [CreateBatch(-2f, -1f, 0f, 1f)]
                : [CreateBatch(4f, -3f, 6f, 3f)],
            out var resolvedBounds);

        Assert.True(changed);
        Assert.Equal(-2d, resolvedBounds.MinX);
        Assert.Equal(-3d, resolvedBounds.MinY);
        Assert.Equal(6d, resolvedBounds.MaxX);
        Assert.Equal(3d, resolvedBounds.MaxY);

        changed = stableBounds.TryResolveBounds(
            [CreateBatch(100f, 100f, 102f, 102f)],
            _ => throw new InvalidOperationException("Reference frames should not be recomputed while bounds are stable."),
            out var reusedBounds);

        Assert.False(changed);
        Assert.Equal(resolvedBounds, reusedBounds);

        stableBounds.ResetBounds();
        stableBounds.ClearReferenceFrames();
        changed = stableBounds.TryResolveBounds(
            [CreateBatch(100f, 100f, 102f, 102f)],
            _ => throw new InvalidOperationException("No reference frames should be resolved after clearing them."),
            out var resetBounds);

        Assert.True(changed);
        Assert.Equal(100d, resetBounds.MinX);
        Assert.Equal(102d, resetBounds.MaxX);
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

    private static SpineAnimationVertexBatch CreateBatch(
        float minX,
        float minY,
        float maxX,
        float maxY) =>
        new(
            "page-a.png",
            [
                new SpineAnimationBatchVertex(minX, minY, 0f, 0f, 1f, 1f, 1f, 1f),
                new SpineAnimationBatchVertex(maxX, maxY, 1f, 1f, 1f, 1f, 1f, 1f)
            ],
            [0, 1, 1]);

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

    private static SpineAnimationTexturePageSize CreateTexturePageWithAlpha(
        int width,
        int height,
        int visibleLeft,
        int visibleTop,
        int visibleRight,
        int visibleBottom)
    {
        var pixels = new byte[checked(width * height * 4)];
        for (var y = visibleTop; y < visibleBottom; y++)
        {
            for (var x = visibleLeft; x < visibleRight; x++)
            {
                var index = ((y * width) + x) * 4;
                pixels[index] = byte.MaxValue;
                pixels[index + 1] = byte.MaxValue;
                pixels[index + 2] = byte.MaxValue;
                pixels[index + 3] = byte.MaxValue;
            }
        }

        return new SpineAnimationTexturePageSize(
            width,
            height,
            SpineAnimationTextureAlphaMap.Create(width, height, pixels));
    }

    private static SpineAnimationTexturePageSize CreateTexturePageWithFaintBoundsAndOpaqueArea()
    {
        const int width = 100;
        const int height = 100;
        var pixels = new byte[checked(width * height * 4)];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                SetPixelAlpha(pixels, width, x, y, alpha: 1);
            }
        }

        for (var y = 80; y < 90; y++)
        {
            for (var x = 80; x < 90; x++)
            {
                SetPixelAlpha(pixels, width, x, y, byte.MaxValue);
            }
        }

        return new SpineAnimationTexturePageSize(
            width,
            height,
            SpineAnimationTextureAlphaMap.Create(width, height, pixels));
    }

    private static void SetPixelAlpha(byte[] pixels, int width, int x, int y, byte alpha)
    {
        var index = ((y * width) + x) * 4;
        pixels[index] = byte.MaxValue;
        pixels[index + 1] = byte.MaxValue;
        pixels[index + 2] = byte.MaxValue;
        pixels[index + 3] = alpha;
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
