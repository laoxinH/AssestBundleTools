using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Previewing;

internal static partial class SpineAnimationRuntimePreviewServiceTests
{
    public static async Task TryBuildPreviewAsync_ShouldBuildV42MeshDrawCommands_FromJsonSkeleton()
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

        TestAssert.True(result.IsHandled, "V42 adapter should handle mesh attachment input.");
        var preview = TestAssert.NotNull(result.Preview, "Runtime adapter should return a preview.");
        var drawCommand = TestAssert.Single(TestAssert.Single(preview.Animations).SampledFrames[0].DrawCommands);
        TestAssert.Equal("body-mesh", drawCommand.AttachmentName, "Draw command should target the mesh attachment.");
        TestAssert.Equal(4, drawCommand.MeshVertices.Count, "Mesh draw command should expose runtime world vertices.");
        TestAssert.Equal(2, drawCommand.MeshTriangles.Count, "Mesh draw command should expose attachment triangles.");
        var vertex = drawCommand.MeshVertices[0];
        TestAssert.True(Math.Abs(vertex.X - -28d) < 0.001d, "Mesh vertex should preserve world x.");
        TestAssert.True(Math.Abs(vertex.Y - -56d) < 0.001d, "Mesh vertex should preserve world y.");
        TestAssert.True(Math.Abs(vertex.U - 0.0625d) < 0.001d, "Mesh vertex should preserve atlas u.");
        TestAssert.True(Math.Abs(vertex.V - 0.09375d) < 0.001d, "Mesh vertex should preserve atlas v.");
    }
}
