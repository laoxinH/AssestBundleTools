using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class AnimationClipPreviewDisplayFormatter
{
    private const string UnknownValue = "-";
    private readonly ILocalizationService _localizationService;

    public AnimationClipPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public AnimationClipPreviewDisplayState BuildDisplayState(
        AssetPreviewAnimationClipDto animationClip,
        string assetName)
    {
        ArgumentNullException.ThrowIfNull(animationClip);

        return new AnimationClipPreviewDisplayState(
            AssetNameText: string.IsNullOrWhiteSpace(assetName) ? string.Empty : assetName.Trim(),
            TimingText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewTimingFormat,
                FormatNumber(animationClip.Duration),
                FormatNumber(animationClip.SampleRate),
                animationClip.EstimatedFrameCount?.ToString(CultureInfo.CurrentCulture) ?? UnknownValue,
                FormatNumber(animationClip.StartTime),
                FormatNumber(animationClip.StopTime)),
            CurveCountText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveCountFormat,
                animationClip.FloatCurveCount.ToString(CultureInfo.CurrentCulture),
                animationClip.PositionCurveCount.ToString(CultureInfo.CurrentCulture),
                animationClip.RotationCurveCount.ToString(CultureInfo.CurrentCulture),
                animationClip.ScaleCurveCount.ToString(CultureInfo.CurrentCulture),
                animationClip.PPtrCurveCount.ToString(CultureInfo.CurrentCulture)),
            LegacyText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewLegacyFormat,
                FormatBoolean(animationClip.IsLegacy)),
            LoopText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewLoopFormat,
                FormatBoolean(animationClip.LoopTime),
                FormatBoolean(animationClip.LoopBlend),
                FormatBoolean(animationClip.LoopBlendOrientation),
                FormatBoolean(animationClip.LoopBlendPositionY),
                FormatBoolean(animationClip.LoopBlendPositionXZ)),
            EventsTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorAnimationClipPreviewEventsTitle),
            EmptyEventsText: _localizationService.GetRequiredString(ResourceKeys.InspectorAnimationClipPreviewEmptyEventsText),
            TruncatedText: animationClip.IsEventListTruncated
                ? _localizationService.GetRequiredString(ResourceKeys.InspectorAnimationClipPreviewTruncatedText)
                : string.Empty,
            EventRows: animationClip.Events.Select(BuildEventRow).ToArray());
    }

    private AnimationClipEventRowDisplayState BuildEventRow(AssetPreviewAnimationEventDto animationEvent)
    {
        var functionName = string.IsNullOrWhiteSpace(animationEvent.FunctionName)
            ? UnknownValue
            : animationEvent.FunctionName.Trim();
        var stringParameter = string.IsNullOrWhiteSpace(animationEvent.StringParameter)
            ? UnknownValue
            : animationEvent.StringParameter.Trim();

        return new AnimationClipEventRowDisplayState(
            TimeText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewEventTimeFormat,
                FormatNumber(animationEvent.Time)),
            FunctionNameText: functionName,
            ParameterText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewEventParametersFormat,
                stringParameter,
                FormatNumber(animationEvent.FloatParameter),
                animationEvent.IntParameter?.ToString(CultureInfo.CurrentCulture) ?? UnknownValue));
    }

    private string FormatBoolean(bool? value)
    {
        if (!value.HasValue)
        {
            return UnknownValue;
        }

        return _localizationService.GetRequiredString(value.Value
            ? ResourceKeys.InspectorBooleanTrue
            : ResourceKeys.InspectorBooleanFalse);
    }

    private static string FormatNumber(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.CurrentCulture)
            : UnknownValue;
    }
}

public sealed record AnimationClipPreviewDisplayState(
    string AssetNameText,
    string TimingText,
    string CurveCountText,
    string LegacyText,
    string LoopText,
    string EventsTitle,
    string EmptyEventsText,
    string TruncatedText,
    IReadOnlyList<AnimationClipEventRowDisplayState> EventRows);

public sealed record AnimationClipEventRowDisplayState(
    string TimeText,
    string FunctionNameText,
    string ParameterText);
