using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class AnimationClipPreviewDisplayFormatter
{
    private const string UnknownValue = "-";
    private const string DetailsTimingCardKey = "animation-clip-timing";
    private const string DetailsCurveBindingCardKey = "animation-clip-curve-bindings";
    private const string DetailsEventsCardKey = "animation-clip-events";
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
            BindingSummaryText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewBindingSummaryFormat,
                ResolveCurveBindingCount(animationClip).ToString(CultureInfo.CurrentCulture),
                FormatNullableInteger(ResolveKnownBindingKeyframeCount(animationClip))),
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
            EventSummaryText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewEventSummaryFormat,
                ResolveEventCount(animationClip).ToString(CultureInfo.CurrentCulture)),
            EmptyEventsText: _localizationService.GetRequiredString(ResourceKeys.InspectorAnimationClipPreviewEmptyEventsText),
            TruncatedText: animationClip.IsEventListTruncated
                ? _localizationService.GetRequiredString(ResourceKeys.InspectorAnimationClipPreviewTruncatedText)
                : string.Empty,
            EventRows: animationClip.Events.Select(BuildEventRow).ToArray(),
            DetailCards: BuildDetailCards(animationClip));
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

    private IReadOnlyList<InspectorInfoCardViewModel> BuildDetailCards(AssetPreviewAnimationClipDto animationClip)
    {
        var cards = new List<InspectorInfoCardViewModel>
        {
            CreateCard(
                DetailsTimingCardKey,
                ResourceKeys.InspectorAnimationClipPreviewTimingCardTitle,
                CreateTimingRows(animationClip),
                isExpanded: false),
            CreateCard(
                DetailsCurveBindingCardKey,
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingCardTitle,
                CreateCurveBindingRows(animationClip),
                isExpanded: true)
        };

        var eventRows = CreateEventDetailRows(animationClip);
        if (eventRows.Count > 0)
        {
            cards.Add(CreateCard(
                DetailsEventsCardKey,
                ResourceKeys.InspectorAnimationClipPreviewEventsTitle,
                eventRows,
                isExpanded: true));
        }

        return cards;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateTimingRows(AssetPreviewAnimationClipDto animationClip)
    {
        return
        [
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewDurationLabel),
                FormatNumber(animationClip.Duration)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewSampleRateLabel),
                FormatNumber(animationClip.SampleRate)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewEstimatedFramesLabel),
                animationClip.EstimatedFrameCount?.ToString(CultureInfo.CurrentCulture) ?? UnknownValue),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewStartTimeLabel),
                FormatNumber(animationClip.StartTime)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewStopTimeLabel),
                FormatNumber(animationClip.StopTime)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewLegacyLabel),
                FormatBoolean(animationClip.IsLegacy)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewLoopTimeLabel),
                FormatBoolean(animationClip.LoopTime)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewLoopBlendLabel),
                FormatBoolean(animationClip.LoopBlend)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewLoopBlendOrientationLabel),
                FormatBoolean(animationClip.LoopBlendOrientation)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewLoopBlendPositionYLabel),
                FormatBoolean(animationClip.LoopBlendPositionY)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewLoopBlendPositionXZLabel),
                FormatBoolean(animationClip.LoopBlendPositionXZ))
        ];
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateCurveBindingRows(AssetPreviewAnimationClipDto animationClip)
    {
        var rows = new List<InspectorInfoRowViewModel>
        {
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewFloatCurveCountLabel),
                animationClip.FloatCurveCount.ToString(CultureInfo.CurrentCulture)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewPositionCurveCountLabel),
                animationClip.PositionCurveCount.ToString(CultureInfo.CurrentCulture)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewRotationCurveCountLabel),
                animationClip.RotationCurveCount.ToString(CultureInfo.CurrentCulture)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewScaleCurveCountLabel),
                animationClip.ScaleCurveCount.ToString(CultureInfo.CurrentCulture)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewPPtrCurveCountLabel),
                animationClip.PPtrCurveCount.ToString(CultureInfo.CurrentCulture)),
            new(
                GetString(ResourceKeys.InspectorAnimationClipPreviewCurveBindingCountLabel),
                ResolveCurveBindingCount(animationClip).ToString(CultureInfo.CurrentCulture))
        };

        var curveBindings = animationClip.CurveBindings ?? Array.Empty<AssetPreviewAnimationCurveBindingDto>();
        if (curveBindings.Count == 0)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimationClipPreviewCurveBindingEmptyText)));
            return rows;
        }

        for (var index = 0; index < curveBindings.Count; index++)
        {
            rows.Add(new InspectorInfoRowViewModel(
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimationClipPreviewCurveBindingLabelFormat,
                    ResolveCurveBindingKindText(curveBindings[index].Kind),
                    (index + 1).ToString(CultureInfo.CurrentCulture)),
                FormatCurveBindingValue(curveBindings[index])));
        }

        if (animationClip.IsCurveBindingListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimationClipPreviewCurveBindingTruncatedText)));
        }

        return rows;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateEventDetailRows(AssetPreviewAnimationClipDto animationClip)
    {
        if (animationClip.Events.Count == 0)
        {
            return Array.Empty<InspectorInfoRowViewModel>();
        }

        var rows = new List<InspectorInfoRowViewModel>(animationClip.Events.Count + 1);
        foreach (var animationEvent in animationClip.Events)
        {
            var row = BuildEventRow(animationEvent);
            rows.Add(new InspectorInfoRowViewModel(
                row.TimeText,
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimationClipPreviewEventDetailFormat,
                    row.FunctionNameText,
                    row.ParameterText)));
        }

        if (animationClip.IsEventListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimationClipPreviewTruncatedText)));
        }

        return rows;
    }

    private InspectorInfoCardViewModel CreateCard(
        string key,
        string titleResourceKey,
        IReadOnlyList<InspectorInfoRowViewModel> rows,
        bool isExpanded)
    {
        return new InspectorInfoCardViewModel(
            key,
            GetString(titleResourceKey),
            GetString(ResourceKeys.InspectorInfoCardCopyToolTip),
            _localizationService.GetFormattedString(
                ResourceKeys.InspectorInfoCardItemCountFormat,
                rows.Count.ToString(CultureInfo.CurrentCulture)),
            isExpanded,
            rows);
    }

    private string FormatCurveBindingValue(AssetPreviewAnimationCurveBindingDto binding)
    {
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(binding.Path))
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingPathFormat,
                binding.Path.Trim()));
        }
        else if (binding.PathHash.HasValue)
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingPathHashFormat,
                binding.PathHash.Value.ToString(CultureInfo.CurrentCulture)));
        }

        if (!string.IsNullOrWhiteSpace(binding.Attribute))
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingAttributeFormat,
                binding.Attribute.Trim()));
        }
        else if (binding.AttributeId.HasValue)
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingAttributeIdFormat,
                binding.AttributeId.Value.ToString(CultureInfo.CurrentCulture)));
        }

        if (binding.ClassId.HasValue)
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingClassIdFormat,
                binding.ClassId.Value.ToString(CultureInfo.CurrentCulture)));
        }

        if (binding.TypeId.HasValue)
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingTypeIdFormat,
                binding.TypeId.Value.ToString(CultureInfo.CurrentCulture)));
        }

        if (binding.ScriptPathId.HasValue)
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingScriptPathIdFormat,
                binding.ScriptPathId.Value.ToString(CultureInfo.CurrentCulture)));
        }

        segments.Add(_localizationService.GetFormattedString(
            ResourceKeys.InspectorAnimationClipPreviewCurveBindingCurveKeyFormat,
            binding.CurveCount.ToString(CultureInfo.CurrentCulture),
            FormatNullableInteger(binding.KeyframeCount)));

        if (binding.IsPPtrCurve.HasValue)
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimationClipPreviewCurveBindingPPtrFormat,
                FormatBoolean(binding.IsPPtrCurve)));
        }

        return segments.Count == 0
            ? UnknownValue
            : string.Join(" · ", segments);
    }

    private string ResolveCurveBindingKindText(AssetPreviewAnimationCurveBindingKind kind)
    {
        var resourceKey = kind switch
        {
            AssetPreviewAnimationCurveBindingKind.Float => ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindFloat,
            AssetPreviewAnimationCurveBindingKind.Position => ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindPosition,
            AssetPreviewAnimationCurveBindingKind.Rotation => ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindRotation,
            AssetPreviewAnimationCurveBindingKind.Scale => ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindScale,
            AssetPreviewAnimationCurveBindingKind.PPtr => ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindPPtr,
            AssetPreviewAnimationCurveBindingKind.Generic => ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindGeneric,
            _ => ResourceKeys.InspectorAnimationClipPreviewCurveBindingKindUnknown
        };

        return GetString(resourceKey);
    }

    private static int ResolveEventCount(AssetPreviewAnimationClipDto animationClip) =>
        Math.Max(animationClip.EventCount, animationClip.Events.Count);

    private static int ResolveCurveBindingCount(AssetPreviewAnimationClipDto animationClip) =>
        Math.Max(animationClip.CurveBindingCount, animationClip.CurveBindings?.Count ?? 0);

    private static int? ResolveKnownBindingKeyframeCount(AssetPreviewAnimationClipDto animationClip)
    {
        if (animationClip.CurveBindings is not { Count: > 0 } curveBindings)
        {
            return null;
        }

        var knownTotal = 0;
        var hasKnownValue = false;
        foreach (var binding in curveBindings)
        {
            if (!binding.KeyframeCount.HasValue)
            {
                continue;
            }

            knownTotal += Math.Max(0, binding.KeyframeCount.Value);
            hasKnownValue = true;
        }

        return hasKnownValue ? knownTotal : null;
    }

    private static string FormatNullableInteger(int? value) =>
        value.HasValue
            ? value.Value.ToString(CultureInfo.CurrentCulture)
            : UnknownValue;

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);

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
    string BindingSummaryText,
    string LegacyText,
    string LoopText,
    string EventsTitle,
    string EventSummaryText,
    string EmptyEventsText,
    string TruncatedText,
    IReadOnlyList<AnimationClipEventRowDisplayState> EventRows,
    IReadOnlyList<InspectorInfoCardViewModel>? DetailCards = null);

public sealed record AnimationClipEventRowDisplayState(
    string TimeText,
    string FunctionNameText,
    string ParameterText);
