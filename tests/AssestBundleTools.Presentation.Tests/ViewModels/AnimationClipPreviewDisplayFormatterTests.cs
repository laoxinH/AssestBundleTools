using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AnimationClipPreviewDisplayFormatterTests
{
    [Fact]
    public void BuildDisplayState_ShouldShowUnknownKeyframes_ForPackedGenericBindings()
    {
        var formatter = new AnimationClipPreviewDisplayFormatter(new TestLocalizationService());
        var animationClip = new AssetPreviewAnimationClipDto(
            SchemaVersion: 3,
            SampleRate: 30,
            StartTime: 0,
            StopTime: 13.533,
            Duration: 13.533,
            EstimatedFrameCount: 406,
            IsLegacy: false,
            LoopTime: false,
            LoopBlend: false,
            LoopBlendOrientation: false,
            LoopBlendPositionY: false,
            LoopBlendPositionXZ: false,
            FloatCurveCount: 0,
            PositionCurveCount: 0,
            RotationCurveCount: 0,
            ScaleCurveCount: 0,
            PPtrCurveCount: 0,
            Events: Array.Empty<AssetPreviewAnimationEventDto>(),
            EventCount: 0,
            CurveBindingCount: 318,
            CurveBindings:
            [
                new AssetPreviewAnimationCurveBindingDto(
                    AssetPreviewAnimationCurveBindingKind.Generic,
                    Path: null,
                    Attribute: null,
                    ClassId: null,
                    ScriptFileId: null,
                    ScriptPathId: null,
                    PathHash: 1387920772,
                    AttributeId: 1895888582,
                    TypeId: 23,
                    CustomType: 22,
                    IsPPtrCurve: false,
                    CurveCount: 1,
                    KeyframeCount: null)
            ],
            IsCurveBindingListTruncated: true);

        var displayState = formatter.BuildDisplayState(animationClip, "idleToInteract");

        Assert.Equal(
            ResourceKeys.InspectorAnimationClipPreviewBindingSummaryFormat + ":318|-",
            displayState.BindingSummaryText);
        var curveBindingCard = Assert.Single(
            displayState.DetailCards!,
            static card => card.Key == "animation-clip-curve-bindings");
        Assert.Contains(
            curveBindingCard.Rows,
            static row => row.Label == ResourceKeys.InspectorAnimationClipPreviewCurveBindingLabelFormat + ":" +
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindGeneric + "|1" &&
                          row.Value.Contains(
                              ResourceKeys.InspectorAnimationClipPreviewCurveBindingCurveKeyFormat + ":1|-",
                              StringComparison.Ordinal));
    }
}
