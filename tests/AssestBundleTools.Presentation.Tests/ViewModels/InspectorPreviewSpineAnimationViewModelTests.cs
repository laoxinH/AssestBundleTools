using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class InspectorPreviewSpineAnimationViewModelTests
{
    [Fact]
    public void Apply_ShouldExposeAnimationSkinAndInitialFrame()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();

        viewModel.Apply(CreatePreview(), CreateTextureStates());

        Assert.True(viewModel.HasSpineAnimation);
        Assert.Equal(["idle", "attack"], viewModel.AnimationNames);
        Assert.Equal(["default", "skin-alt"], viewModel.SkinNames);
        Assert.True(viewModel.IsSkinSelectionEnabled);
        Assert.Equal("idle", viewModel.SelectedAnimationName);
        Assert.Equal("default", viewModel.SelectedSkinName);
        Assert.NotNull(viewModel.CurrentFrame);
        Assert.Equal(0d, viewModel.CurrentFrame.Time);
    }

    [Fact]
    public void PlaybackControls_ShouldPauseStepSeekAndApplySpeed()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(), CreateTextureStates());

        viewModel.PlaybackSpeed = 2d;
        viewModel.Advance(TimeSpan.FromSeconds(0.25d));

        Assert.Equal(0.5d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(0.5d, viewModel.CurrentFrame?.Time ?? -1d, precision: 6);

        viewModel.StepNextFrame();

        Assert.False(viewModel.IsPlaying);
        Assert.Equal(1d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(1d, viewModel.CurrentFrame?.Time ?? -1d, precision: 6);

        viewModel.StepPreviousFrame();

        Assert.Equal(0.5d, viewModel.PlaybackTime, precision: 6);

        viewModel.ShowSetupPose();

        Assert.False(viewModel.IsPlaying);
        Assert.Equal(0d, viewModel.PlaybackTime, precision: 6);
        Assert.Same(viewModel.Preview!.SetupFrame, viewModel.CurrentFrame);

        viewModel.TogglePlayback();

        Assert.True(viewModel.IsPlaying);
        Assert.NotSame(viewModel.Preview!.SetupFrame, viewModel.CurrentFrame);
    }

    [Fact]
    public void SelectedSkinName_ShouldSwitchAnimationFramesAndSetupPose()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(), CreateTextureStates());

        viewModel.SelectedSkinName = "skin-alt";

        Assert.Equal("skin-alt", viewModel.SelectedSkinName);
        Assert.Equal("idle", viewModel.SelectedAnimationName);
        Assert.Equal("skin-alt-idle-frame", viewModel.CurrentFrame?.DrawCommands[0].AttachmentName);

        viewModel.ShowSetupPose();

        Assert.Equal("skin-alt-setup", viewModel.CurrentFrame?.DrawCommands[0].AttachmentName);
    }

    [Fact]
    public void SelectedAnimationName_ShouldResetPlaybackBeforeNotifying()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(), CreateTextureStates());
        viewModel.Advance(TimeSpan.FromSeconds(0.75d));
        var observedPlaybackTimes = new List<double>();
        var observedFrameAttachments = new List<string?>();

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(InspectorPreviewSpineAnimationViewModel.SelectedAnimationName))
            {
                observedPlaybackTimes.Add(viewModel.PlaybackTime);
                observedFrameAttachments.Add(viewModel.CurrentFrame?.DrawCommands[0].AttachmentName);
            }
        };

        viewModel.SelectedAnimationName = "attack";

        Assert.Equal("attack", viewModel.SelectedAnimationName);
        Assert.Equal(0d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal("default-attack-frame", viewModel.CurrentFrame?.DrawCommands[0].AttachmentName);
        Assert.Equal([0d], observedPlaybackTimes);
        Assert.Equal(["default-attack-frame"], observedFrameAttachments);
    }

    [Fact]
    public void SelectedSkinName_ShouldSyncFrameBeforeNotifying()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(), CreateTextureStates());
        var observedFrameAttachments = new List<string?>();

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(InspectorPreviewSpineAnimationViewModel.SelectedSkinName))
            {
                observedFrameAttachments.Add(viewModel.CurrentFrame?.DrawCommands[0].AttachmentName);
            }
        };

        viewModel.SelectedSkinName = "skin-alt";

        Assert.Equal(["skin-alt-idle-frame"], observedFrameAttachments);
    }

    [Fact]
    public void Apply_ShouldExposeRuntimeAnimationsWithoutSampledFrames()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();

        viewModel.Apply(CreateRuntimePreviewWithoutSampledFrames(), CreateTextureStates());

        Assert.True(viewModel.HasSpineAnimation);
        Assert.True(viewModel.HasRuntimePlayback);
        Assert.Equal(["idle", "attack"], viewModel.AnimationNames);
        Assert.Equal("idle", viewModel.SelectedAnimationName);
        Assert.Same(viewModel.Preview!.SetupFrame, viewModel.CurrentFrame);

        var runtimeFrame = CreateFrame(0.25d, "runtime-idle-frame");
        viewModel.ApplyRuntimeFrame(runtimeFrame);

        Assert.Same(runtimeFrame, viewModel.CurrentFrame);
    }

    private static SpineAnimationPreviewDto CreatePreview()
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
        var defaultAnimations = new[]
        {
            CreateAnimation("idle", "default-idle-frame"),
            CreateAnimation("attack", "default-attack-frame")
        };
        var altAnimations = new[]
        {
            CreateAnimation("idle", "skin-alt-idle-frame"),
            CreateAnimation("attack", "skin-alt-attack-frame")
        };
        var defaultSetupFrame = CreateFrame(0d, "default-setup");
        var altSetupFrame = CreateFrame(0d, "skin-alt-setup");

        return new SpineAnimationPreviewDto(
            skeletonAsset,
            SpineVersion: "3.8.99",
            Bones: [new SpineAnimationBoneDto("root", null, 0d, 0d, 0d, 1d, 1d)],
            Slots: [new SpineAnimationSlotDto("slot", "root", "body", 0)],
            Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
            Animations: defaultAnimations)
        {
            SkinNames = ["default", "skin-alt"],
            SetupFrame = defaultSetupFrame,
            SkinPreviews =
            [
                new SpineAnimationSkinPreviewDto(
                    "default",
                    Array.Empty<SpineAnimationAttachmentDto>(),
                    defaultAnimations,
                    defaultSetupFrame),
                new SpineAnimationSkinPreviewDto(
                    "skin-alt",
                    Array.Empty<SpineAnimationAttachmentDto>(),
                    altAnimations,
                    altSetupFrame)
            ]
        };
    }

    private static SpineAnimationPreviewDto CreateRuntimePreviewWithoutSampledFrames()
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
        var animations = new[]
        {
            CreateAnimationWithoutSampledFrames("idle"),
            CreateAnimationWithoutSampledFrames("attack")
        };
        var setupFrame = CreateFrame(0d, "runtime-setup");

        return new SpineAnimationPreviewDto(
            skeletonAsset,
            SpineVersion: "3.8.99",
            Bones: [new SpineAnimationBoneDto("root", null, 0d, 0d, 0d, 1d, 1d)],
            Slots: [new SpineAnimationSlotDto("slot", "root", "body", 0)],
            Attachments: Array.Empty<SpineAnimationAttachmentDto>(),
            Animations: animations,
            Runtime: new SpineAnimationRuntimeDto(
                "3.8.99",
                Major: 3,
                Minor: 8,
                AdapterKey: "V38",
                SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable))
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
            PlaybackSource = new SpineAnimationRuntimePlaybackSourceDto(
                "spine-group",
                skeletonAsset,
                SkeletonJson: "{}",
                Runtime: new SpineAnimationRuntimeDto(
                    "3.8.99",
                    Major: 3,
                    Minor: 8,
                    AdapterKey: "V38",
                    SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable),
                AtlasPages: Array.Empty<SpineAtlasPageDto>(),
                AtlasTexts: Array.Empty<string>())
        };
    }

    private static SpineAnimationClipDto CreateAnimation(string name, string attachmentName) =>
        new(
            name,
            Duration: 1d,
            BoneTimelines: Array.Empty<SpineAnimationBoneTimelineDto>(),
            SlotAttachmentTimelines: Array.Empty<SpineAnimationSlotAttachmentTimelineDto>())
        {
            SampledFrames =
            [
                CreateFrame(0d, attachmentName),
                CreateFrame(0.5d, attachmentName),
                CreateFrame(1d, attachmentName)
            ]
        };

    private static SpineAnimationClipDto CreateAnimationWithoutSampledFrames(string name) =>
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

    private static IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> CreateTextureStates()
    {
        var page = new SpineAtlasPageDto(
            Name: "hero.png",
            Width: 64,
            Height: 64,
            Regions: Array.Empty<SpineAtlasRegionDto>(),
            TexturePageAsset: null);
        var preview = new AssetPreviewDto(
            new WorkspaceId("workspace"),
            new BundleId("bundle"),
            new AssetId("texture"),
            AssetPreviewKind.Image,
            "image/png",
            [1, 2, 3],
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: null,
            PreviewMessage: null,
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            DateTimeOffset.UnixEpoch,
            new AssetPreviewDiagnosticDto(
                AssetPreviewCacheStatusKind.Missing,
                AssetPreviewFallbackReasonKind.None));
        var state = new SpineAssetGroupTexturePreviewState(
            page,
            preview,
            CreateImageSourceStub(),
            PixelWidth: 64,
            PixelHeight: 64);
        return new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal)
        {
            ["hero.png"] = new("hero.png", state)
        };
    }

    private static ImageSource CreateImageSourceStub() =>
        (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
}
