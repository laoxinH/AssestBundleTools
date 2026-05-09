using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static partial class SpineAnimationRuntimePreviewServiceTests
{
    public static async Task TryBuildPreviewAsync_ShouldApplyV38DrawOrderAndSlotColorTimelines()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.json"),
            SkeletonJson: """
                {
                  "skeleton": { "hash": "", "spine": "3.8.99" },
                  "bones": [{ "name": "root" }],
                  "slots": [
                    { "name": "back-slot", "bone": "root", "attachment": "back" },
                    { "name": "front-slot", "bone": "root", "attachment": "front" }
                  ],
                  "skins": [
                    {
                      "name": "default",
                      "attachments": {
                        "back-slot": {
                          "back": { "type": "region", "path": "back", "width": 32, "height": 32 }
                        },
                        "front-slot": {
                          "front": { "type": "region", "path": "front", "width": 32, "height": 32 }
                        }
                      }
                    }
                  ],
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
                """,
            Runtime: new SpineAnimationRuntimeDto(
                SpineVersion: "3.8.99",
                Major: 3,
                Minor: 8,
                AdapterKey: "V38",
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
                back
                  rotate: false
                  xy: 0, 0
                  size: 32, 32
                  orig: 32, 32
                  offset: 0, 0
                  index: -1
                front
                  rotate: false
                  xy: 32, 0
                  size: 32, 32
                  orig: 32, 32
                  offset: 0, 0
                  index: -1
                """
            ]));

        TestAssert.True(result.IsHandled, "V38 adapter should handle draw order and slot color timelines.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        var commands = TestAssert.Single(preview.Animations).SampledFrames[0].DrawCommands;
        TestAssert.Equal("front-slot", commands[0].SlotName, "Runtime draw order should be reflected in draw commands.");
        TestAssert.Equal("back-slot", commands[1].SlotName, "Runtime draw order should keep the shifted slot last.");
        var tintedCommand = commands[1];
        TestAssert.True(Math.Abs(tintedCommand.TintR - 1d) < 0.001d, "Runtime slot color should preserve red.");
        TestAssert.True(Math.Abs(tintedCommand.TintG) < 0.001d, "Runtime slot color should suppress green.");
        TestAssert.True(Math.Abs(tintedCommand.TintB) < 0.001d, "Runtime slot color should suppress blue.");
        TestAssert.True(Math.Abs(tintedCommand.TintA - (128d / 255d)) < 0.001d, "Runtime slot color should preserve alpha.");
    }

    public static async Task TryBuildPreviewAsync_ShouldApplyV38UnweightedMeshDeformTimeline()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.json"),
            SkeletonJson: """
                {
                  "skeleton": { "hash": "", "spine": "3.8.99" },
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
                      "deform": {
                        "default": {
                          "body-slot": {
                            "body-mesh": [
                              { "time": 0, "vertices": [0, 0, 0, 0, 0, 0, 0, 0] },
                              { "time": 1, "vertices": [16, 0, 16, 0, 16, 0, 16, 0] }
                            ]
                          }
                        }
                      }
                    }
                  }
                }
                """,
            Runtime: new SpineAnimationRuntimeDto(
                SpineVersion: "3.8.99",
                Major: 3,
                Minor: 8,
                AdapterKey: "V38",
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

        TestAssert.True(result.IsHandled, "V38 adapter should handle unweighted mesh deform timelines.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        var animation = TestAssert.Single(preview.Animations);
        TestAssert.True(animation.SampledFrames.Count >= 2, "Deform timeline should produce sampled frames.");
        var setupVertex = preview.SetupFrame!.DrawCommands[0].MeshVertices[0];
        var firstVertex = animation.SampledFrames[0].DrawCommands[0].MeshVertices[0];
        var lastVertex = animation.SampledFrames[^1].DrawCommands[0].MeshVertices[0];
        TestAssert.True(Math.Abs(setupVertex.X - -32d) < 0.001d, "Setup frame should keep the mesh setup x.");
        TestAssert.True(Math.Abs(firstVertex.X - -32d) < 0.001d, "First deform frame should start at setup x.");
        TestAssert.True(Math.Abs(lastVertex.X - -16d) < 0.001d, "Last deform frame should include the x offset.");
        TestAssert.True(Math.Abs(lastVertex.Y - -64d) < 0.001d, "Deform frame should keep unchanged y.");
    }

    public static async Task TryBuildPreviewAsync_ShouldApplyV38WeightedMeshDeformTimeline()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.json"),
            SkeletonJson: """
                {
                  "skeleton": { "hash": "", "spine": "3.8.99" },
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
                            "vertices": [
                              1, 0, -32, -64, 1,
                              1, 0, 32, -64, 1,
                              1, 0, 32, 64, 1,
                              1, 0, -32, 64, 1
                            ],
                            "hull": 4,
                            "width": 64,
                            "height": 128
                          }
                        }
                      }
                    }
                  ],
                  "animations": {
                    "weighted-deform": {
                      "deform": {
                        "default": {
                          "body-slot": {
                            "body-mesh": [
                              { "time": 0, "vertices": [0, 0, 0, 0, 0, 0, 0, 0] },
                              { "time": 1, "vertices": [16, 0, 16, 0, 16, 0, 16, 0] }
                            ]
                          }
                        }
                      }
                    }
                  }
                }
                """,
            Runtime: new SpineAnimationRuntimeDto(
                SpineVersion: "3.8.99",
                Major: 3,
                Minor: 8,
                AdapterKey: "V38",
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

        TestAssert.True(result.IsHandled, "V38 adapter should handle weighted mesh deform timelines.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        var animation = TestAssert.Single(preview.Animations);
        TestAssert.True(animation.SampledFrames.Count >= 2, "Weighted deform timeline should produce sampled frames.");
        var setupVertex = preview.SetupFrame!.DrawCommands[0].MeshVertices[0];
        var lastVertex = animation.SampledFrames[^1].DrawCommands[0].MeshVertices[0];
        TestAssert.True(Math.Abs(setupVertex.X - -32d) < 0.001d, "Setup frame should keep the weighted mesh setup x.");
        TestAssert.True(Math.Abs(lastVertex.X - -16d) < 0.001d, "Weighted deform frame should include the x offset.");
        TestAssert.True(Math.Abs(lastVertex.Y - -64d) < 0.001d, "Weighted deform frame should keep unchanged y.");
    }

    public static async Task TryBuildPreviewAsync_ShouldApplyV38TransformConstraintTimeline()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.json"),
            SkeletonJson: """
                {
                  "skeleton": { "hash": "", "spine": "3.8.99" },
                  "bones": [
                    { "name": "root" },
                    { "name": "target", "parent": "root" },
                    { "name": "body", "parent": "root" }
                  ],
                  "transform": [
                    {
                      "name": "follow-target",
                      "target": "target",
                      "bones": ["body"],
                      "rotateMix": 0,
                      "translateMix": 1,
                      "scaleMix": 0,
                      "shearMix": 0
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
                          "body": { "type": "region", "path": "body", "width": 32, "height": 32 }
                        }
                      }
                    }
                  ],
                  "animations": {
                    "follow": {
                      "bones": {
                        "target": {
                          "translate": [
                            { "time": 0, "x": 0, "y": 0 },
                            { "time": 1, "x": 24, "y": 0 }
                          ]
                        }
                      },
                      "transform": {
                        "follow-target": [
                          { "time": 0, "translateMix": 1 },
                          { "time": 1, "translateMix": 1 }
                        ]
                      }
                    }
                  }
                }
                """,
            Runtime: new SpineAnimationRuntimeDto(
                SpineVersion: "3.8.99",
                Major: 3,
                Minor: 8,
                AdapterKey: "V38",
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

        TestAssert.True(result.IsHandled, "V38 adapter should handle transform constraint timelines.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        var animation = TestAssert.Single(preview.Animations);
        TestAssert.True(animation.SampledFrames.Count >= 2, "Transform constraint timeline should produce sampled frames.");
        var setupCommand = preview.SetupFrame!.DrawCommands[0];
        var lastCommand = animation.SampledFrames[^1].DrawCommands[0];
        TestAssert.True(Math.Abs(setupCommand.TransformM31) < 0.001d, "Setup frame should start at the body setup x.");
        TestAssert.True(Math.Abs(lastCommand.TransformM31 - 24d) < 0.001d, "Transform constraint should move the body with the target.");
    }

    public static async Task TryBuildPreviewAsync_ShouldApplyV38IkConstraintTimeline()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.json"),
            SkeletonJson: """
                {
                  "skeleton": { "hash": "", "spine": "3.8.99" },
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
                SpineVersion: "3.8.99",
                Major: 3,
                Minor: 8,
                AdapterKey: "V38",
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

        TestAssert.True(result.IsHandled, "V38 adapter should handle IK constraint timelines.");
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
