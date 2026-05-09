using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static partial class SpineAnimationRuntimePreviewServiceTests
{
    public static async Task TryBuildPreviewAsync_ShouldApplyV42MeshDeformTimeline()
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
                    { "name": "body-slot", "bone": "root", "attachment": "body-mesh" }
                  ],
                  "skins": [
                    {
                      "name": "default",
                      "attachments": {
                        "body-slot": {
                          "body-mesh": {
                            "type": "mesh",
                            "path": "body",
                            "uvs": [0, 0, 1, 0, 1, 1, 0, 1],
                            "triangles": [0, 1, 2, 2, 3, 0],
                            "vertices": [-32, -64, 32, -64, 32, 64, -32, 64],
                            "hull": 4,
                            "width": 64,
                            "height": 128
                          }
                        }
                      }
                    }
                  ],
                  "animations": {
                    "deform": {
                      "attachments": {
                        "default": {
                          "body-slot": {
                            "body-mesh": {
                              "deform": [
                                { "time": 0, "vertices": [0, 0, 0, 0, 0, 0, 0, 0] },
                                { "time": 1, "vertices": [16, 0, 16, 0, 16, 0, 16, 0] }
                              ]
                            }
                          }
                        }
                      }
                    }
                  }
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
                body
                  rotate: false
                  xy: 0, 0
                  size: 32, 32
                  orig: 32, 32
                  offset: 0, 0
                  index: -1
                """
            ]));

        TestAssert.True(result.IsHandled, "V42 adapter should handle mesh deform timelines.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        var animation = TestAssert.Single(preview.Animations);
        TestAssert.True(animation.SampledFrames.Count >= 2, "V42 deform timeline should produce sampled frames.");
        var setupVertex = preview.SetupFrame!.DrawCommands[0].MeshVertices[0];
        var firstVertex = animation.SampledFrames[0].DrawCommands[0].MeshVertices[0];
        var lastVertex = animation.SampledFrames[^1].DrawCommands[0].MeshVertices[0];
        TestAssert.True(Math.Abs(setupVertex.X - -32d) < 0.001d, "Setup frame should keep the mesh setup x.");
        TestAssert.True(Math.Abs(firstVertex.X - -32d) < 0.001d, "First V42 deform frame should start at setup x.");
        TestAssert.True(Math.Abs(lastVertex.X - -16d) < 0.001d, "Last V42 deform frame should include the x offset.");
        TestAssert.True(Math.Abs(lastVertex.Y - -64d) < 0.001d, "V42 deform frame should keep unchanged y.");
    }

    public static async Task TryBuildPreviewAsync_ShouldApplyV42IkConstraintTimeline()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.json"),
            SkeletonJson: """
                {
                  "skeleton": { "hash": "", "spine": "4.2.74" },
                  "bones": [
                    { "name": "root" },
                    { "name": "target", "parent": "root", "x": 32, "y": 0 },
                    { "name": "body", "parent": "root", "length": 32 }
                  ],
                  "ik": [
                    {
                      "name": "aim",
                      "target": "target",
                      "bones": ["body"],
                      "mix": 1
                    }
                  ],
                  "slots": [
                    { "name": "body-slot", "bone": "body", "attachment": "body" }
                  ],
                  "skins": [
                    {
                      "name": "default",
                      "attachments": {
                        "body-slot": {
                          "body": {
                            "type": "region",
                            "path": "body",
                            "x": 32,
                            "y": 0,
                            "width": 8,
                            "height": 8
                          }
                        }
                      }
                    }
                  ],
                  "animations": {
                    "aim-up": {
                      "bones": {
                        "target": {
                          "translate": [
                            { "time": 0, "x": 0, "y": 0 },
                            { "time": 1, "x": -32, "y": 32 }
                          ]
                        }
                      },
                      "ik": {
                        "aim": [
                          { "time": 0, "mix": 1 },
                          { "time": 1, "mix": 1 }
                        ]
                      }
                    }
                  }
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
                body
                  rotate: false
                  xy: 0, 0
                  size: 8, 8
                  orig: 8, 8
                  offset: 0, 0
                  index: -1
                """
            ]));

        TestAssert.True(result.IsHandled, "V42 adapter should handle IK constraint timelines.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        var animation = TestAssert.Single(preview.Animations);
        TestAssert.True(animation.SampledFrames.Count >= 2, "IK constraint timeline should produce sampled frames.");
        var setupCommand = preview.SetupFrame!.DrawCommands[0];
        var lastCommand = animation.SampledFrames[^1].DrawCommands[0];
        TestAssert.True(setupCommand.TransformM31 > 24d, "Setup pose should point the body attachment toward the setup target.");
        TestAssert.True(lastCommand.TransformM31 < 8d, "IK should rotate the body attachment away from the setup x target.");
        TestAssert.True(lastCommand.TransformM32 > 24d, "IK should rotate the body attachment toward the animated y target.");
    }
}
