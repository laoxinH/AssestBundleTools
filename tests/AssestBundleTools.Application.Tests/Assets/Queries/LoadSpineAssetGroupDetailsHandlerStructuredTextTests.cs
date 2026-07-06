using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class LoadSpineAssetGroupDetailsHandlerTests
{
    public static async Task HandleAsync_ShouldParseAtlasFromFullTextContent_WhenDisplayTextIsBounded()
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
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/hero.png", AssetType.Texture2D));
        var boundedAtlasText = """
            wrong.png
            size: 1,1
            wrong
              xy: 0, 0
              size: 1, 1
            """;
        var fullAtlasText = """
            hero.png
            size: 512,256
            head
              xy: 12, 16
              size: 32, 48
            """;
        var skeletonJson = """
            {
              "skeleton": { "spine": "3.8.99" },
              "bones": [{ "name": "root" }],
              "slots": [],
              "skins": {},
              "animations": {}
            }
            """;
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(skeletonAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, skeletonJson));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(
                    query,
                    boundedAtlasText,
                    fullAtlasText));
            }

            return query.AssetId.Value == "texture-hero"
                ? Result<AssetPreviewDto>.Success(CreateImagePreview(query, 512, 256))
                : Result<AssetPreviewDto>.Failure(ApplicationErrors.AssetNotFound(query.AssetId));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine atlas details should load from the full structured text payload.");
        var details = result.Value ?? throw new InvalidOperationException("Expected Spine group details.");
        var page = TestAssert.Single(details.AtlasPages);
        TestAssert.Equal("hero.png", page.Name, "Atlas parsing should prefer FullTextContent over bounded display text.");
        TestAssert.Equal(512, page.Width, "Atlas page width should come from the full atlas text.");
        TestAssert.Equal(256, page.Height, "Atlas page height should come from the full atlas text.");
        TestAssert.Equal("texture-hero", page.TexturePageAsset?.AssetId.Value, "Texture matching should use the full atlas page name.");
        var region = TestAssert.Single(page.Regions);
        TestAssert.Equal("head", region.Name, "Regions should come from the full atlas text.");
    }

    public static async Task HandleAsync_ShouldRejectLargeJsonAtlasText_WithoutRuntimePreview()
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
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/hero.png", AssetType.Texture2D));
        var oversizedJson = "{" + new string('a', 1_050_000) + "}";
        var skeletonJson = """
            {
              "skeleton": { "spine": "4.2.74" },
              "bones": [{ "name": "root" }],
              "slots": [{ "name": "slot", "bone": "root", "attachment": "body" }],
              "skins": { "default": { "slot": { "body": { "type": "region", "path": "body" } } } },
              "animations": { "idle": {} }
            }
            """;
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(skeletonAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, skeletonJson));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(
                    query,
                    oversizedJson[..24000],
                    oversizedJson));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 512, 256));
        });
        var runtimePreviewService = new TestSpineAnimationRuntimePreviewService(_ =>
            throw new InvalidOperationException("Invalid atlas text should not reach the runtime preview adapter."));
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler,
            runtimePreviewService);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Invalid large atlas text should finish with diagnostics instead of hanging preview loading.");
        var details = result.Value ?? throw new InvalidOperationException("Expected Spine group details.");
        TestAssert.Empty(details.AtlasPages, "Oversized JSON should not be parsed as an atlas page.");
        TestAssert.Equal(2, previewHandler.HandleCallCount, "Only skeleton summary and atlas text should be read; texture/runtime preview should be skipped.");
        TestAssert.Equal(0, runtimePreviewService.HandleCallCount, "Runtime adapter should not receive invalid atlas text.");
        var diagnostic = TestAssert.Single(details.Diagnostics);
        TestAssert.Equal(
            SpineAssetGroupDiagnosticKind.AtlasParseFailed,
            diagnostic.Kind,
            "Invalid JSON supplied as an atlas replacement should be reported as an atlas parse diagnostic.");
    }

    public static async Task HandleAsync_ShouldRejectTruncatedAtlasDisplayText_WhenFullTextIsUnavailable()
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
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/hero.png", AssetType.Texture2D));
        var truncatedAtlasText = """
            hero.png
            size: 512,256
            head
              xy: 12, 16
            """;
        var skeletonJson = """
            {
              "skeleton": { "spine": "3.8.99" },
              "bones": [{ "name": "root" }],
              "slots": [],
              "skins": {},
              "animations": {}
            }
            """;
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(skeletonAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, skeletonJson));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(
                    query,
                    truncatedAtlasText,
                    isDisplayTruncated: true));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 512, 256));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Truncated atlas display text should finish with diagnostics instead of parsing a partial atlas.");
        var details = result.Value ?? throw new InvalidOperationException("Expected Spine group details.");
        TestAssert.Empty(details.AtlasPages, "Truncated display text must not be parsed as complete atlas content.");
        TestAssert.Equal(2, previewHandler.HandleCallCount, "Texture preview should be skipped when atlas text is incomplete.");
        var diagnostic = TestAssert.Single(details.Diagnostics);
        TestAssert.Equal(
            SpineAssetGroupDiagnosticKind.AtlasPreviewUnavailable,
            diagnostic.Kind,
            "Missing full atlas text should be reported as unavailable rather than parsed from a display sample.");
    }

    public static async Task HandleAsync_ShouldParseAtlasFromFullContentReference_WhenDisplayTextIsTruncated()
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
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/hero.png", AssetType.Texture2D));
        var displayAtlasText = "hero.png\nsize: 512,256\n";
        var fullAtlasText = """
            hero.png
            size: 512,256
            head
              xy: 12, 16
              size: 32, 48
            """;
        var skeletonJson = """
            {
              "skeleton": { "spine": "3.8.99" },
              "bones": [{ "name": "root" }],
              "slots": [],
              "skins": {},
              "animations": {}
            }
            """;
        var atlasReference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            "text/plain",
            fullAtlasText.Length,
            "workspace-key",
            PreviewCacheKey: "atlas-cache");
        var chunkHandler = new TestPreviewTextChunkHandler();
        chunkHandler.Add("atlas-cache", fullAtlasText);
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(skeletonAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, skeletonJson));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(
                    query,
                    displayAtlasText,
                    isDisplayTruncated: true,
                    fullContentReference: atlasReference));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 512, 256));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler,
            chunkHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine atlas details should load full atlas text from the preview content reference.");
        var details = result.Value ?? throw new InvalidOperationException("Expected Spine group details.");
        var page = TestAssert.Single(details.AtlasPages);
        TestAssert.Equal("hero.png", page.Name, "Atlas page should come from reference-backed full text.");
        var region = TestAssert.Single(page.Regions);
        TestAssert.Equal("head", region.Name, "Atlas regions should come from reference-backed full text.");
        TestAssert.True(chunkHandler.HandleCallCount > 0, "Reference-backed atlas parsing should read through the chunk query.");
    }

    public static async Task HandleAsync_ShouldPassReferenceBackedTextSources_ToRuntimePreview()
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
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/hero.png", AssetType.Texture2D));
        var atlasText = """
            hero.png
            size: 512,256
            head
              xy: 12, 16
              size: 32, 48
            """;
        var skeletonJson = """
            {
              "skeleton": { "spine": "3.8.99" },
              "bones": [{ "name": "root" }],
              "slots": [],
              "skins": {},
              "animations": {}
            }
            """;
        var atlasReference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            "text/plain",
            atlasText.Length,
            "workspace-key",
            PreviewCacheKey: "atlas-cache");
        var skeletonReference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.PreviewCache,
            "application/json",
            skeletonJson.Length,
            "workspace-key",
            PreviewCacheKey: "skeleton-cache");
        var chunkHandler = new TestPreviewTextChunkHandler();
        chunkHandler.Add("atlas-cache", atlasText);
        chunkHandler.Add("skeleton-cache", skeletonJson);
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(skeletonAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(
                    query,
                    skeletonJson[..Math.Min(24, skeletonJson.Length)],
                    isDisplayTruncated: true,
                    fullContentReference: skeletonReference));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(
                    query,
                    atlasText[..Math.Min(24, atlasText.Length)],
                    isDisplayTruncated: true,
                    fullContentReference: atlasReference));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 512, 256));
        });
        var runtimePreviewService = new TestSpineAnimationRuntimePreviewService(request =>
        {
            TestAssert.Equal(null, request.SkeletonJson, "Reference-backed skeleton text should not be forced into the runtime request string.");
            TestAssert.True(request.SkeletonJsonSource is not null, "Runtime request should carry a skeleton text source.");
            TestAssert.Equal(1, request.AtlasTextSources?.Count ?? 0, "Runtime request should carry atlas text sources.");
            return SpineAnimationRuntimePreviewResultDto.NotHandled;
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler,
            chunkHandler,
            runtimePreviewService);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Reference-backed Spine details should complete.");
        TestAssert.Equal(1, runtimePreviewService.HandleCallCount, "Runtime preview should receive the reference-backed text sources.");
    }
}
