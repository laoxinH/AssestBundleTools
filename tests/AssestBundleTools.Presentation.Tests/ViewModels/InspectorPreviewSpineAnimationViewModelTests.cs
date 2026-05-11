using System.Runtime.CompilerServices;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
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
        Assert.Equal("0:00.00 / 0:01.00", viewModel.DisplayPlaybackTimeText);
        Assert.True(viewModel.IsSolidBackgroundSelected);
        Assert.Equal(Visibility.Visible, viewModel.SolidBackgroundVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.CheckerboardBackgroundVisibility);
    }

    [Fact]
    public void Apply_ShouldPreferDrawableSkin_WhenDefaultSkinIsEmpty()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();

        viewModel.Apply(CreatePreviewWithEmptyDefaultSkin(), CreateTextureStates());

        Assert.True(viewModel.HasSpineAnimation);
        Assert.Equal("skin-alt", viewModel.SelectedSkinName);
        Assert.Equal("idle", viewModel.SelectedAnimationName);
        Assert.Equal("skin-alt-idle-frame", viewModel.CurrentFrame?.DrawCommands[0].AttachmentName);
    }

    [Fact]
    public void PlaybackControls_ShouldPauseStepSeekAndApplySpeed()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(), CreateTextureStates());

        viewModel.PlaybackSpeed = 2d;
        viewModel.Advance(TimeSpan.FromSeconds(0.25d));

        Assert.Equal(0.5d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(0.5d, viewModel.DisplayPlaybackTime, precision: 6);
        Assert.Equal(0.5d, viewModel.CurrentFrame?.Time ?? -1d, precision: 6);

        viewModel.StepNextFrame();

        Assert.False(viewModel.IsPlaying);
        Assert.Equal(1d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(1d, viewModel.DisplayPlaybackTime, precision: 6);
        Assert.Equal(1d, viewModel.CurrentFrame?.Time ?? -1d, precision: 6);

        viewModel.StepPreviousFrame();

        Assert.Equal(0.5d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(0.5d, viewModel.DisplayPlaybackTime, precision: 6);

        viewModel.ShowSetupPose();

        Assert.False(viewModel.IsPlaying);
        Assert.Equal(0d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(0d, viewModel.DisplayPlaybackTime, precision: 6);
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
    public void SelectedSkinName_ShouldReplaceAnimationNamesCollection()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(), CreateTextureStates());
        var originalAnimationNames = viewModel.AnimationNames;
        var observedAnimationNameCollections = new List<IReadOnlyList<string>>();

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(InspectorPreviewSpineAnimationViewModel.AnimationNames))
            {
                observedAnimationNameCollections.Add(viewModel.AnimationNames);
            }
        };

        viewModel.SelectedSkinName = "skin-alt";

        Assert.Equal(["idle", "attack"], originalAnimationNames);
        Assert.NotSame(originalAnimationNames, viewModel.AnimationNames);
        Assert.Same(viewModel.AnimationNames, Assert.Single(observedAnimationNameCollections));
    }

    [Fact]
    public void ToggleBackgroundMode_ShouldSwitchSpinePreviewBackground()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.ToggleBackgroundMode();

        Assert.True(viewModel.IsCheckerboardBackgroundSelected);
        Assert.Equal("\uECA5", viewModel.BackgroundModeGlyph);
        Assert.Equal(Visibility.Visible, viewModel.CheckerboardBackgroundVisibility);
        Assert.Equal(Visibility.Collapsed, viewModel.SolidBackgroundVisibility);
        Assert.Contains(nameof(InspectorPreviewSpineAnimationViewModel.BackgroundModeGlyph), changedProperties);

        viewModel.ToggleBackgroundMode();

        Assert.True(viewModel.IsSolidBackgroundSelected);
        Assert.Equal("\uE790", viewModel.BackgroundModeGlyph);
        Assert.Equal(Visibility.Collapsed, viewModel.CheckerboardBackgroundVisibility);
        Assert.Equal(Visibility.Visible, viewModel.SolidBackgroundVisibility);
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

    [Fact]
    public void Apply_ShouldExposeExternalAlphaTextureContent()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        byte[] alphaContent = [4, 5, 6];

        viewModel.Apply(CreatePreview(), CreateTextureStates(alphaContent));

        var content = Assert.Single(viewModel.TexturePageContents).Value;
        Assert.Equal("hero.png", content.PageName);
        Assert.Equal([1, 2, 3], content.ColorContent);
        Assert.Same(alphaContent, content.AlphaContent);
        Assert.True(content.HasAlphaContent);
    }

    [Fact]
    public void Advance_ShouldThrottleDisplayPlaybackTimeNotifications()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreateRuntimePreviewWithoutSampledFrames(), CreateTextureStates());
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        for (var index = 0; index < 5; index++)
        {
            Assert.True(viewModel.Advance(TimeSpan.FromMilliseconds(16)));
        }

        Assert.Equal(0.08d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(0d, viewModel.DisplayPlaybackTime, precision: 6);
        Assert.Equal("0:00.00 / 0:01.00", viewModel.DisplayPlaybackTimeText);
        Assert.DoesNotContain(nameof(InspectorPreviewSpineAnimationViewModel.DisplayPlaybackTime), changedProperties);
        Assert.DoesNotContain(nameof(InspectorPreviewSpineAnimationViewModel.DisplayPlaybackTimeText), changedProperties);

        Assert.True(viewModel.Advance(TimeSpan.FromMilliseconds(20)));

        Assert.Equal(0.1d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(0.1d, viewModel.DisplayPlaybackTime, precision: 6);
        Assert.Equal("0:00.10 / 0:01.00", viewModel.DisplayPlaybackTimeText);
        Assert.Contains(nameof(InspectorPreviewSpineAnimationViewModel.DisplayPlaybackTime), changedProperties);
        Assert.Contains(nameof(InspectorPreviewSpineAnimationViewModel.DisplayPlaybackTimeText), changedProperties);
    }

    [Fact]
    public void DisplayPlaybackTime_ShouldSeekImmediately()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreateRuntimePreviewWithoutSampledFrames(), CreateTextureStates());
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.DisplayPlaybackTime = 0.5d;

        Assert.Equal(0.5d, viewModel.PlaybackTime, precision: 6);
        Assert.Equal(0.5d, viewModel.DisplayPlaybackTime, precision: 6);
        Assert.Equal("0:00.50 / 0:01.00", viewModel.DisplayPlaybackTimeText);
        Assert.Contains(nameof(InspectorPreviewSpineAnimationViewModel.PlaybackTime), changedProperties);
        Assert.Contains(nameof(InspectorPreviewSpineAnimationViewModel.DisplayPlaybackTime), changedProperties);
        Assert.Contains(nameof(InspectorPreviewSpineAnimationViewModel.DisplayPlaybackTimeText), changedProperties);
    }

    [Fact]
    public void CaptureAndRestorePreviewState_ShouldPreservePlaybackAndViewportOptions()
    {
        var source = new InspectorPreviewSpineAnimationViewModel();
        source.Apply(CreatePreview(), CreateTextureStates());
        source.SelectedSkinName = "skin-alt";
        source.SelectedAnimationName = "attack";
        source.PlaybackSpeed = 1.5d;
        source.DisplayPlaybackTime = 0.5d;
        source.ToggleBackgroundMode();
        source.UpdateViewportState(2d, 18d, -9d);
        source.TogglePlayback();

        WorkspaceSpinePreviewStateDto state = source.CapturePreviewState();
        var restored = new InspectorPreviewSpineAnimationViewModel();
        restored.Apply(CreatePreview(), CreateTextureStates(), state);

        Assert.Equal("skin-alt", restored.SelectedSkinName);
        Assert.Equal("attack", restored.SelectedAnimationName);
        Assert.Equal(1.5d, restored.PlaybackSpeed);
        Assert.Equal(0.5d, restored.PlaybackTime, precision: 6);
        Assert.Equal(0.5d, restored.DisplayPlaybackTime, precision: 6);
        Assert.False(restored.IsPlaying);
        Assert.True(restored.IsCheckerboardBackgroundSelected);
        Assert.Equal(2d, restored.ViewportZoom);
        Assert.Equal(18d, restored.ViewportPanX);
        Assert.Equal(-9d, restored.ViewportPanY);
        Assert.Equal("skin-alt-attack-frame", restored.CurrentFrame?.DrawCommands[0].AttachmentName);
    }

    [Fact]
    public void SelectorNames_ShouldIgnoreTransientNullOrInvalidValues_WhenOptionsAreLoaded()
    {
        var viewModel = new InspectorPreviewSpineAnimationViewModel();
        viewModel.Apply(CreatePreview(), CreateTextureStates());
        viewModel.SelectedSkinName = "skin-alt";
        viewModel.SelectedAnimationName = "attack";

        viewModel.SelectedSkinName = null;
        viewModel.SelectedAnimationName = string.Empty;
        viewModel.SelectedSkinName = "missing-skin";
        viewModel.SelectedAnimationName = "missing-animation";

        Assert.Equal("skin-alt", viewModel.SelectedSkinName);
        Assert.Equal("attack", viewModel.SelectedAnimationName);
        Assert.Equal("skin-alt-attack-frame", viewModel.CurrentFrame?.DrawCommands[0].AttachmentName);
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

    private static SpineAnimationPreviewDto CreatePreviewWithEmptyDefaultSkin()
    {
        var preview = CreatePreview();
        var emptySetupFrame = new SpineAnimationFrameDto(0d, Array.Empty<SpineAnimationDrawCommandDto>());
        var emptyDefaultSkin = preview.SkinPreviews[0] with
        {
            Animations = Array.Empty<SpineAnimationClipDto>(),
            SetupFrame = emptySetupFrame
        };
        return preview with
        {
            Animations = Array.Empty<SpineAnimationClipDto>(),
            SetupFrame = emptySetupFrame,
            SkinPreviews = [emptyDefaultSkin, preview.SkinPreviews[1]]
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

    private static IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> CreateTextureStates(
        byte[]? alphaContent = null)
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
        var alphaState = alphaContent is null
            ? null
            : new SpineAssetGroupTexturePreviewState(
                page,
                preview with
                {
                    AssetId = new AssetId("texture-alpha"),
                    BinaryContent = alphaContent
                },
                CreateImageSourceStub(),
                PixelWidth: 64,
                PixelHeight: 64);
        return new Dictionary<string, SpineAnimationPreviewTextureState>(StringComparer.Ordinal)
        {
            ["hero.png"] = new("hero.png", state, alphaState)
        };
    }

    private static ImageSource CreateImageSourceStub() =>
        (BitmapImage)RuntimeHelpers.GetUninitializedObject(typeof(BitmapImage));
}
