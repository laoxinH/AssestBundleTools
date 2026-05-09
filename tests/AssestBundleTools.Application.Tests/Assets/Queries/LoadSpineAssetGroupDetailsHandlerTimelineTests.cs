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
    public static async Task HandleAsync_ShouldApplyJsonDrawOrderAndSlotColorTimelines()
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
                      "skeleton": { "spine": "4.3.0" },
                      "bones": [{ "name": "root" }],
                      "slots": [
                        { "name": "back-slot", "bone": "root", "attachment": "back" },
                        { "name": "front-slot", "bone": "root", "attachment": "front" }
                      ],
                      "skins": {
                        "default": {
                          "back-slot": {
                            "back": { "type": "region", "path": "back", "width": 32, "height": 32 }
                          },
                          "front-slot": {
                            "front": { "type": "region", "path": "front", "width": 32, "height": 32 }
                          }
                        }
                      },
                      "animations": {
                        "swap": {
                          "slots": {
                            "back-slot": {
                              "color": [{ "time": 0, "color": "ff000080" }]
                            }
                          },
                          "drawOrder": [
                            { "time": 0, "offsets": [{ "slot": "back-slot", "offset": 1 }] }
                          ]
                        }
                      }
                    }
                    """));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    Hero.png
                    size: 128,128
                    back
                      xy: 0, 0
                      size: 32, 32
                    front
                      xy: 32, 0
                      size: 32, 32
                    """));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 128, 128));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Timeline preview should remain a successful diagnostic-light read.");
        var animation = TestAssert.Single(result.Value!.AnimationPreview!.Animations);
        TestAssert.Single(animation.SlotColorTimelines);
        TestAssert.Single(animation.DrawOrderTimelineFrames);
        var commands = animation.SampledFrames[0].DrawCommands;
        TestAssert.Equal("front-slot", commands[0].SlotName, "Draw order timeline should move the back slot above the front slot.");
        TestAssert.Equal("back-slot", commands[1].SlotName, "Draw order timeline should preserve the shifted slot as the later draw command.");
        var tintedCommand = commands[1];
        TestAssert.True(Math.Abs(tintedCommand.TintR - 1d) < 0.001d, "Slot color should preserve red.");
        TestAssert.True(Math.Abs(tintedCommand.TintG) < 0.001d, "Slot color should suppress green.");
        TestAssert.True(Math.Abs(tintedCommand.TintB) < 0.001d, "Slot color should suppress blue.");
        TestAssert.True(Math.Abs(tintedCommand.TintA - (128d / 255d)) < 0.001d, "Slot color should preserve alpha.");
    }
}
