using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class InspectorSpineAnimationPreviewRenderer
{
    private const double DegreesToRadians = Math.PI / 180d;
    private const double MinExtent = 0.001d;

    public static void Draw(
        CanvasDrawingSession drawingSession,
        SpineAnimationPreviewDto preview,
        SpineAnimationClipDto animation,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps,
        float width,
        float height,
        double time)
    {
        DrawReferenceGrid(drawingSession, width, height);

        var drawItems = BuildDrawItems(preview, animation, time, textureBitmaps);
        if (drawItems.Count == 0)
        {
            return;
        }

        var viewportTransform = CreateViewportTransform(drawItems, width, height);
        foreach (var item in drawItems)
        {
            var previousTransform = drawingSession.Transform;
            drawingSession.Transform = item.Transform * viewportTransform;
            drawingSession.DrawImage(
                item.Texture,
                new Rect(-item.Width / 2d, -item.Height / 2d, item.Width, item.Height),
                item.SourceRect,
                1f,
                CanvasImageInterpolation.Linear);
            drawingSession.Transform = previousTransform;
        }

        DrawOrigin(drawingSession, viewportTransform);
    }

    private static IReadOnlyList<SpineDrawItem> BuildDrawItems(
        SpineAnimationPreviewDto preview,
        SpineAnimationClipDto animation,
        double time,
        IReadOnlyDictionary<string, CanvasBitmap> textureBitmaps)
    {
        var boneTransforms = BuildBoneTransforms(preview, animation, time);
        var attachments = preview.Attachments
            .GroupBy(static attachment => CreateAttachmentKey(attachment.SlotName, attachment.Name), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var attachmentTimelineBySlot = animation.SlotAttachmentTimelines
            .ToDictionary(static timeline => timeline.SlotName, StringComparer.Ordinal);
        var drawItems = new List<SpineDrawItem>();
        foreach (var slot in preview.Slots.OrderBy(static slot => slot.DrawOrder))
        {
            if (!boneTransforms.TryGetValue(slot.BoneName, out var boneTransform))
            {
                continue;
            }

            var attachmentName = ResolveAttachmentName(slot, attachmentTimelineBySlot, time);
            if (string.IsNullOrWhiteSpace(attachmentName) ||
                !attachments.TryGetValue(CreateAttachmentKey(slot.Name, attachmentName), out var attachment) ||
                !textureBitmaps.TryGetValue(attachment.PageName, out var texture))
            {
                continue;
            }

            drawItems.Add(CreateDrawItem(attachment, texture, boneTransform));
        }

        return drawItems;
    }

    private static Dictionary<string, Matrix3x2> BuildBoneTransforms(
        SpineAnimationPreviewDto preview,
        SpineAnimationClipDto animation,
        double time)
    {
        var setupBones = preview.Bones.ToDictionary(static bone => bone.Name, StringComparer.Ordinal);
        var localTransforms = new Dictionary<string, Matrix3x2>(StringComparer.Ordinal);
        foreach (var bone in preview.Bones)
        {
            var pose = ApplyBoneTimelines(bone, animation, time);
            localTransforms[bone.Name] = CreateTransform(
                pose.X,
                pose.Y,
                pose.Rotation,
                pose.ScaleX,
                pose.ScaleY);
        }

        var worldTransforms = new Dictionary<string, Matrix3x2>(StringComparer.Ordinal);
        foreach (var bone in preview.Bones)
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
            var amount = (time - previous.Time) / Math.Max(next.Time - previous.Time, MinExtent);
            return InterpolateFrame(previous, next, amount);
        }

        return frames[^1];
    }

    private static SpineAnimationTimelineFrameDto InterpolateFrame(
        SpineAnimationTimelineFrameDto previous,
        SpineAnimationTimelineFrameDto next,
        double amount)
    {
        return new SpineAnimationTimelineFrameDto(
            Time: previous.Time + ((next.Time - previous.Time) * amount),
            X: Lerp(previous.X, next.X, amount),
            Y: Lerp(previous.Y, next.Y, amount),
            Angle: LerpAngle(previous.Angle, next.Angle, amount),
            ScaleX: Lerp(previous.ScaleX, next.ScaleX, amount),
            ScaleY: Lerp(previous.ScaleY, next.ScaleY, amount));
    }

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

    private static SpineDrawItem CreateDrawItem(
        SpineAnimationAttachmentDto attachment,
        CanvasBitmap texture,
        Matrix3x2 boneTransform)
    {
        var attachmentTransform = CreateTransform(
            attachment.X,
            attachment.Y,
            attachment.Rotation + (attachment.IsRegionRotated ? 90d : 0d),
            attachment.ScaleX,
            attachment.ScaleY);
        var sourceRect = new Rect(
            attachment.RegionX,
            attachment.RegionY,
            attachment.RegionWidth,
            attachment.RegionHeight);
        return new SpineDrawItem(
            texture,
            sourceRect,
            attachmentTransform * boneTransform,
            Math.Max(1d, attachment.Width),
            Math.Max(1d, attachment.Height));
    }

    private static Matrix3x2 CreateViewportTransform(
        IReadOnlyList<SpineDrawItem> drawItems,
        float width,
        float height)
    {
        var bounds = ResolveBounds(drawItems);
        var extent = Math.Max(
            Math.Max(bounds.Width, bounds.Height),
            MinExtent);
        var scale = (Math.Min(width, height) * 0.72d) / extent;
        return Matrix3x2.CreateTranslation(
                   (float)(-bounds.CenterX),
                   (float)(-bounds.CenterY)) *
               Matrix3x2.CreateScale((float)scale, (float)-scale) *
               Matrix3x2.CreateTranslation(width / 2f, height / 2f);
    }

    private static SpineBounds ResolveBounds(IReadOnlyList<SpineDrawItem> drawItems)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var item in drawItems)
        {
            foreach (var point in EnumerateCorners(item))
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        if (!double.IsFinite(minX) || !double.IsFinite(minY) ||
            !double.IsFinite(maxX) || !double.IsFinite(maxY))
        {
            return new SpineBounds(0d, 0d, 1d, 1d);
        }

        return new SpineBounds(
            (minX + maxX) / 2d,
            (minY + maxY) / 2d,
            Math.Max(maxX - minX, 1d),
            Math.Max(maxY - minY, 1d));
    }

    private static IEnumerable<Vector2> EnumerateCorners(SpineDrawItem item)
    {
        var halfWidth = (float)(item.Width / 2d);
        var halfHeight = (float)(item.Height / 2d);
        yield return Vector2.Transform(new Vector2(-halfWidth, -halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(halfWidth, -halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(halfWidth, halfHeight), item.Transform);
        yield return Vector2.Transform(new Vector2(-halfWidth, halfHeight), item.Transform);
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

    private static void DrawReferenceGrid(CanvasDrawingSession drawingSession, float width, float height)
    {
        var minorLineColor = Color.FromArgb(14, 100, 116, 139);
        const float gridStep = 24f;
        for (var x = width / 2f % gridStep; x < width; x += gridStep)
        {
            drawingSession.DrawLine(x, 0f, x, height, minorLineColor, 1f);
        }

        for (var y = height / 2f % gridStep; y < height; y += gridStep)
        {
            drawingSession.DrawLine(0f, y, width, y, minorLineColor, 1f);
        }
    }

    private static void DrawOrigin(CanvasDrawingSession drawingSession, Matrix3x2 viewportTransform)
    {
        var origin = Vector2.Transform(Vector2.Zero, viewportTransform);
        drawingSession.FillCircle(origin, 3.2f, Color.FromArgb(220, 59, 130, 246));
    }

    private sealed record SpineDrawItem(
        CanvasBitmap Texture,
        Rect SourceRect,
        Matrix3x2 Transform,
        double Width,
        double Height);

    private sealed record SpineAnimationBonePose(
        double X,
        double Y,
        double Rotation,
        double ScaleX,
        double ScaleY);

    private readonly record struct SpineBounds(
        double CenterX,
        double CenterY,
        double Width,
        double Height);
}
