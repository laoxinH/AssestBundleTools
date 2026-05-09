using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private static IReadOnlyList<SpineAnimationSlotDto> ResolveDrawOrder(
        IReadOnlyList<SpineAnimationSlotDto> slots,
        SpineAnimationClipDto? animation,
        double time)
    {
        var setupSlots = slots.OrderBy(static slot => slot.DrawOrder).ToArray();
        if (animation is null || animation.DrawOrderTimelineFrames.Count == 0)
        {
            return setupSlots;
        }

        var frame = animation.DrawOrderTimelineFrames
            .Where(frame => frame.Time <= time)
            .DefaultIfEmpty(animation.DrawOrderTimelineFrames[0])
            .Last();
        if (frame.Offsets.Count == 0 ||
            !TryBuildDrawOrderIndexes(setupSlots, frame.Offsets, out var drawOrderIndexes))
        {
            return setupSlots;
        }

        return drawOrderIndexes.Select(index => setupSlots[index]).ToArray();
    }

    private static bool TryBuildDrawOrderIndexes(
        IReadOnlyList<SpineAnimationSlotDto> setupSlots,
        IReadOnlyList<SpineAnimationDrawOrderOffsetDto> offsets,
        out IReadOnlyList<int> drawOrderIndexes)
    {
        drawOrderIndexes = Array.Empty<int>();
        var slotIndexByName = setupSlots
            .Select(static (slot, index) => new { slot.Name, Index = index })
            .ToDictionary(static item => item.Name, static item => item.Index, StringComparer.Ordinal);
        var drawOrder = Enumerable.Repeat(-1, setupSlots.Count).ToArray();
        var unchanged = new int[Math.Max(0, setupSlots.Count - offsets.Count)];
        var originalIndex = 0;
        var unchangedIndex = 0;
        foreach (var offset in offsets)
        {
            if (!slotIndexByName.TryGetValue(offset.SlotName, out var slotIndex) ||
                slotIndex < originalIndex)
            {
                return false;
            }

            while (originalIndex != slotIndex)
            {
                if (unchangedIndex >= unchanged.Length)
                {
                    return false;
                }

                unchanged[unchangedIndex++] = originalIndex++;
            }

            var targetIndex = originalIndex + offset.Offset;
            if (targetIndex < 0 ||
                targetIndex >= drawOrder.Length ||
                drawOrder[targetIndex] != -1)
            {
                return false;
            }

            drawOrder[targetIndex] = originalIndex++;
        }

        while (originalIndex < setupSlots.Count)
        {
            if (unchangedIndex >= unchanged.Length)
            {
                return false;
            }

            unchanged[unchangedIndex++] = originalIndex++;
        }

        for (var index = drawOrder.Length - 1; index >= 0; index--)
        {
            if (drawOrder[index] != -1)
            {
                continue;
            }

            if (unchangedIndex <= 0)
            {
                return false;
            }

            drawOrder[index] = unchanged[--unchangedIndex];
        }

        drawOrderIndexes = drawOrder;
        return true;
    }

    private static SpineAnimationSlotTint ResolveSlotTint(
        SpineAnimationSlotDto slot,
        IEnumerable<SpineAnimationSlotColorTimelineDto> timelines,
        double time)
    {
        var tint = new SpineAnimationSlotTint(slot.ColorR, slot.ColorG, slot.ColorB, slot.ColorA);
        foreach (var timeline in timelines)
        {
            var frame = SampleSlotColorFrame(timeline.Frames, time);
            if (frame is null)
            {
                continue;
            }

            tint = timeline.Kind switch
            {
                SpineAnimationSlotColorTimelineKind.Rgb => tint with
                {
                    R = frame.R ?? tint.R,
                    G = frame.G ?? tint.G,
                    B = frame.B ?? tint.B
                },
                SpineAnimationSlotColorTimelineKind.Alpha => tint with
                {
                    A = frame.A ?? tint.A
                },
                _ => new SpineAnimationSlotTint(
                    frame.R ?? tint.R,
                    frame.G ?? tint.G,
                    frame.B ?? tint.B,
                    frame.A ?? tint.A)
            };
        }

        return tint.Clamp();
    }

    private static SpineAnimationSlotColorFrameDto? SampleSlotColorFrame(
        IReadOnlyList<SpineAnimationSlotColorFrameDto> frames,
        double time)
    {
        if (frames.Count == 0)
        {
            return null;
        }

        if (frames.Count == 1 || time <= frames[0].Time)
        {
            return frames[0];
        }

        for (var index = 1; index < frames.Count; index++)
        {
            if (time > frames[index].Time)
            {
                continue;
            }

            var previous = frames[index - 1];
            var next = frames[index];
            var amount = (time - previous.Time) / Math.Max(next.Time - previous.Time, MinSampleExtent);
            return new SpineAnimationSlotColorFrameDto(
                previous.Time + ((next.Time - previous.Time) * amount),
                Lerp(previous.R, next.R, amount),
                Lerp(previous.G, next.G, amount),
                Lerp(previous.B, next.B, amount),
                Lerp(previous.A, next.A, amount));
        }

        return frames[^1];
    }

    private static double ClampUnit(double value) =>
        Math.Clamp(value, 0d, 1d);

    private readonly record struct SpineAnimationSlotTint(
        double R,
        double G,
        double B,
        double A)
    {
        public SpineAnimationSlotTint Clamp() =>
            new(
                ClampUnit(R),
                ClampUnit(G),
                ClampUnit(B),
                ClampUnit(A));
    }
}
