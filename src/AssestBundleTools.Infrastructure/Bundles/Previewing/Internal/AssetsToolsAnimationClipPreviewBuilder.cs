using System.Text.Json;
using AssetsTools.NET;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsAnimationClipPreviewBuilder
{
    private const string AnimationClipPreviewContentType = "application/vnd.assestbundletools.animation-clip-preview+json";
    private const int AnimationClipPreviewSchemaVersion = 1;
    private const int MaxEventCount = 32;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildAnimationClipPreview(
        AssetTypeValueField animationClipField,
        long serializedObjectSizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        ArgumentNullException.ThrowIfNull(animationClipField);

        var sampleRate = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(animationClipField, "m_SampleRate"));
        var settingsField = AssetsToolsFieldReader.TryGetField(animationClipField, "m_AnimationClipSettings");
        var startTime = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(settingsField, "m_StartTime"));
        var stopTime = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(settingsField, "m_StopTime"));
        var duration = CalculateDuration(startTime, stopTime);
        var animationClip = new AssetPreviewAnimationClipDto(
            SchemaVersion: AnimationClipPreviewSchemaVersion,
            SampleRate: sampleRate,
            StartTime: startTime,
            StopTime: stopTime,
            Duration: duration,
            EstimatedFrameCount: CalculateEstimatedFrameCount(duration, sampleRate),
            IsLegacy: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(animationClipField, "m_Legacy")),
            LoopTime: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(settingsField, "m_LoopTime")),
            LoopBlend: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(settingsField, "m_LoopBlend")),
            LoopBlendOrientation: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(settingsField, "m_LoopBlendOrientation")),
            LoopBlendPositionY: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(settingsField, "m_LoopBlendPositionY")),
            LoopBlendPositionXZ: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(settingsField, "m_LoopBlendPositionXZ")),
            FloatCurveCount: CountArray(animationClipField, "m_FloatCurves"),
            PositionCurveCount: CountArray(animationClipField, "m_PositionCurves"),
            RotationCurveCount: CountArray(animationClipField, "m_RotationCurves"),
            ScaleCurveCount: CountArray(animationClipField, "m_ScaleCurves"),
            PPtrCurveCount: CountArray(animationClipField, "m_PPtrCurves"),
            Events: ReadEvents(animationClipField, out var eventListTruncated),
            IsEventListTruncated: eventListTruncated);

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.AnimationClip,
            ContentType: AnimationClipPreviewContentType,
            Content: JsonSerializer.SerializeToUtf8Bytes(animationClip, JsonSerializerOptions),
            PreviewInfoItems: BuildPreviewInfoItems(animationClipField, serializedObjectSizeInBytes),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static IReadOnlyList<AssetPreviewAnimationEventDto> ReadEvents(
        AssetTypeValueField animationClipField,
        out bool isTruncated)
    {
        var eventsArray = AssetsToolsFieldReader.TryGetField(animationClipField, "m_Events", "Array");
        if (eventsArray?.Children is not { Count: > 0 } children)
        {
            isTruncated = false;
            return Array.Empty<AssetPreviewAnimationEventDto>();
        }

        isTruncated = children.Count > MaxEventCount;
        var rows = new List<AssetPreviewAnimationEventDto>(Math.Min(children.Count, MaxEventCount));
        for (var index = 0; index < children.Count && rows.Count < MaxEventCount; index++)
        {
            var entry = children[index];
            rows.Add(new AssetPreviewAnimationEventDto(
                Time: AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(entry, "time")),
                FunctionName: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "functionName")),
                StringParameter: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "data")),
                FloatParameter: AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(entry, "floatParameter")),
                IntParameter: AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(entry, "intParameter"))));
        }

        return rows;
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AssetTypeValueField animationClipField,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AssetsToolsImagePreviewSupport.AddDecimalInfo(items, AssetPreviewInfoKey.AnimationClipSampleRate, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(animationClipField, "m_SampleRate")));
        AssetsToolsImagePreviewSupport.AddBooleanInfo(items, AssetPreviewInfoKey.AnimationClipIsLegacy, AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(animationClipField, "m_Legacy")));
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.AnimationClipEventCount, CountArray(animationClipField, "m_Events"));
        return items;
    }

    private static double? CalculateDuration(float? startTime, float? stopTime)
    {
        if (!startTime.HasValue || !stopTime.HasValue || stopTime.Value < startTime.Value)
        {
            return null;
        }

        return stopTime.Value - startTime.Value;
    }

    private static long? CalculateEstimatedFrameCount(double? duration, float? sampleRate)
    {
        if (!duration.HasValue || !sampleRate.HasValue || duration.Value < 0 || sampleRate.Value <= 0)
        {
            return null;
        }

        return Convert.ToInt64(Math.Round(duration.Value * sampleRate.Value, MidpointRounding.AwayFromZero));
    }

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }
}
