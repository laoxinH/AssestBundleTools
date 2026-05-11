using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class SpineAnimationTexturePageNameResolverTests
{
    [Fact]
    public void Resolve_ShouldFallbackToAllMatchedAtlasPages_ForRuntimePreviewWithoutActiveAttachments()
    {
        var skeleton = CreatePart(SpineAssetGroupPartRole.Skeleton, "skeleton", "hero.skel.bytes", AssetType.TextAsset);
        var bodyTexture = CreatePart(SpineAssetGroupPartRole.TexturePage, "body", "body.png", AssetType.Texture2D);
        var effectsTexture = CreatePart(SpineAssetGroupPartRole.TexturePage, "effects", "effects.png", AssetType.Texture2D);
        var atlasPages = new[]
        {
            CreateAtlasPage("body.png", bodyTexture),
            CreateAtlasPage("effects.png", effectsTexture)
        };
        var runtime = new SpineAnimationRuntimeDto(
            "3.8.99",
            Major: 3,
            Minor: 8,
            "V38",
            SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable);
        var preview = new SpineAnimationPreviewDto(
            skeleton,
            runtime.SpineVersion,
            Bones: Array.Empty<SpineAnimationBoneDto>(),
            Slots: Array.Empty<SpineAnimationSlotDto>(),
            Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
            Animations: Array.Empty<SpineAnimationClipDto>(),
            runtime)
        {
            PlaybackSource = new SpineAnimationRuntimePlaybackSourceDto(
                "hero",
                skeleton,
                SkeletonJson: null,
                runtime,
                atlasPages,
                AtlasTexts: Array.Empty<string>(),
                SkeletonBinaryContent: [1, 2, 3])
        };
        var details = CreateDetails(preview, atlasPages, bodyTexture, effectsTexture);

        var result = SpineAnimationTexturePageNameResolver.Resolve(details);

        Assert.Equal(["body.png", "effects.png"], result);
    }

    [Fact]
    public void Resolve_ShouldCollectPagesFromSkinFrames_WhenActiveAttachmentsAreEmpty()
    {
        var skeleton = CreatePart(SpineAssetGroupPartRole.Skeleton, "skeleton", "hero.json", AssetType.TextAsset);
        var preview = new SpineAnimationPreviewDto(
            skeleton,
            SpineVersion: null,
            Bones: Array.Empty<SpineAnimationBoneDto>(),
            Slots: Array.Empty<SpineAnimationSlotDto>(),
            Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
            Animations: Array.Empty<SpineAnimationClipDto>())
        {
            SkinPreviews =
            [
                new SpineAnimationSkinPreviewDto(
                    "skin-alt",
                    Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
                    Animations:
                    [
                        new SpineAnimationClipDto(
                            "Attack",
                            Duration: 1d,
                            BoneTimelines: Array.Empty<SpineAnimationBoneTimelineDto>(),
                            SlotAttachmentTimelines: Array.Empty<SpineAnimationSlotAttachmentTimelineDto>())
                        {
                            SampledFrames = [CreateFrame("attack.png")]
                        }
                    ],
                    SetupFrame: CreateFrame("setup.png"))
            ]
        };
        var details = CreateDetails(preview, atlasPages: Array.Empty<SpineAtlasPageDto>());

        var result = SpineAnimationTexturePageNameResolver.Resolve(details);

        Assert.Equal(["setup.png", "attack.png"], result);
    }

    private static SpineAssetGroupDetailsDto CreateDetails(
        SpineAnimationPreviewDto preview,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        params SpineAssetGroupPartDto[] textureParts)
    {
        var group = new SpineAssetGroupDto(
            "group",
            new BundleId("bundle"),
            "hero",
            "assets/spine/hero",
            SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
            AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
            TexturePageAssets: textureParts,
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
        return new SpineAssetGroupDetailsDto(
            group,
            SkeletonSummaries: Array.Empty<SpineSkeletonSummaryDto>(),
            AtlasPages: atlasPages,
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>(),
            preview);
    }

    private static SpineAtlasPageDto CreateAtlasPage(
        string pageName,
        SpineAssetGroupPartDto texturePart) =>
        new(
            pageName,
            Width: 64,
            Height: 64,
            Regions: Array.Empty<SpineAtlasRegionDto>(),
            texturePart);

    private static SpineAnimationFrameDto CreateFrame(string pageName) =>
        new(
            0d,
            [
                new SpineAnimationDrawCommandDto(
                    "slot",
                    "attachment",
                    "region",
                    pageName,
                    SourceX: 0,
                    SourceY: 0,
                    SourceWidth: 1,
                    SourceHeight: 1,
                    Width: 1d,
                    Height: 1d,
                    TransformM11: 1d,
                    TransformM12: 0d,
                    TransformM21: 0d,
                    TransformM22: 1d,
                    TransformM31: 0d,
                    TransformM32: 0d)
            ]);

    private static SpineAssetGroupPartDto CreatePart(
        SpineAssetGroupPartRole role,
        string assetId,
        string name,
        AssetType assetType) =>
        new(
            role,
            new BundleId("bundle"),
            new AssetId(assetId),
            "bundle.ab",
            name,
            string.Concat("Assets/spine/", name),
            assetType,
            assetType.ToString());
}
