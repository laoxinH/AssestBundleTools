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
    public static async Task HandleAsync_ShouldPreferRuntimeAdapterPreview_WhenAdapterHandlesSkeleton()
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
                      "skeleton": { "spine": "4.2.74" },
                      "bones": [{ "name": "root" }],
                      "slots": [{ "name": "body-slot", "bone": "root", "attachment": "body" }],
                      "skins": {
                        "default": {
                          "body-slot": {
                            "body": { "type": "region", "path": "body", "width": 32, "height": 32 }
                          }
                        }
                      },
                      "animations": { "json-fallback": {} }
                    }
                    """));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    Hero.png
                    size: 64,64
                    body
                      xy: 0, 0
                      size: 32, 32
                    """));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 64, 64));
        });
        var runtimePreviewService = new TestSpineAnimationRuntimePreviewService(request =>
            SpineAnimationRuntimePreviewResultDto.Handled(new SpineAnimationPreviewDto(
                request.SkeletonAsset,
                SpineVersion: null,
                Bones: Array.Empty<SpineAnimationBoneDto>(),
                Slots: Array.Empty<SpineAnimationSlotDto>(),
                Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
                Animations:
                [
                    new SpineAnimationClipDto(
                        "runtime-adapter",
                        Duration: 0.5d,
                        BoneTimelines: Array.Empty<SpineAnimationBoneTimelineDto>(),
                        SlotAttachmentTimelines: Array.Empty<SpineAnimationSlotAttachmentTimelineDto>())
                    {
                        SampledFrames =
                        [
                            new SpineAnimationFrameDto(
                                Time: 0d,
                                DrawCommands: Array.Empty<SpineAnimationDrawCommandDto>())
                        ]
                    }
                ],
                Runtime: null)));
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler,
            runtimePreviewService);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine runtime preview service should integrate through the Application port.");
        TestAssert.Equal(1, runtimePreviewService.HandleCallCount, "Known runtime versions should be offered to the runtime adapter.");
        var runtimeRequest = runtimePreviewService.LastRequest ??
                             throw new InvalidOperationException("Expected a runtime preview request.");
        TestAssert.Equal("V42", runtimeRequest.Runtime.AdapterKey, "The request should carry the resolved adapter key.");
        TestAssert.False(runtimeRequest.IncludeSampledFrames, "Interactive runtime previews should not pre-sample whole animations.");
        TestAssert.Single(runtimeRequest.AtlasPages);
        TestAssert.Single(runtimeRequest.AtlasTexts);
        var animationPreview = result.Value?.AnimationPreview ??
                               throw new InvalidOperationException("Expected runtime animation preview.");
        TestAssert.Equal("4.2.74", animationPreview.SpineVersion, "Builder should preserve skeleton version when adapter omits it.");
        TestAssert.Equal("V42", animationPreview.Runtime?.AdapterKey, "Builder should preserve runtime metadata when adapter omits it.");
        var animation = TestAssert.Single(animationPreview.Animations);
        TestAssert.Equal("runtime-adapter", animation.Name, "Runtime adapter output should win over JSON fallback output.");
    }

    public static async Task HandleAsync_ShouldPassBinarySkeletonPreview_ToRuntimeAdapter()
    {
        var workspaceId = new WorkspaceId("workspace-a");
        var spineBundleId = new BundleId("bundle.spine");
        var skeletonAssetId = new AssetId("skeleton-hero");
        var atlasAssetId = new AssetId("atlas-hero");
        var spineAssets = CreateAssetList(
            workspaceId,
            spineBundleId,
            "spine.ab",
            CreateItem(spineBundleId, skeletonAssetId.Value, "Assets/Hero/Hero.skel.bytes", AssetType.TextAsset),
            CreateItem(spineBundleId, atlasAssetId.Value, "Assets/Hero/Hero.atlas", AssetType.TextAsset),
            CreateItem(spineBundleId, "texture-hero", "Assets/Hero/Hero.png", AssetType.Texture2D));
        var binarySkeleton = new byte[] { 1, 2, 3, 4 };
        var previewHandler = new TestAssetPreviewHandler(query =>
        {
            if (query.AssetId.Equals(skeletonAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateBinaryPreview(query, binarySkeleton));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    Hero.png
                    size: 64,64
                    body
                      xy: 0, 0
                      size: 32, 32
                    """));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 64, 64));
        });
        var runtimePreviewService = new TestSpineAnimationRuntimePreviewService(request =>
            SpineAnimationRuntimePreviewResultDto.Handled(new SpineAnimationPreviewDto(
                request.SkeletonAsset,
                SpineVersion: "4.2.74",
                Bones: Array.Empty<SpineAnimationBoneDto>(),
                Slots: Array.Empty<SpineAnimationSlotDto>(),
                Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
                Animations:
                [
                    new SpineAnimationClipDto(
                        "binary-runtime",
                        Duration: 0d,
                        BoneTimelines: Array.Empty<SpineAnimationBoneTimelineDto>(),
                        SlotAttachmentTimelines: Array.Empty<SpineAnimationSlotAttachmentTimelineDto>())
                    {
                        SampledFrames =
                        [
                            new SpineAnimationFrameDto(
                                Time: 0d,
                                DrawCommands: Array.Empty<SpineAnimationDrawCommandDto>())
                        ]
                    }
                ],
                Runtime: new SpineAnimationRuntimeDto(
                    "4.2.74",
                    Major: 4,
                    Minor: 2,
                    AdapterKey: "V42",
                    SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable))));
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler,
            runtimePreviewService);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Binary skeleton groups should still load through the details query.");
        var request = runtimePreviewService.LastRequest ??
                      throw new InvalidOperationException("Expected a runtime preview request.");
        TestAssert.Equal(binarySkeleton.Length, request.SkeletonBinaryContent?.Length ?? 0, "Binary skeleton payload should come from the preview pipeline.");
        TestAssert.True(string.IsNullOrWhiteSpace(request.SkeletonJson), "Binary skeleton requests should not synthesize JSON.");
        TestAssert.False(request.IncludeSampledFrames, "Binary runtime previews should use playback session frames instead of whole-animation pre-sampling.");
        TestAssert.True(
            result.Value?.Diagnostics.All(static diagnostic =>
                diagnostic.AnimationUnsupportedFeature != SpineAnimationUnsupportedFeatureKind.BinarySkeleton) == true,
            "Handled binary runtime previews should not keep the old binary unsupported diagnostic.");
        var animation = TestAssert.Single(result.Value!.AnimationPreview!.Animations);
        TestAssert.Equal("binary-runtime", animation.Name, "Runtime adapter output should be used for binary skeletons.");
    }
}
