using System.Globalization;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private static IReadOnlyList<SpineAnimationSlotColorTimelineDto> ParseSlotColorTimelines(
        JsonElement animationElement)
    {
        if (!TryGetObject(animationElement, "slots", out var slotsElement))
        {
            return Array.Empty<SpineAnimationSlotColorTimelineDto>();
        }

        var timelines = new List<SpineAnimationSlotColorTimelineDto>();
        foreach (var slotProperty in slotsElement.EnumerateObject())
        {
            AddSlotColorTimeline(timelines, slotProperty.Name, slotProperty.Value, "color", SpineAnimationSlotColorTimelineKind.Rgba);
            AddSlotColorTimeline(timelines, slotProperty.Name, slotProperty.Value, "rgba", SpineAnimationSlotColorTimelineKind.Rgba);
            AddSlotColorTimeline(timelines, slotProperty.Name, slotProperty.Value, "rgb", SpineAnimationSlotColorTimelineKind.Rgb);
            AddSlotColorTimeline(timelines, slotProperty.Name, slotProperty.Value, "alpha", SpineAnimationSlotColorTimelineKind.Alpha);
        }

        return timelines;
    }

    private static void AddSlotColorTimeline(
        List<SpineAnimationSlotColorTimelineDto> timelines,
        string slotName,
        JsonElement slotElement,
        string propertyName,
        SpineAnimationSlotColorTimelineKind kind)
    {
        if (!TryGetArray(slotElement, propertyName, out var framesElement))
        {
            return;
        }

        var frames = framesElement
            .EnumerateArray()
            .Where(static frame => frame.ValueKind == JsonValueKind.Object)
            .Select(frame => CreateSlotColorFrame(frame, kind))
            .Where(static frame => frame is not null)
            .Cast<SpineAnimationSlotColorFrameDto>()
            .OrderBy(static frame => frame.Time)
            .ToArray();
        if (frames.Length > 0)
        {
            timelines.Add(new SpineAnimationSlotColorTimelineDto(slotName, kind, frames));
        }
    }

    private static SpineAnimationSlotColorFrameDto? CreateSlotColorFrame(
        JsonElement frame,
        SpineAnimationSlotColorTimelineKind kind)
    {
        var time = GetDouble(frame, "time", 0d);
        if (kind == SpineAnimationSlotColorTimelineKind.Alpha)
        {
            return new SpineAnimationSlotColorFrameDto(
                time,
                A: GetDouble(frame, "value", GetDouble(frame, "alpha", 1d)));
        }

        var colorText = GetString(frame, "color");
        var color = ParseColor(
            colorText,
            kind == SpineAnimationSlotColorTimelineKind.Rgb ? 6 : 8);
        return colorText is null
            ? null
            : new SpineAnimationSlotColorFrameDto(
                time,
                color.R,
                color.G,
                color.B,
                kind == SpineAnimationSlotColorTimelineKind.Rgb ? null : color.A);
    }

    private static IReadOnlyList<SpineAnimationDrawOrderTimelineFrameDto> ParseDrawOrderTimelineFrames(
        JsonElement animationElement)
    {
        var hasTimeline = TryGetArray(animationElement, "drawOrder", out var framesElement) ||
                          TryGetArray(animationElement, "draworder", out framesElement);
        if (!hasTimeline)
        {
            return Array.Empty<SpineAnimationDrawOrderTimelineFrameDto>();
        }

        return framesElement
            .EnumerateArray()
            .Where(static frame => frame.ValueKind == JsonValueKind.Object)
            .Select(static frame => new SpineAnimationDrawOrderTimelineFrameDto(
                GetDouble(frame, "time", 0d),
                ParseDrawOrderOffsets(frame)))
            .OrderBy(static frame => frame.Time)
            .ToArray();
    }

    private static IReadOnlyList<SpineAnimationDrawOrderOffsetDto> ParseDrawOrderOffsets(
        JsonElement frame)
    {
        if (!TryGetArray(frame, "offsets", out var offsetsElement))
        {
            return Array.Empty<SpineAnimationDrawOrderOffsetDto>();
        }

        return offsetsElement
            .EnumerateArray()
            .Where(static offset => offset.ValueKind == JsonValueKind.Object)
            .Select(static offset => new SpineAnimationDrawOrderOffsetDto(
                GetString(offset, "slot") ?? string.Empty,
                (int)Math.Round(GetDouble(offset, "offset", 0d))))
            .Where(static offset => !string.IsNullOrWhiteSpace(offset.SlotName))
            .ToArray();
    }

    private static SpineAnimationColor ParseColor(string? colorText, int expectedLength)
    {
        if (string.IsNullOrWhiteSpace(colorText))
        {
            return SpineAnimationColor.OpaqueWhite;
        }

        var text = colorText.Trim();
        if (text.StartsWith("#", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        if (text.Length != expectedLength ||
            !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return SpineAnimationColor.OpaqueWhite;
        }

        var r = ParseColorChannel(text, 0);
        var g = ParseColorChannel(text, 2);
        var b = ParseColorChannel(text, 4);
        var a = expectedLength >= 8 ? ParseColorChannel(text, 6) : 1d;
        return new SpineAnimationColor(r, g, b, a);
    }

    private static double ParseColorChannel(string text, int startIndex)
    {
        return byte.Parse(text.AsSpan(startIndex, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
    }

    private readonly record struct SpineAnimationColor(
        double R,
        double G,
        double B,
        double A)
    {
        public static SpineAnimationColor OpaqueWhite { get; } = new(1d, 1d, 1d, 1d);
    }
}
