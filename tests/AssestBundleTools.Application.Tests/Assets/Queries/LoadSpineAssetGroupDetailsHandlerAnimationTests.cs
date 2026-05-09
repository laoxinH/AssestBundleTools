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
    public static async Task HandleAsync_ShouldBuildJsonSpineAnimationPreview_FromPreviewPipeline()
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
                      "bones": [
                        { "name": "root" },
                        { "name": "body", "parent": "root", "x": 4, "y": 8 }
                      ],
                      "slots": [
                        { "name": "body-slot", "bone": "body", "attachment": "body" }
                      ],
                      "skins": {
                        "default": {
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
                        },
                        "skin-alt": {
                          "body-slot": {
                            "body": {
                              "type": "region",
                              "path": "body-alt",
                              "x": 1,
                              "y": 2,
                              "width": 64,
                              "height": 128
                            }
                          }
                        }
                      },
                      "animations": {
                        "idle": {
                          "bones": {
                            "body": {
                              "translate": [
                                { "time": 0, "x": 0, "y": 0 },
                                { "time": 1, "x": 12, "y": 16 }
                              ],
                              "rotate": [
                                { "time": 0, "angle": 0 },
                                { "time": 1, "angle": 15 }
                              ]
                            }
                          },
                          "slots": {
                            "body-slot": {
                              "attachment": [
                                { "time": 0, "name": "body" }
                              ]
                            }
                          }
                        }
                      }
                    }
                    """));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    Hero.png
                    size: 256,256
                    body
                      xy: 16, 24
                      size: 64, 128
                    body-alt
                      xy: 96, 24
                      size: 64, 128
                    """));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 256, 256));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Spine animation preview should be built through preview queries.");
        TestAssert.Equal(4, previewHandler.HandleCallCount, "Skeleton summary, atlas, texture diagnostics and animation preview should stay on the preview pipeline.");
        var animationPreview = result.Value?.AnimationPreview ??
                               throw new InvalidOperationException("Expected animation preview.");
        TestAssert.Equal("4.1.24", animationPreview.SpineVersion, "Spine version should be preserved.");
        TestAssert.Equal(2, animationPreview.Bones.Count, "Bones should be parsed for pose sampling.");
        TestAssert.Equal(1, animationPreview.Slots.Count, "Slots should be parsed for draw order.");
        _ = animationPreview.SetupFrame ??
            throw new InvalidOperationException("Expected JSON fallback setup pose frame.");
        TestAssert.Equal(2, animationPreview.SkinPreviews.Count, "JSON fallback should expose previewable skin variants.");
        var attachment = TestAssert.Single(animationPreview.Attachments);
        TestAssert.Equal("body-slot", attachment.SlotName, "Attachment should retain its owning slot.");
        TestAssert.Equal("Hero.png", attachment.PageName, "Attachment should resolve the atlas page.");
        TestAssert.Equal(16, attachment.RegionX, "Attachment should carry atlas region x.");
        TestAssert.Equal(128, attachment.RegionHeight, "Attachment should carry atlas region height.");
        var animation = TestAssert.Single(animationPreview.Animations);
        TestAssert.Equal("idle", animation.Name, "Animation name should be parsed.");
        TestAssert.Equal(1d, animation.Duration, "Duration should be derived from timeline frames.");
        TestAssert.Equal(2, animation.BoneTimelines.Count, "Translate and rotate timelines should be parsed.");
        TestAssert.Single(animation.SlotAttachmentTimelines);
        TestAssert.True(animation.SampledFrames.Count >= 2, "Animation preview should include runtime-neutral sampled frames.");
        var drawCommand = TestAssert.Single(animation.SampledFrames[0].DrawCommands);
        TestAssert.Equal("Hero.png", drawCommand.PageName, "Sampled draw command should retain the texture page.");
        TestAssert.Equal(16, drawCommand.SourceX, "Sampled draw command should retain atlas source x.");
        TestAssert.Equal(64, drawCommand.SourceWidth, "Sampled draw command should retain atlas source width.");
        var altSkin = animationPreview.SkinPreviews.First(static skin => skin.Name == "skin-alt");
        var altDrawCommand = TestAssert.Single(TestAssert.Single(altSkin.Animations).SampledFrames[0].DrawCommands);
        TestAssert.Equal(96, altDrawCommand.SourceX, "JSON fallback skin variant should resolve its own atlas region.");
        TestAssert.Equal("V41", animationPreview.Runtime?.AdapterKey, "Spine 4.1 should select the V41 runtime adapter.");
        TestAssert.Equal(
            SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable,
            animationPreview.Runtime?.SupportKind,
            "Known Spine versions should be marked as runtime-adaptable.");
        var playbackSource = animationPreview.PlaybackSource ??
                             throw new InvalidOperationException("Expected JSON fallback sampled playback source.");
        TestAssert.Equal("V41", playbackSource.Runtime.AdapterKey, "Fallback playback source should preserve runtime metadata.");
        if (playbackSource.SampledFallback is null)
        {
            throw new InvalidOperationException("JSON fallback previews should expose sampled frames through the playback session source.");
        }

        TestAssert.Equal(2, playbackSource.SampledFallback!.SkinPreviews.Count, "Fallback playback should preserve per-skin sampled frames.");
        TestAssert.Empty(result.Value!.Diagnostics, "A complete previewable multi-skin group should not add diagnostics.");
    }

    public static async Task HandleAsync_ShouldReportUnsupportedRuntimeVersion_WhenNoAdapterMatches()
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
                      "slots": [{ "name": "body-slot", "bone": "root", "attachment": "body" }],
                      "skins": {
                        "default": {
                          "body-slot": {
                            "body": { "type": "region", "path": "body", "width": 64, "height": 64 }
                          }
                        }
                      },
                      "animations": {
                        "idle": {
                          "bones": {
                            "root": {
                              "translate": [
                                { "time": 0, "x": 0, "y": 0 },
                                { "time": 1, "x": 1, "y": 1 }
                              ]
                            }
                          }
                        }
                      }
                    }
                    """));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    Hero.png
                    size: 256,256
                    body
                      xy: 0, 0
                      size: 64, 64
                    """));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 256, 256));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Unsupported runtime versions should remain diagnostic-only.");
        var runtime = result.Value?.AnimationPreview?.Runtime ??
                      throw new InvalidOperationException("Expected runtime metadata.");
        TestAssert.Equal(
            SpineAnimationRuntimeSupportKind.UnsupportedVersion,
            runtime.SupportKind,
            "Spine 4.3 should not silently reuse the 4.2 adapter.");
        var diagnostic = FindDiagnostic(
            result.Value!.Diagnostics,
            SpineAssetGroupDiagnosticKind.AnimationPreviewRuntimeUnsupported);
        TestAssert.Equal("4.3.0", diagnostic.SpineVersion, "Runtime diagnostics should preserve the version.");
    }

    public static async Task HandleAsync_ShouldResolveSpineViewerRuntimeMatrix_ByMajorMinorVersion()
    {
        var versions = new (string SpineVersion, string AdapterKey)[]
        {
            ("2.1.25", "V21"),
            ("3.4.02", "V34"),
            ("3.5.51", "V35"),
            ("3.6.53", "V36"),
            ("3.7.94", "V37"),
            ("3.8.99", "V38"),
            ("4.0.64", "V40"),
            ("4.1.24", "V41"),
            ("4.2.74", "V42")
        };

        foreach (var (spineVersion, adapterKey) in versions)
        {
            var result = await LoadMinimalAnimationPreviewAsync(spineVersion);
            TestAssert.True(result.IsSuccess, $"Spine {spineVersion} should load a minimal animation preview.");
            var runtime = result.Value?.AnimationPreview?.Runtime ??
                          throw new InvalidOperationException($"Expected runtime metadata for Spine {spineVersion}.");

            TestAssert.Equal(
                SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable,
                runtime.SupportKind,
                $"Spine {spineVersion} should be covered by the multi-version adapter matrix.");
            TestAssert.Equal(adapterKey, runtime.AdapterKey, $"Spine {spineVersion} should resolve to {adapterKey}.");
            TestAssert.True(
                result.Value!.AnimationPreview!.Animations.All(static animation => animation.SampledFrames.Count > 0),
                $"Spine {spineVersion} should expose sampled draw frames.");
            TestAssert.Empty(result.Value!.Diagnostics, $"Spine {spineVersion} should not report runtime diagnostics.");
        }
    }

    public static async Task HandleAsync_ShouldReportJsonSpineAnimationUnsupportedFeatures()
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
                      "bones": [
                        { "name": "root" },
                        { "name": "body", "parent": "root" }
                      ],
                      "slots": [
                        { "name": "body-slot", "bone": "body", "attachment": "body" }
                      ],
                      "ik": [{ "name": "leg" }],
                      "transform": [{ "name": "squash" }],
                      "path": [{ "name": "tail" }],
                      "physics": [{ "name": "hair" }],
                      "skins": [
                        {
                          "name": "default",
                          "attachments": {
                            "body-slot": {
                              "body": { "type": "region", "path": "body", "width": 64, "height": 64 },
                              "body-mesh": {
                                "type": "mesh",
                                "path": "body",
                                "uvs": [0, 0, 1, 0, 1, 1],
                                "triangles": [0, 1, 2],
                                "bones": [1, 0],
                                "vertices": [0, 0, 1]
                              },
                              "hitbox": { "type": "boundingbox", "vertexCount": 4 }
                            }
                          }
                        },
                        { "name": "alt", "attachments": {} }
                      ],
                      "animations": {
                        "idle": {
                          "bones": {
                            "body": {
                              "translate": [
                                { "time": 0, "x": 0, "y": 0, "curve": [0.25, 0, 0.75, 1] },
                                { "time": 1, "x": 4, "y": 8 }
                              ],
                              "shear": [{ "time": 0, "x": 10, "y": 0 }]
                            }
                          },
                          "slots": {
                            "body-slot": {
                              "attachment": [{ "time": 0, "name": "body" }],
                              "color": [{ "time": 0, "color": "ffffffff" }],
                              "twoColor": [{ "time": 0, "light": "ffffffff", "dark": "000000ff" }]
                            }
                          },
                          "deform": {
                            "default": {
                              "body-slot": {
                                "body-mesh": [{ "time": 0, "vertices": [0, 0] }]
                              }
                            }
                          },
                          "drawOrder": [{ "time": 0, "offsets": [] }],
                          "events": [{ "time": 0, "name": "footstep" }],
                          "ik": { "leg": [{ "time": 0, "mix": 1 }] }
                        }
                      }
                    }
                    """));
            }

            if (query.AssetId.Equals(atlasAssetId))
            {
                return Result<AssetPreviewDto>.Success(CreateTextPreview(query, """
                    Hero.png
                    size: 256,256
                    body
                      xy: 0, 0
                      size: 64, 64
                    """));
            }

            return Result<AssetPreviewDto>.Success(CreateImagePreview(query, 256, 256));
        });
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        var result = await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));

        TestAssert.True(result.IsSuccess, "Unsupported Spine features should be diagnostic-only.");
        TestAssert.True(result.Value?.AnimationPreview is not null, "Supported region fallback should still produce a preview.");
        var diagnostics = result.Value!.Diagnostics
            .Where(static diagnostic => diagnostic.Kind == SpineAssetGroupDiagnosticKind.AnimationPreviewUnsupportedFeature)
            .ToArray();
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.MeshAttachment);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.WeightedVertices);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.BoundingBoxAttachment);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.SlotTwoColorTimeline);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.BoneShearTimeline);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.DeformTimeline);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.EventTimeline);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.IkConstraint);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.TransformConstraint);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.PathConstraint);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.PhysicsConstraint);
        AssertUnsupportedFeature(diagnostics, SpineAnimationUnsupportedFeatureKind.BezierCurve);
    }

    private static async Task<Result<SpineAssetGroupDetailsDto>> LoadMinimalAnimationPreviewAsync(
        string spineVersion)
    {
        var workspaceId = new WorkspaceId($"workspace-{spineVersion}");
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
                return Result<AssetPreviewDto>.Success(CreateTextPreview(
                    query,
                    CreateMinimalSkeletonJson(spineVersion)));
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
        var handler = new LoadSpineAssetGroupDetailsHandler(
            new TestWorkspaceSessionRegistry(workspaceId, [spineAssets]),
            previewHandler);

        return await handler.HandleAsync(CreateQuery(workspaceId, spineBundleId));
    }

    private static string CreateMinimalSkeletonJson(string spineVersion) =>
        $$"""
        {
          "skeleton": { "spine": "{{spineVersion}}" },
          "bones": [{ "name": "root" }],
          "slots": [{ "name": "body-slot", "bone": "root", "attachment": "body" }],
          "skins": {
            "default": {
              "body-slot": {
                "body": { "type": "region", "path": "body", "width": 32, "height": 32 }
              }
            }
          },
          "animations": {
            "idle": {
              "bones": {
                "root": {
                  "translate": [
                    { "time": 0, "x": 0, "y": 0 },
                    { "time": 1, "x": 1, "y": 1 }
                  ]
                }
              }
            }
          }
        }
        """;
}
