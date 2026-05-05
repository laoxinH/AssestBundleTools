using System.Text.Json;
using AssetsTools.NET;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsAnimationClipPreviewBuilder
{
    private const string AnimationClipPreviewContentType = "application/vnd.assestbundletools.animation-clip-preview+json";
    private const int AnimationClipPreviewSchemaVersion = 3;
    private const int MaxEventCount = 32;
    private const int MaxCurveBindingCount = 48;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildAnimationClipPreview(
        AssetTypeValueField animationClipField,
        long serializedObjectSizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        ArgumentNullException.ThrowIfNull(animationClipField);

        var sampleRate = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(animationClipField, "m_SampleRate"));
        var settingsField = AssetsToolsFieldReader.TryGetField(animationClipField, "m_AnimationClipSettings");
        var muscleClipField = AssetsToolsFieldReader.TryGetField(animationClipField, "m_MuscleClip");
        var startTime = ReadSettingFloat(settingsField, muscleClipField, "m_StartTime");
        var stopTime = ReadSettingFloat(settingsField, muscleClipField, "m_StopTime");
        var duration = CalculateDuration(startTime, stopTime);
        var animationClip = new AssetPreviewAnimationClipDto(
            SchemaVersion: AnimationClipPreviewSchemaVersion,
            SampleRate: sampleRate,
            StartTime: startTime,
            StopTime: stopTime,
            Duration: duration,
            EstimatedFrameCount: CalculateEstimatedFrameCount(duration, sampleRate),
            IsLegacy: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(animationClipField, "m_Legacy")),
            LoopTime: ReadSettingBoolean(settingsField, muscleClipField, "m_LoopTime"),
            LoopBlend: ReadSettingBoolean(settingsField, muscleClipField, "m_LoopBlend"),
            LoopBlendOrientation: ReadSettingBoolean(settingsField, muscleClipField, "m_LoopBlendOrientation"),
            LoopBlendPositionY: ReadSettingBoolean(settingsField, muscleClipField, "m_LoopBlendPositionY"),
            LoopBlendPositionXZ: ReadSettingBoolean(settingsField, muscleClipField, "m_LoopBlendPositionXZ"),
            FloatCurveCount: CountArray(animationClipField, "m_FloatCurves"),
            PositionCurveCount: CountArray(animationClipField, "m_PositionCurves"),
            RotationCurveCount: CountArray(animationClipField, "m_RotationCurves"),
            ScaleCurveCount: CountArray(animationClipField, "m_ScaleCurves"),
            PPtrCurveCount: CountArray(animationClipField, "m_PPtrCurves"),
            Events: ReadEvents(animationClipField, out var eventListTruncated, out var eventCount),
            IsEventListTruncated: eventListTruncated,
            EventCount: eventCount,
            CurveBindingCount: ReadCurveBindings(animationClipField, out var curveBindings, out var curveBindingListTruncated),
            CurveBindings: curveBindings,
            IsCurveBindingListTruncated: curveBindingListTruncated);

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
        out bool isTruncated,
        out int eventCount)
    {
        var eventsArray = AssetsToolsFieldReader.TryGetField(animationClipField, "m_Events", "Array");
        if (eventsArray?.Children is not { Count: > 0 } children)
        {
            isTruncated = false;
            eventCount = 0;
            return Array.Empty<AssetPreviewAnimationEventDto>();
        }

        eventCount = children.Count;
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

    private static int ReadCurveBindings(
        AssetTypeValueField animationClipField,
        out IReadOnlyList<AssetPreviewAnimationCurveBindingDto> curveBindings,
        out bool isTruncated)
    {
        var rows = new List<AssetPreviewAnimationCurveBindingDto>(MaxCurveBindingCount);
        var totalCount = 0;

        AppendFloatLikeCurveBindings(
            animationClipField,
            "m_FloatCurves",
            AssetPreviewAnimationCurveBindingKind.Float,
            rows,
            ref totalCount);
        AppendTransformCurveBindings(
            animationClipField,
            "m_PositionCurves",
            AssetPreviewAnimationCurveBindingKind.Position,
            expectedCurveCount: 3,
            rows,
            ref totalCount);
        AppendTransformCurveBindings(
            animationClipField,
            "m_RotationCurves",
            AssetPreviewAnimationCurveBindingKind.Rotation,
            expectedCurveCount: 4,
            rows,
            ref totalCount);
        AppendTransformCurveBindings(
            animationClipField,
            "m_ScaleCurves",
            AssetPreviewAnimationCurveBindingKind.Scale,
            expectedCurveCount: 3,
            rows,
            ref totalCount);
        AppendPPtrCurveBindings(animationClipField, rows, ref totalCount);

        if (totalCount == 0)
        {
            AppendGenericCurveBindings(animationClipField, rows, ref totalCount);
        }

        isTruncated = totalCount > rows.Count;
        curveBindings = rows;
        return totalCount;
    }

    private static void AppendFloatLikeCurveBindings(
        AssetTypeValueField animationClipField,
        string arrayName,
        AssetPreviewAnimationCurveBindingKind kind,
        ICollection<AssetPreviewAnimationCurveBindingDto> rows,
        ref int totalCount)
    {
        var arrayField = AssetsToolsFieldReader.TryGetField(animationClipField, arrayName, "Array");
        if (arrayField?.Children is not { Count: > 0 } children)
        {
            return;
        }

        totalCount += children.Count;
        foreach (var entry in children)
        {
            if (rows.Count >= MaxCurveBindingCount)
            {
                break;
            }

            rows.Add(CreateTypedCurveBinding(
                entry,
                kind,
                curveCount: 1,
                keyframeCount: CountAnimationCurveKeyframes(AssetsToolsFieldReader.TryGetField(entry, "curve"))));
        }
    }

    private static void AppendTransformCurveBindings(
        AssetTypeValueField animationClipField,
        string arrayName,
        AssetPreviewAnimationCurveBindingKind kind,
        int expectedCurveCount,
        ICollection<AssetPreviewAnimationCurveBindingDto> rows,
        ref int totalCount)
    {
        var arrayField = AssetsToolsFieldReader.TryGetField(animationClipField, arrayName, "Array");
        if (arrayField?.Children is not { Count: > 0 } children)
        {
            return;
        }

        totalCount += children.Count;
        foreach (var entry in children)
        {
            if (rows.Count >= MaxCurveBindingCount)
            {
                break;
            }

            var curveField = AssetsToolsFieldReader.TryGetField(entry, "curve");
            var componentCurves = CountComponentCurves(curveField, expectedCurveCount, out var keyframeCount);
            rows.Add(CreateTypedCurveBinding(
                entry,
                kind,
                curveCount: componentCurves,
                keyframeCount: keyframeCount));
        }
    }

    private static void AppendPPtrCurveBindings(
        AssetTypeValueField animationClipField,
        ICollection<AssetPreviewAnimationCurveBindingDto> rows,
        ref int totalCount)
    {
        var arrayField = AssetsToolsFieldReader.TryGetField(animationClipField, "m_PPtrCurves", "Array");
        if (arrayField?.Children is not { Count: > 0 } children)
        {
            return;
        }

        totalCount += children.Count;
        foreach (var entry in children)
        {
            if (rows.Count >= MaxCurveBindingCount)
            {
                break;
            }

            rows.Add(CreateTypedCurveBinding(
                entry,
                AssetPreviewAnimationCurveBindingKind.PPtr,
                curveCount: 1,
                keyframeCount: CountPPtrCurveKeyframes(AssetsToolsFieldReader.TryGetField(entry, "curve"))));
        }
    }

    private static void AppendGenericCurveBindings(
        AssetTypeValueField animationClipField,
        ICollection<AssetPreviewAnimationCurveBindingDto> rows,
        ref int totalCount)
    {
        var arrayField = AssetsToolsFieldReader.TryGetField(
            animationClipField,
            "m_ClipBindingConstant",
            "genericBindings",
            "Array");
        if (arrayField?.Children is not { Count: > 0 } children)
        {
            return;
        }

        totalCount += children.Count;
        foreach (var entry in children)
        {
            if (rows.Count >= MaxCurveBindingCount)
            {
                break;
            }

            var isPPtrCurve = AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(entry, "isPPtrCurve"));
            rows.Add(new AssetPreviewAnimationCurveBindingDto(
                Kind: isPPtrCurve == true
                    ? AssetPreviewAnimationCurveBindingKind.PPtr
                    : AssetPreviewAnimationCurveBindingKind.Generic,
                Path: null,
                Attribute: null,
                ClassId: null,
                ScriptFileId: null,
                ScriptPathId: ReadPathId(AssetsToolsFieldReader.TryGetField(entry, "script")),
                PathHash: ReadUnsignedAsInt64(AssetsToolsFieldReader.TryGetField(entry, "path")),
                AttributeId: ReadUnsignedAsInt64(AssetsToolsFieldReader.TryGetField(entry, "attribute")),
                TypeId: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(entry, "typeID")),
                CustomType: AssetsToolsFieldReader.ReadByte(AssetsToolsFieldReader.TryGetField(entry, "customType")),
                IsPPtrCurve: isPPtrCurve,
                CurveCount: 1,
                KeyframeCount: null));
        }
    }

    private static AssetPreviewAnimationCurveBindingDto CreateTypedCurveBinding(
        AssetTypeValueField entry,
        AssetPreviewAnimationCurveBindingKind kind,
        int curveCount,
        int keyframeCount)
    {
        var scriptField = AssetsToolsFieldReader.TryGetField(entry, "script");
        return new AssetPreviewAnimationCurveBindingDto(
            Kind: kind,
            Path: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "path")),
            Attribute: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(entry, "attribute")),
            ClassId: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(entry, "classID")),
            ScriptFileId: ReadFileId(scriptField),
            ScriptPathId: ReadPathId(scriptField),
            PathHash: null,
            AttributeId: null,
            TypeId: null,
            CustomType: null,
            IsPPtrCurve: kind == AssetPreviewAnimationCurveBindingKind.PPtr,
            CurveCount: Math.Max(0, curveCount),
            KeyframeCount: Math.Max(0, keyframeCount));
    }

    private static int CountComponentCurves(
        AssetTypeValueField? curveField,
        int expectedCurveCount,
        out int keyframeCount)
    {
        keyframeCount = 0;
        var curveCount = 0;
        foreach (var component in expectedCurveCount == 4
                     ? new[] { "x", "y", "z", "w" }
                     : new[] { "x", "y", "z" })
        {
            var componentKeyframes = CountAnimationCurveKeyframes(AssetsToolsFieldReader.TryGetField(curveField, component));
            if (componentKeyframes <= 0)
            {
                continue;
            }

            curveCount++;
            keyframeCount += componentKeyframes;
        }

        if (curveCount > 0)
        {
            return curveCount;
        }

        keyframeCount = CountAnimationCurveKeyframes(curveField);
        return keyframeCount > 0 ? 1 : expectedCurveCount;
    }

    private static int CountAnimationCurveKeyframes(AssetTypeValueField? curveField)
    {
        var keyframeArray = AssetsToolsFieldReader.TryGetField(curveField, "m_Curve", "Array") ??
                            AssetsToolsFieldReader.TryGetField(curveField, "Array");
        return AssetsToolsFieldReader.CountChildren(keyframeArray);
    }

    private static int CountPPtrCurveKeyframes(AssetTypeValueField? curveField)
    {
        var keyframeArray = AssetsToolsFieldReader.TryGetField(curveField, "Array") ??
                            AssetsToolsFieldReader.TryGetField(curveField, "m_Curve", "Array");
        return AssetsToolsFieldReader.CountChildren(keyframeArray);
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

    private static float? ReadSettingFloat(
        AssetTypeValueField? settingsField,
        AssetTypeValueField? muscleClipField,
        string fieldName)
    {
        return AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(settingsField, fieldName)) ??
               AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(muscleClipField, fieldName));
    }

    private static bool? ReadSettingBoolean(
        AssetTypeValueField? settingsField,
        AssetTypeValueField? muscleClipField,
        string fieldName)
    {
        return AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(settingsField, fieldName)) ??
               AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(muscleClipField, fieldName));
    }

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }

    private static long? ReadFileId(AssetTypeValueField? pptrField)
    {
        var value = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(pptrField, "m_FileID"));
        return value is 0 ? null : value;
    }

    private static long? ReadPathId(AssetTypeValueField? pptrField)
    {
        var value = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(pptrField, "m_PathID"));
        return value is 0 ? null : value;
    }

    private static long? ReadUnsignedAsInt64(AssetTypeValueField? field)
    {
        var value = AssetsToolsFieldReader.ReadUInt32(field);
        return value.HasValue ? value.Value : null;
    }
}
