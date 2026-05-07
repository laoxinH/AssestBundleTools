using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class LoadSpineAssetGroupDetailsHandlerTests
{
    public static async Task HandleAsync_ShouldParseAtlasPagesAndRegions_FromPreviewPipeline()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var atlasAssetId = new AssetId("atlas-hero");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skeleton-hero", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, atlasAssetId.Value, "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/Hero.png", AssetType.Texture2D));
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    hero.png
                    size: 512,256
                    format: RGBA8888
                    head
                      rotate: false
                      xy: 12, 16
                      size: 32, 48
                    body
                      rotate: true
                      bounds: 64, 80, 96, 128
                    """));
            }

            return query.AssetId.Value == "texture-hero"
                ? Result<AssetPreviewDto>.Success(CreateImagePreview(query, 512, 256))
                : Result<AssetPreviewDto>.Failure(AssestBundleTools.Application.Common.Errors.ApplicationErrors.AssetNotFound(query.AssetId));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(new LoadSpineAssetGroupDetailsQuery(
            workspaceId,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: @"C:\WorkspaceData",
            spineBundleId,
            GroupId: "spine-group:bundle.spine:assets/hero/hero"));

        TestAssert.True(result.IsSuccess, "Spine group details should load through the preview pipeline.");
        TestAssert.Equal(2, previewHandler.HandleCallCount, "Atlas text and matched Texture2D image should be requested from the preview query.");
        var details = result.Value ?? throw new InvalidOperationException("Expected Spine group details.");
        var page = TestAssert.Single(details.AtlasPages);
        TestAssert.Equal("hero.png", page.Name, "Atlas page name should be parsed.");
        TestAssert.Equal(
            "texture-hero",
            page.TexturePageAsset?.AssetId.Value,
            "Atlas page should be matched back to its Texture2D asset for overlay loading.");
        TestAssert.Equal(512, page.Width, "Atlas page width should be parsed.");
        TestAssert.Equal(256, page.Height, "Atlas page height should be parsed.");
        TestAssert.Equal(2, page.Regions.Count, "Atlas regions should be parsed.");
        TestAssert.Equal("head", page.Regions[0].Name, "First region name should be parsed.");
        TestAssert.Equal(12, page.Regions[0].X, "Region xy should set x.");
        TestAssert.Equal(16, page.Regions[0].Y, "Region xy should set y.");
        TestAssert.Equal(32, page.Regions[0].Width, "Region size should set width.");
        TestAssert.False(page.Regions[0].IsRotated, "Boolean rotate false should be preserved.");
        TestAssert.Equal("body", page.Regions[1].Name, "Second region name should be parsed.");
        TestAssert.Equal(64, page.Regions[1].X, "Region bounds should set x.");
        TestAssert.Equal(128, page.Regions[1].Height, "Region bounds should set height.");
        TestAssert.True(page.Regions[1].IsRotated, "Boolean rotate true should be preserved.");
        TestAssert.Empty(details.Diagnostics, "A parsed complete group should not add diagnostics.");
    }

    public static async Task HandleAsync_ShouldParseJsonSkeletonSummary_FromPreviewPipeline()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var skeletonAssetId = new AssetId("skeleton-hero");
        var atlasAssetId = new AssetId("atlas-hero");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, skeletonAssetId.Value, "Assets/Hero/Hero.json", AssetType.TextAsset),
            CreateItem(spineBundleId, atlasAssetId.Value, "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/Hero.png", AssetType.Texture2D));
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(skeletonAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    {
                      "skeleton": { "spine": "4.1.24" },
                      "bones": [{ "name": "root" }, { "name": "hip" }],
                      "slots": [{ "name": "body" }, { "name": "head" }, { "name": "weapon" }],
                      "skins": [{ "name": "default" }],
                      "animations": { "idle": {}, "run": {}, "attack": {}, "hit": {} },
                      "events": { "footstep": {} },
                      "ik": [{ "name": "leg" }],
                      "transform": [{ "name": "squash" }],
                      "path": [{ "name": "tail" }]
                    }
                    """));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    hero.png
                    size: 512,256
                    head
                      xy: 0, 0
                      size: 32, 32
                    """));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 512, 256));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine JSON skeleton summary should load through the preview pipeline.");
        TestAssert.Equal(3, previewHandler.HandleCallCount, "Skeleton JSON, atlas text and Texture2D image should be requested.");
        var summary = TestAssert.Single(result.Value?.SkeletonSummaries ?? Array.Empty<SpineSkeletonSummaryDto>());
        TestAssert.Equal(SpineSkeletonAssetFormat.Json, summary.Format, "JSON skeleton assets should be identified.");
        TestAssert.Equal("4.1.24", summary.SpineVersion, "Spine version should come from skeleton metadata.");
        TestAssert.Equal(2, summary.BoneCount, "Bone count should be parsed from the JSON array.");
        TestAssert.Equal(3, summary.SlotCount, "Slot count should be parsed from the JSON array.");
        TestAssert.Equal(1, summary.SkinCount, "Skin count should be parsed from the JSON array.");
        TestAssert.Equal(4, summary.AnimationCount, "Animation count should be parsed from the JSON object.");
        TestAssert.Equal(1, summary.EventCount, "Event count should be parsed from the JSON object.");
        TestAssert.Equal(1, summary.IkConstraintCount, "IK constraint count should be parsed.");
        TestAssert.Equal(1, summary.TransformConstraintCount, "Transform constraint count should be parsed.");
        TestAssert.Equal(1, summary.PathConstraintCount, "Path constraint count should be parsed.");
        TestAssert.Empty(result.Value!.Diagnostics, "A valid JSON skeleton should not add skeleton diagnostics.");
    }

    public static async Task HandleAsync_ShouldReportAtlasPageTextureUnmatched_WhenPageHasNoTextureAsset()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var atlasAssetId = new AssetId("atlas-hero");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skeleton-hero", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, atlasAssetId.Value, "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-other", "Assets/Hero/Other.png", AssetType.Texture2D));
        var previewHandler = new TestAssetPreviewHandler(query =>
            Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                hero.png
                size: 512,256
                head
                  xy: 0, 0
                  size: 32, 32
                """)));
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine group details should still load when an atlas page is unmatched.");
        var diagnostic = FindDiagnostic(
            result.Value?.Diagnostics ?? Array.Empty<SpineAssetGroupDiagnosticDto>(),
            SpineAssetGroupDiagnosticKind.AtlasPageTextureUnmatched);
        TestAssert.Equal(
            SpineAssetGroupDiagnosticKind.AtlasPageTextureUnmatched,
            diagnostic.Kind,
            "Unmatched atlas pages should produce a page-level diagnostic.");
        TestAssert.Equal("hero.png", diagnostic.AtlasPageName, "Diagnostic should preserve the atlas page name.");
        TestAssert.Equal(512, diagnostic.AtlasPageWidth, "Diagnostic should preserve the atlas page width.");
        TestAssert.Equal(256, diagnostic.AtlasPageHeight, "Diagnostic should preserve the atlas page height.");
    }

    public static async Task HandleAsync_ShouldReportTexturePreviewUnavailable_WhenMatchedTextureHasNoImagePreview()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var atlasAssetId = new AssetId("atlas-hero");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skeleton-hero", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, atlasAssetId.Value, "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/Hero.png", AssetType.Texture2D));
        var previewHandler = new TestAssetPreviewHandler(query =>
            query.AssetId.Equals(atlasAssetId)
                ? Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    hero.png
                    size: 512,256
                    head
                      xy: 0, 0
                      size: 32, 32
                    """))
                : Result<AssetPreviewDto>.Success(CreateSummaryPreview(query, "metadata only")));
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine group details should keep loading when texture preview is unavailable.");
        var diagnostic = TestAssert.Single(result.Value?.Diagnostics ?? Array.Empty<SpineAssetGroupDiagnosticDto>());
        TestAssert.Equal(
            SpineAssetGroupDiagnosticKind.TexturePreviewUnavailable,
            diagnostic.Kind,
            "Matched Texture2D assets without image previews should be reported.");
        TestAssert.Equal(2, diagnostic.RelatedParts.Count, "Texture preview diagnostics should retain atlas and texture context.");
    }

    public static async Task HandleAsync_ShouldReportAtlasPageTextureSizeMismatch_WhenImageDimensionsDiffer()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var atlasAssetId = new AssetId("atlas-hero");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skeleton-hero", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, atlasAssetId.Value, "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/Hero.png", AssetType.Texture2D));
        var previewHandler = new TestAssetPreviewHandler(query =>
            query.AssetId.Equals(atlasAssetId)
                ? Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    hero.png
                    size: 512,256
                    head
                      xy: 0, 0
                      size: 32, 32
                    """))
                : Result<AssetPreviewDto>.Success(CreateImagePreview(query, 1024, 256)));
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine group details should keep loading when dimensions are inconsistent.");
        var diagnostic = TestAssert.Single(result.Value?.Diagnostics ?? Array.Empty<SpineAssetGroupDiagnosticDto>());
        TestAssert.Equal(
            SpineAssetGroupDiagnosticKind.AtlasPageTextureSizeMismatch,
            diagnostic.Kind,
            "Page and decoded image dimension mismatch should be reported.");
        TestAssert.Equal(512, diagnostic.AtlasPageWidth, "Diagnostic should preserve atlas page width.");
        TestAssert.Equal(256, diagnostic.AtlasPageHeight, "Diagnostic should preserve atlas page height.");
        TestAssert.Equal(1024, diagnostic.TextureWidth, "Diagnostic should preserve texture preview width.");
        TestAssert.Equal(256, diagnostic.TextureHeight, "Diagnostic should preserve texture preview height.");
    }

    public static async Task HandleAsync_ShouldReportAtlasPreviewUnavailable_WhenAtlasPreviewIsNotText()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, "skeleton-hero", "Assets/Hero/Hero.skel", AssetType.TextAsset),
            CreateItem(spineBundleId, "atlas-hero", "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/Hero.png", AssetType.Texture2D));
        var previewHandler = new TestAssetPreviewHandler(query =>
            Result<AssetPreviewDto>.Success(CreateSummaryPreview(query, "summary fallback")));
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(new LoadSpineAssetGroupDetailsQuery(
            workspaceId,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: @"C:\WorkspaceData",
            spineBundleId,
            GroupId: "spine-group:bundle.spine:assets/hero/hero"));

        TestAssert.True(result.IsSuccess, "Preview fallback should keep the group details query successful.");
        var details = result.Value ?? throw new InvalidOperationException("Expected Spine group details.");
        TestAssert.Empty(details.AtlasPages, "Unavailable atlas text should not emit parsed pages.");
        var diagnostic = TestAssert.Single(details.Diagnostics);
        TestAssert.Equal(
            SpineAssetGroupDiagnosticKind.AtlasPreviewUnavailable,
            diagnostic.Kind,
            "Unavailable text preview should be reported as a structured diagnostic.");
    }

}
