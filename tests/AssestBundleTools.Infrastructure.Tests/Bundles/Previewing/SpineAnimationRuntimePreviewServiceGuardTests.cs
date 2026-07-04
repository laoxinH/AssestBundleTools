using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static partial class SpineAnimationRuntimePreviewServiceTests
{
    public static async Task TryBuildPreviewAsync_ShouldReturnNotHandled_WhenRuntimeMetadataIsMissing()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.skel"),
            SkeletonJson: null,
            Runtime: null!,
            AtlasPages: Array.Empty<SpineAtlasPageDto>(),
            AtlasTexts: Array.Empty<string>(),
            SkeletonBinaryContent: [1, 2, 3]));

        TestAssert.False(result.IsHandled, "Missing runtime metadata should be treated as an unhandled preview.");
        TestAssert.False(result.IsCancelled, "Missing runtime metadata should not be treated as cancellation.");
    }

    public static async Task TryBuildPreviewAsync_ShouldReturnNotHandled_WhenV42ParserReturnsNullSkeletonData()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.json"),
            SkeletonJson: """
                {
                  "skeleton": { "hash": "", "spine": "4.2.74" },
                  "bones": [{ "name": "root" }],
                  "slots": [
                    { "name": "body-slot", "bone": "root", "attachment": "missing-region" }
                  ],
                  "skins": [
                    {
                      "name": "default",
                      "attachments": {
                        "body-slot": {
                          "missing-region": {
                            "type": "region",
                            "path": "missing-region",
                            "width": 32,
                            "height": 32
                          }
                        }
                      }
                    }
                  ],
                  "animations": { "idle": {} }
                }
                """,
            Runtime: new SpineAnimationRuntimeDto(
                SpineVersion: "4.2.74",
                Major: 4,
                Minor: 2,
                AdapterKey: "V42",
                SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable),
            AtlasPages: Array.Empty<SpineAtlasPageDto>(),
            AtlasTexts:
            [
                """
                Hero.png
                size: 128,128
                format: RGBA8888
                filter: Linear,Linear
                repeat: none
                other-region
                  rotate: false
                  xy: 0, 0
                  size: 32, 32
                  orig: 32, 32
                  offset: 0, 0
                  index: -1
                """
            ]));

        TestAssert.False(result.IsHandled, "Null skeleton data from the V42 parser should be treated as unhandled.");
        TestAssert.False(result.IsCancelled, "Null skeleton data from the V42 parser should not be treated as cancellation.");
    }

    public static async Task TryBuildPreviewAsync_ShouldRejectOversizedJsonAtlasBeforeRuntimeParse()
    {
        var service = new SpineAnimationRuntimePreviewService();
        var oversizedJsonAtlas = "{" + new string('a', 1_050_000) + "}";

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.json"),
            SkeletonJson: """
                {
                  "skeleton": { "spine": "4.2.74" },
                  "bones": [{ "name": "root" }],
                  "slots": [
                    { "name": "body-slot", "bone": "root", "attachment": "body" }
                  ],
                  "skins": [
                    {
                      "name": "default",
                      "attachments": {
                        "body-slot": {
                          "body": {
                            "type": "region",
                            "path": "body",
                            "width": 32,
                            "height": 32
                          }
                        }
                      }
                    }
                  ],
                  "animations": { "idle": {} }
                }
                """,
            Runtime: new SpineAnimationRuntimeDto(
                SpineVersion: "4.2.74",
                Major: 4,
                Minor: 2,
                AdapterKey: "V42",
                SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable),
            AtlasPages: Array.Empty<SpineAtlasPageDto>(),
            AtlasTexts: [oversizedJsonAtlas]));

        TestAssert.False(result.IsHandled, "Oversized JSON atlas input should be rejected before entering the official runtime parser.");
        TestAssert.False(result.IsCancelled, "Rejected oversized atlas input should not be treated as cancellation.");
    }
}
