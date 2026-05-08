using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private const double SampledFrameRate = 30d;
    private const int MaxSampledFrameCount = 240;
    private const double DegreesToRadians = Math.PI / 180d;
    private const double MinSampleExtent = 0.001d;

    private static IReadOnlyList<SpineAnimationClipDto> BuildSampledAnimationClips(
        IReadOnlyList<SpineAnimationBoneDto> bones,
        IReadOnlyList<SpineAnimationSlotDto> slots,
        IReadOnlyList<SpineAnimationAttachmentDto> attachments,
        IReadOnlyList<SpineAnimationClipDto> animations)
    {
        return animations
            .Select(animation => animation with
            {
                SampledFrames = BuildSampledFrames(bones, slots, attachments, animation)
            })
            .ToArray();
    }

    private static IReadOnlyList<SpineAnimationFrameDto> BuildSampledFrames(
        IReadOnlyList<SpineAnimationBoneDto> bones,
        IReadOnlyList<SpineAnimationSlotDto> slots,
        IReadOnlyList<SpineAnimationAttachmentDto> attachments,
        SpineAnimationClipDto animation)
    {
        var frameTimes = CreateSampleTimes(animation.Duration);
        return frameTimes
            .Select(time => new SpineAnimationFrameDto(
                time,
                BuildDrawCommands(bones, slots, attachments, animation, time)))
            .ToArray();
    }

    private static IReadOnlyList<double> CreateSampleTimes(double duration)
    {
        if (duration <= 0d)
        {
            return [0d];
        }

        var requestedFrameCount = (int)Math.Floor(duration * SampledFrameRate) + 1;
        var frameCount = Math.Clamp(requestedFrameCount, 2, MaxSampledFrameCount);
        var step = duration / Math.Max(frameCount - 1, 1);
        return Enumerable.Range(0, frameCount)
            .Select(index => index == frameCount - 1 ? duration : index * step)
            .ToArray();
    }

    private static IReadOnlyList<SpineAnimationDrawCommandDto> BuildDrawCommands(
        IReadOnlyList<SpineAnimationBoneDto> bones,
        IReadOnlyList<SpineAnimationSlotDto> slots,
        IReadOnlyList<SpineAnimationAttachmentDto> attachments,
        SpineAnimationClipDto animation,
        double time)
    {
        var boneTransforms = BuildBoneTransforms(bones, animation, time);
        var attachmentsBySlotAndName = attachments
            .GroupBy(static attachment => CreateAttachmentKey(attachment.SlotName, attachment.Name), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var attachmentTimelineBySlot = animation.SlotAttachmentTimelines
            .ToDictionary(static timeline => timeline.SlotName, StringComparer.Ordinal);
        var commands = new List<SpineAnimationDrawCommandDto>();
        foreach (var slot in slots.OrderBy(static slot => slot.DrawOrder))
        {
            if (!boneTransforms.TryGetValue(slot.BoneName, out var boneTransform))
            {
                continue;
            }

            var attachmentName = ResolveAttachmentName(slot, attachmentTimelineBySlot, time);
            if (string.IsNullOrWhiteSpace(attachmentName) ||
                !attachmentsBySlotAndName.TryGetValue(CreateAttachmentKey(slot.Name, attachmentName), out var attachment))
            {
                continue;
            }

            commands.Add(CreateDrawCommand(attachment, boneTransform));
        }

        return commands;
    }

    private static Dictionary<string, Matrix3x2> BuildBoneTransforms(
        IReadOnlyList<SpineAnimationBoneDto> bones,
        SpineAnimationClipDto animation,
        double time)
    {
        var setupBones = bones.ToDictionary(static bone => bone.Name, StringComparer.Ordinal);
        var localTransforms = new Dictionary<string, Matrix3x2>(StringComparer.Ordinal);
        foreach (var bone in bones)
        {
            var pose = ApplyBoneTimelines(bone, animation, time);
            localTransforms[bone.Name] = CreateTransform(pose.X, pose.Y, pose.Rotation, pose.ScaleX, pose.ScaleY);
        }

        var worldTransforms = new Dictionary<string, Matrix3x2>(StringComparer.Ordinal);
        foreach (var bone in bones)
        {
            ResolveWorldBoneTransform(bone.Name, setupBones, localTransforms, worldTransforms);
        }

        return worldTransforms;
    }

    private static Matrix3x2 ResolveWorldBoneTransform(
        string boneName,
        IReadOnlyDictionary<string, SpineAnimationBoneDto> setupBones,
        IReadOnlyDictionary<string, Matrix3x2> localTransforms,
        Dictionary<string, Matrix3x2> worldTransforms)
    {
        if (worldTransforms.TryGetValue(boneName, out var existing))
        {
            return existing;
        }

        if (!setupBones.TryGetValue(boneName, out var bone) ||
            !localTransforms.TryGetValue(boneName, out var localTransform))
        {
            return Matrix3x2.Identity;
        }

        var worldTransform = !string.IsNullOrWhiteSpace(bone.ParentName) &&
                             setupBones.ContainsKey(bone.ParentName)
            ? localTransform * ResolveWorldBoneTransform(
                bone.ParentName,
                setupBones,
                localTransforms,
                worldTransforms)
            : localTransform;
        worldTransforms[boneName] = worldTransform;
        return worldTransform;
    }

    private static SpineAnimationBonePose ApplyBoneTimelines(
        SpineAnimationBoneDto setupBone,
        SpineAnimationClipDto animation,
        double time)
    {
        var pose = new SpineAnimationBonePose(
            setupBone.X,
            setupBone.Y,
            setupBone.Rotation,
            setupBone.ScaleX,
            setupBone.ScaleY);
        foreach (var timeline in animation.BoneTimelines.Where(timeline =>
                     string.Equals(timeline.BoneName, setupBone.Name, StringComparison.Ordinal)))
        {
            var frame = SampleFrame(timeline.Frames, time);
            if (frame is null)
            {
                continue;
            }

            pose = timeline.Kind switch
            {
                SpineAnimationBoneTimelineKind.Rotate => pose with
                {
                    Rotation = setupBone.Rotation + (frame.Angle ?? 0d)
                },
                SpineAnimationBoneTimelineKind.Scale => pose with
                {
                    ScaleX = setupBone.ScaleX * (frame.ScaleX ?? 1d),
                    ScaleY = setupBone.ScaleY * (frame.ScaleY ?? 1d)
                },
                _ => pose with
                {
                    X = setupBone.X + (frame.X ?? 0d),
                    Y = setupBone.Y + (frame.Y ?? 0d)
                }
            };
        }

        return pose;
    }

    private static SpineAnimationTimelineFrameDto? SampleFrame(
        IReadOnlyList<SpineAnimationTimelineFrameDto> frames,
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
            return InterpolateFrame(previous, next, amount);
        }

        return frames[^1];
    }

    private static SpineAnimationTimelineFrameDto InterpolateFrame(
        SpineAnimationTimelineFrameDto previous,
        SpineAnimationTimelineFrameDto next,
        double amount) =>
        new(
            Time: previous.Time + ((next.Time - previous.Time) * amount),
            X: Lerp(previous.X, next.X, amount),
            Y: Lerp(previous.Y, next.Y, amount),
            Angle: LerpAngle(previous.Angle, next.Angle, amount),
            ScaleX: Lerp(previous.ScaleX, next.ScaleX, amount),
            ScaleY: Lerp(previous.ScaleY, next.ScaleY, amount));

    private static string? ResolveAttachmentName(
        SpineAnimationSlotDto slot,
        IReadOnlyDictionary<string, SpineAnimationSlotAttachmentTimelineDto> attachmentTimelineBySlot,
        double time)
    {
        if (!attachmentTimelineBySlot.TryGetValue(slot.Name, out var timeline) ||
            timeline.Frames.Count == 0)
        {
            return slot.SetupAttachmentName;
        }

        var frame = timeline.Frames
            .Where(frame => frame.Time <= time)
            .DefaultIfEmpty(timeline.Frames[0])
            .Last();
        return frame.AttachmentName;
    }

    private static SpineAnimationDrawCommandDto CreateDrawCommand(
        SpineAnimationAttachmentDto attachment,
        Matrix3x2 boneTransform)
    {
        var attachmentTransform = CreateTransform(
            attachment.X,
            attachment.Y,
            attachment.Rotation + (attachment.IsRegionRotated ? 90d : 0d),
            attachment.ScaleX,
            attachment.ScaleY);
        var transform = attachmentTransform * boneTransform;
        return new SpineAnimationDrawCommandDto(
            attachment.SlotName,
            attachment.Name,
            attachment.RegionName,
            attachment.PageName,
            attachment.RegionX,
            attachment.RegionY,
            attachment.RegionWidth,
            attachment.RegionHeight,
            Math.Max(1d, attachment.Width),
            Math.Max(1d, attachment.Height),
            transform.M11,
            transform.M12,
            transform.M21,
            transform.M22,
            transform.M31,
            transform.M32);
    }

    private static Matrix3x2 CreateTransform(
        double x,
        double y,
        double rotation,
        double scaleX,
        double scaleY)
    {
        return Matrix3x2.CreateScale((float)scaleX, (float)scaleY) *
               Matrix3x2.CreateRotation((float)(rotation * DegreesToRadians)) *
               Matrix3x2.CreateTranslation((float)x, (float)y);
    }

    private static double? Lerp(double? previous, double? next, double amount)
    {
        if (!previous.HasValue || !next.HasValue)
        {
            return previous ?? next;
        }

        return previous.Value + ((next.Value - previous.Value) * amount);
    }

    private static double? LerpAngle(double? previous, double? next, double amount)
    {
        if (!previous.HasValue || !next.HasValue)
        {
            return previous ?? next;
        }

        var delta = next.Value - previous.Value;
        while (delta > 180d)
        {
            delta -= 360d;
        }

        while (delta < -180d)
        {
            delta += 360d;
        }

        return previous.Value + (delta * amount);
    }

    private static string CreateAttachmentKey(string slotName, string attachmentName) =>
        string.Concat(slotName, "\n", attachmentName);

    private sealed record SpineAnimationBonePose(
        double X,
        double Y,
        double Rotation,
        double ScaleX,
        double ScaleY);
}
