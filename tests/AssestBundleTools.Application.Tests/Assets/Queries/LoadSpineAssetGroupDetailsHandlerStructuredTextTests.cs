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
}
