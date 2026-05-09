using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static partial class SpineAnimationRuntimePreviewServiceTests
{
    public static async Task TryBuildPreviewAsync_ShouldBuildV42RuntimeDrawCommands_FromJsonSkeleton()
    {
        var service = new SpineAnimationRuntimePreviewService();

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart(),
            SkeletonJson: """
                {
                  "skeleton": { "hash": "", "spine": "4.2.74" },
                  "bones": [
                    { "name": "root" },
                    { "name": "body", "parent": "root", "x": 4, "y": 8 }
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
                            "x": 1,
                            "y": 2,
                            "width": 64,
                            "height": 128
                          }
                        }
                      }
                    }
                  ],
                  "animations": {
                    "idle": {
                      "bones": {
                        "body": {
                          "translate": [
                            { "time": 0, "x": 0, "y": 0 },
                            { "time": 1, "x": 12, "y": 16 }
                          ]
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
                size: 256,256
                format: RGBA8888
                filter: Linear,Linear
                repeat: none
                body
                  rotate: false
                  xy: 16, 24
                  size: 64, 128
                  orig: 64, 128
                  offset: 0, 0
                  index: -1
                """
            ]));

        TestAssert.True(result.IsHandled, "V42 adapter should handle compatible JSON skeleton input.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        TestAssert.Equal("4.2.74", preview.SpineVersion, "Runtime preview should preserve the Spine version.");
        TestAssert.Equal("V42", preview.Runtime?.AdapterKey, "Runtime preview should preserve adapter metadata.");
        TestAssert.Equal(2, preview.Bones.Count, "Runtime preview should expose skeleton bones.");
        TestAssert.Equal(1, preview.Slots.Count, "Runtime preview should expose skeleton slots.");
        TestAssert.NotNull(preview.SetupFrame, "Runtime preview should expose a setup pose frame.");
        var attachment = TestAssert.Single(preview.Attachments);
        TestAssert.Equal("Hero.png", attachment.PageName, "Runtime attachment should resolve the atlas page.");
        TestAssert.Equal(16, attachment.RegionX, "Runtime attachment should preserve atlas region x.");
        var animation = TestAssert.Single(preview.Animations);
        TestAssert.Equal("idle", animation.Name, "Runtime preview should expose animation names.");
        TestAssert.True(animation.SampledFrames.Count >= 2, "Runtime preview should sample animation frames.");
        var drawCommand = TestAssert.Single(animation.SampledFrames[0].DrawCommands);
        TestAssert.Equal("Hero.png", drawCommand.PageName, "Draw command should target the atlas page.");
        TestAssert.Equal(16, drawCommand.SourceX, "Draw command should target the atlas region.");
    }

    public static async Task TryBuildPreviewAsync_ShouldBuildV38RuntimeDrawCommands_FromJsonSkeleton()
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
                    { "name": "body", "parent": "root", "x": 4, "y": 8 }
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
                            "x": 1,
                            "y": 2,
                            "width": 64,
                            "height": 128
                          }
                        }
                      }
                    }
                  ],
                  "animations": {
                    "idle": {
                      "bones": {
                        "body": {
                          "translate": [
                            { "time": 0, "x": 0, "y": 0 },
                            { "time": 1, "x": 12, "y": 16 }
                          ]
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
                size: 256,256
                format: RGBA8888
                filter: Linear,Linear
                repeat: none
                body
                  rotate: false
                  xy: 16, 24
                  size: 64, 128
                  orig: 64, 128
                  offset: 0, 0
                  index: -1
                """
            ]));

        TestAssert.True(result.IsHandled, "V38 adapter should handle compatible JSON skeleton input.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        TestAssert.Equal("3.8.99", preview.SpineVersion, "Runtime preview should preserve the Spine version.");
        TestAssert.Equal("V38", preview.Runtime?.AdapterKey, "Runtime preview should preserve adapter metadata.");
        TestAssert.NotNull(preview.SetupFrame, "Runtime preview should expose a setup pose frame.");
        var drawCommand = TestAssert.Single(TestAssert.Single(preview.Animations).SampledFrames[0].DrawCommands);
        TestAssert.Equal("Hero.png", drawCommand.PageName, "Draw command should target the atlas page.");
        TestAssert.Equal(16, drawCommand.SourceX, "Draw command should target the atlas region.");
    }

    public static async Task TryBuildPreviewAsync_ShouldBuildV38SkinPreviewVariants_FromJsonSkeleton()
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
                            "width": 64,
                            "height": 128
                          }
                        }
                      }
                    },
                    {
                      "name": "skin-alt",
                      "attachments": {
                        "body-slot": {
                          "body": {
                            "type": "region",
                            "path": "body-alt",
                            "width": 64,
                            "height": 128
                          }
                        }
                      }
                    }
                  ],
                  "animations": { "idle": {} }
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
                size: 256,256
                format: RGBA8888
                filter: Linear,Linear
                repeat: none
                body
                  rotate: false
                  xy: 16, 24
                  size: 64, 128
                  orig: 64, 128
                  offset: 0, 0
                  index: -1
                body-alt
                  rotate: false
                  xy: 96, 24
                  size: 64, 128
                  orig: 64, 128
                  offset: 0, 0
                  index: -1
                """
            ]));

        TestAssert.True(result.IsHandled, "V38 adapter should handle multi-skin skeleton input.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        TestAssert.Equal(2, preview.SkinPreviews.Count, "Runtime preview should expose per-skin variants.");
        var altSkin = preview.SkinPreviews.First(static skin => skin.Name == "skin-alt");
        var drawCommand = TestAssert.Single(TestAssert.Single(altSkin.Animations).SampledFrames[0].DrawCommands);
        TestAssert.Equal("body-alt", drawCommand.RegionName, "Alt skin should resolve its own region path.");
        TestAssert.Equal(96, drawCommand.SourceX, "Alt skin draw commands should target the alternate atlas region.");
        TestAssert.Equal("body-alt", altSkin.SetupFrame?.DrawCommands[0].RegionName, "Alt setup pose should use the alternate skin.");
    }

    public static async Task TryBuildPreviewAsync_ShouldBuildV38MeshDrawCommands_FromJsonSkeleton()
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
                    { "name": "body", "parent": "root", "x": 4, "y": 8 }
                  ],
                  "slots": [
                    { "name": "body-slot", "bone": "body", "attachment": "body-mesh" }
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
                  "animations": { "idle": {} }
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
                size: 256,256
                format: RGBA8888
                filter: Linear,Linear
                repeat: none
                body
                  rotate: false
                  xy: 16, 24
                  size: 64, 128
                  orig: 64, 128
                  offset: 0, 0
                  index: -1
                """
            ]));

        TestAssert.True(result.IsHandled, "V38 adapter should handle mesh attachment input.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        var attachment = TestAssert.Single(preview.Attachments);
        TestAssert.Equal("body-mesh", attachment.Name, "Runtime attachment should preserve the mesh name.");
        var drawCommand = TestAssert.Single(TestAssert.Single(preview.Animations).SampledFrames[0].DrawCommands);
        TestAssert.Equal("body-mesh", drawCommand.AttachmentName, "Draw command should target the mesh attachment.");
        TestAssert.True(Math.Abs(drawCommand.Width - 64d) < 0.001d, "Mesh draw command should use computed world bounds width.");
        TestAssert.True(Math.Abs(drawCommand.Height - 128d) < 0.001d, "Mesh draw command should use computed world bounds height.");
        TestAssert.True(Math.Abs(drawCommand.TransformM31 - 4d) < 0.001d, "Mesh draw command should preserve the world center x.");
        TestAssert.True(Math.Abs(drawCommand.TransformM32 - 8d) < 0.001d, "Mesh draw command should preserve the world center y.");
        TestAssert.Equal(4, drawCommand.MeshVertices.Count, "Mesh draw command should expose runtime world vertices.");
        TestAssert.Equal(2, drawCommand.MeshTriangles.Count, "Mesh draw command should expose attachment triangles.");
        var vertex = drawCommand.MeshVertices[0];
        TestAssert.True(Math.Abs(vertex.X - -28d) < 0.001d, "Mesh vertex should preserve world x.");
        TestAssert.True(Math.Abs(vertex.Y - -56d) < 0.001d, "Mesh vertex should preserve world y.");
        TestAssert.True(Math.Abs(vertex.U - 0.0625d) < 0.001d, "Mesh vertex should preserve atlas u.");
        TestAssert.True(Math.Abs(vertex.V - 0.09375d) < 0.001d, "Mesh vertex should preserve atlas v.");
    }

    public static async Task TryBuildPreviewAsync_ShouldTryRegisteredAdapters_ForBinarySkeletonWithUnknownRuntime()
    {
        var adapter = new TestSpineRuntimeAdapter();
        var service = new SpineAnimationRuntimePreviewService([adapter]);

        var result = await service.TryBuildPreviewAsync(new SpineAnimationRuntimePreviewRequestDto(
            GroupKey: "assets/hero/hero",
            SkeletonAsset: CreateSkeletonPart("Hero.skel"),
            SkeletonJson: null,
            Runtime: new SpineAnimationRuntimeDto(
                SpineVersion: null,
                Major: null,
                Minor: null,
                AdapterKey: null,
                SpineAnimationRuntimeSupportKind.VersionMissing),
            AtlasPages: Array.Empty<SpineAtlasPageDto>(),
            AtlasTexts: Array.Empty<string>(),
            SkeletonBinaryContent: [1, 2, 3]));

        TestAssert.True(result.IsHandled, "Binary skeleton input should be offered to registered adapters even before its version is known.");
        TestAssert.Equal(1, adapter.CallCount, "The service should try registered adapters for binary skeletons without an adapter key.");
        TestAssert.Equal(3, adapter.LastRequest?.SkeletonBinaryContent?.Length ?? 0, "The adapter should receive the binary skeleton payload.");
    }

    private static SpineAssetGroupPartDto CreateSkeletonPart(string name = "Hero.json") =>
        new(
            SpineAssetGroupPartRole.Skeleton,
            new BundleId("bundle.spine"),
            new AssetId("skeleton-hero"),
            "spine.ab",
            name,
            "Assets/Hero/" + name,
            AssetType.TextAsset,
            nameof(AssetType.TextAsset));

    private sealed class TestSpineRuntimeAdapter : ISpineAnimationRuntimeAdapter
    {
        public int CallCount { get; private set; }

        public SpineAnimationRuntimePreviewRequestDto? LastRequest { get; private set; }

        public string AdapterKey => "TEST";

        public SpineAnimationRuntimePreviewResultDto TryBuildPreview(
            SpineAnimationRuntimePreviewRequestDto request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return SpineAnimationRuntimePreviewResultDto.Handled(new SpineAnimationPreviewDto(
                request.SkeletonAsset,
                SpineVersion: "4.2.74",
                Bones: Array.Empty<SpineAnimationBoneDto>(),
                Slots: Array.Empty<SpineAnimationSlotDto>(),
                Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
                Animations: Array.Empty<SpineAnimationClipDto>(),
                Runtime: new SpineAnimationRuntimeDto(
                    "4.2.74",
                    Major: 4,
                    Minor: 2,
                    AdapterKey,
                    SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable)));
        }
    }
}
