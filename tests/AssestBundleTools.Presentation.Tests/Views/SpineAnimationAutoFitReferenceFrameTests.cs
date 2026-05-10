using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SpineAnimationAutoFitReferenceFrameTests
{
    [Fact]
    public void ResolveAutoFitReferenceFrames_ShouldExcludeSetupPoseDuringSampledAnimationPlayback()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(includeRuntime: false, includeSampledFrames: true), CreateTextureStates());

        var frames = InspectorSpineAnimationPreviewSurfaceView.ResolveAutoFitReferenceFrames(viewModel);

        Assert.Equal(viewModel.SelectedAnimation!.SampledFrames, frames);
        Assert.DoesNotContain(viewModel.Preview!.SetupFrame, frames);
    }

    [Fact]
    public void ResolveAutoFitReferenceFrames_ShouldUseSetupPoseOnlyWhenSetupPoseIsShown()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(includeRuntime: false, includeSampledFrames: true), CreateTextureStates());

        viewModel.ShowSetupPose();

        var frame = Assert.Single(InspectorSpineAnimationPreviewSurfaceView.ResolveAutoFitReferenceFrames(viewModel));
        Assert.Same(viewModel.Preview!.SetupFrame, frame);
    }

    [Fact]
    public void ResolveAutoFitReferenceFrames_ShouldWaitForCapturedRuntimeFrameWithoutSampledFrames()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(includeRuntime: true, includeSampledFrames: false), CreateTextureStates());
        var runtimeFrame = CreateFrame(0.25d, "runtime-frame");

        Assert.Same(viewModel.Preview!.SetupFrame, viewModel.CurrentFrame);
        Assert.Empty(InspectorSpineAnimationPreviewSurfaceView.ResolveAutoFitReferenceFrames(viewModel));
        Assert.Empty(InspectorSpineAnimationPreviewSurfaceView.ResolveAutoFitReferenceFrames(
            viewModel,
            viewModel.Preview.SetupFrame));

        viewModel.ApplyRuntimeFrame(runtimeFrame);

        var frame = Assert.Single(InspectorSpineAnimationPreviewSurfaceView.ResolveAutoFitReferenceFrames(
            viewModel,
            runtimeFrame));
        Assert.Same(runtimeFrame, frame);
    }

    private static SpineAnimationPreviewDto CreatePreview(bool includeRuntime, bool includeSampledFrames)
    {
        var skeletonAsset = new SpineAssetGroupPartDto(
            SpineAssetGroupPartRole.Skeleton,
            new BundleId("bundle"),
            new AssetId("skeleton"),
            "spine.ab",
            "hero.skel",
            "Assets/hero.skel",
            AssetType.TextAsset,
            nameof(AssetType.TextAsset));
        var setupFrame = CreateFrame(0d, "setup");
        var animations = new[]
        {
            includeSampledFrames
                ? CreateSampledAnimation("idle")
                : CreateRuntimeAnimation("idle")
        };
        var runtime = includeRuntime
            ? new SpineAnimationRuntimeDto(
                "3.8.99",
                Major: 3,
                Minor: 8,
                AdapterKey: "V38",
                SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable)
            : null;

        return new SpineAnimationPreviewDto(
            skeletonAsset,
            SpineVersion: "3.8.99",
            Bones: [new SpineAnimationBoneDto("root", null, 0d, 0d, 0d, 1d, 1d)],
            Slots: [new SpineAnimationSlotDto("slot", "root", "body", 0)],
            Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
            Animations: animations,
            Runtime: runtime)
        {
            SkinNames = ["default"],
            SetupFrame = setupFrame,
            SkinPreviews =
            [
                new SpineAnimationSkinPreviewDto(
                    "default",
                    Array.Empty<SpineAnimationAttachmentDto>(),
                    animations,
                    setupFrame)
            ],
            PlaybackSource = includeRuntime
                ? new SpineAnimationRuntimePlaybackSourceDto(
                    "spine-group",
                    skeletonAsset,
                    SkeletonJson: "{}",
                    Runtime: runtime!,
                    AtlasPages: Array.Empty<SpineAtlasPageDto>(),
                    AtlasTexts: Array.Empty<string>())
                : null
        };
    }

    private static SpineAnimationClipDto CreateSampledAnimation(string name) =>
        CreateRuntimeAnimation(name) with
        {
            SampledFrames =
            [
                CreateFrame(0d, "sampled-frame-0"),
                CreateFrame(0.5d, "sampled-frame-1")
            ]
        };

    private static SpineAnimationClipDto CreateRuntimeAnimation(string name) =>
        new(
            name,
            Duration: 1d,
            BoneTimelines: Array.Empty<SpineAnimationBoneTimelineDto>(),
            SlotAttachmentTimelines: Array.Empty<SpineAnimationSlotAttachmentTimelineDto>());

    private static SpineAnimationFrameDto CreateFrame(double time, string attachmentName) =>
        new(
            time,
            [
                new SpineAnimationDrawCommandDto(
                    "slot",
                    attachmentName,
                    attachmentName,
                    "hero.png",
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

    private static IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> CreateTextureStates() =>
        new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal);
}
