using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using Runtime = AssestBundleTools.Infrastructure.ThirdParty.Spine.V42;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal sealed partial class V42SpineAnimationRuntimeAdapter :
    ISpineAnimationRuntimeAdapter,
    ISpineAnimationRuntimePlaybackAdapter
{
    public string AdapterKey => "V42";

    public SpineAnimationRuntimePreviewResultDto TryBuildPreview(
        SpineAnimationRuntimePreviewRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePreviewResultDto.Cancelled;
        }

        if (request.AtlasTexts is not { Count: > 0 } ||
            (string.IsNullOrWhiteSpace(request.SkeletonJson) &&
             request.SkeletonBinaryContent is not { Length: > 0 }))
        {
            return SpineAnimationRuntimePreviewResultDto.NotHandled;
        }

        try
        {
            var skeletonData = ReadSkeletonData(request);
            if (skeletonData is null ||
                !IsV42Skeleton(skeletonData.Version))
            {
                return SpineAnimationRuntimePreviewResultDto.NotHandled;
            }

            var runtime = new SpineAnimationRuntimeDto(
                skeletonData.Version,
                Major: 4,
                Minor: 2,
                AdapterKey,
                SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable);
            var skinPreviews = BuildSkinPreviews(
                skeletonData,
                request.IncludeSampledFrames,
                cancellationToken);
            if (skinPreviews.Count == 0 ||
                !HasDrawablePreview(skinPreviews))
            {
                return SpineAnimationRuntimePreviewResultDto.NotHandled;
            }

            var activeSkinPreview = ResolveInitialSkinPreview(skinPreviews);
            return SpineAnimationRuntimePreviewResultDto.Handled(new SpineAnimationPreviewDto(
                request.SkeletonAsset,
                skeletonData.Version,
                MapBones(skeletonData),
                MapSlots(skeletonData),
                activeSkinPreview.Attachments,
                activeSkinPreview.Animations,
                runtime)
            {
                SkinNames = MapSkinNames(skeletonData),
                SetupFrame = activeSkinPreview.SetupFrame,
                SkinPreviews = skinPreviews
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePreviewResultDto.Cancelled;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePreviewResultDto.NotHandled;
        }
    }

    private static Runtime.SkeletonData? ReadSkeletonData(SpineAnimationRuntimePreviewRequestDto request)
    {
        var atlases = request.AtlasTexts
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Select(static text => new Runtime.Atlas(
                new StringReader(text),
                string.Empty,
                new NoOpTextureLoader()))
            .ToArray();
        if (request.SkeletonBinaryContent is { Length: > 0 } skeletonBinaryContent)
        {
            var binaryLoader = new Runtime.SkeletonBinary(atlases);
            using var stream = new MemoryStream(skeletonBinaryContent);
            return binaryLoader.ReadSkeletonData(stream);
        }

        if (string.IsNullOrWhiteSpace(request.SkeletonJson))
        {
            throw new InvalidDataException("Skeleton JSON or binary content is required.");
        }

        var jsonLoader = new Runtime.SkeletonJson(atlases);
        using var reader = new StringReader(request.SkeletonJson);
        return jsonLoader.ReadSkeletonData(reader);
    }

    private static bool IsV42Skeleton(string? spineVersion)
    {
        if (string.IsNullOrWhiteSpace(spineVersion))
        {
            return false;
        }

        var segments = spineVersion.Trim().Split('.', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 2 &&
               string.Equals(segments[0], "4", StringComparison.Ordinal) &&
               string.Equals(segments[1], "2", StringComparison.Ordinal);
    }

    private static IReadOnlyList<SpineAnimationBoneDto> MapBones(Runtime.SkeletonData skeletonData) =>
        skeletonData.Bones
            .Select(static bone => new SpineAnimationBoneDto(
                bone.Name,
                bone.Parent?.Name,
                bone.X,
                bone.Y,
                bone.Rotation,
                bone.ScaleX,
                bone.ScaleY))
            .ToArray();

    private static IReadOnlyList<SpineAnimationSlotDto> MapSlots(Runtime.SkeletonData skeletonData) =>
        skeletonData.Slots
            .Select(static slot => new SpineAnimationSlotDto(
                slot.Name,
                slot.BoneData.Name,
                slot.AttachmentName,
                slot.Index))
            .ToArray();

    private static IReadOnlyList<string> MapSkinNames(Runtime.SkeletonData skeletonData) =>
        skeletonData.Skins
            .Select(static skin => skin.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<SpineAnimationSkinPreviewDto> BuildSkinPreviews(
        Runtime.SkeletonData skeletonData,
        bool includeSampledFrames,
        CancellationToken cancellationToken)
    {
        var skins = skeletonData.Skins
            .Where(static skin => !string.IsNullOrWhiteSpace(skin.Name))
            .ToArray();
        if (skins.Length == 0)
        {
            return [BuildSkinPreview(skeletonData, "default", skin: null, includeSampledFrames, cancellationToken)];
        }

        return skins
            .Select(skin => BuildSkinPreview(skeletonData, skin.Name, skin, includeSampledFrames, cancellationToken))
            .ToArray();
    }

    private static SpineAnimationSkinPreviewDto BuildSkinPreview(
        Runtime.SkeletonData skeletonData,
        string skinName,
        Runtime.Skin? skin,
        bool includeSampledFrames,
        CancellationToken cancellationToken)
    {
        var attachmentMap = new Dictionary<string, SpineAnimationAttachmentDto>(StringComparer.Ordinal);
        var setupFrame = BuildSetupFrame(skeletonData, skin, attachmentMap);
        var clips = BuildAnimationClips(
            skeletonData,
            skin,
            attachmentMap,
            setupFrame,
            includeSampledFrames,
            cancellationToken);
        return new SpineAnimationSkinPreviewDto(
            skinName,
            attachmentMap.Values.ToArray(),
            clips,
            setupFrame);
    }

    private static SpineAnimationSkinPreviewDto ResolveInitialSkinPreview(
        IReadOnlyList<SpineAnimationSkinPreviewDto> skinPreviews)
    {
        var defaultPreview = skinPreviews.FirstOrDefault(static skin =>
            string.Equals(skin.Name, "default", StringComparison.OrdinalIgnoreCase));
        return defaultPreview ??
               skinPreviews.FirstOrDefault(static skin =>
                   skin.Animations.Any(static animation =>
                       animation.SampledFrames.Any(static frame => frame.DrawCommands.Count > 0))) ??
               skinPreviews[0];
    }

    private static SpineAnimationFrameDto BuildSetupFrame(
        Runtime.SkeletonData skeletonData,
        Runtime.Skin? skin,
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap)
    {
        var skeleton = CreateSkeleton(skeletonData, skin);
        skeleton.UpdateWorldTransform(Runtime.Skeleton.Physics.None);
        return new SpineAnimationFrameDto(
            0d,
            BuildDrawCommands(skeleton, attachmentMap));
    }

    private static IReadOnlyList<SpineAnimationClipDto> BuildAnimationClips(
        Runtime.SkeletonData skeletonData,
        Runtime.Skin? skin,
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap,
        SpineAnimationFrameDto setupFrame,
        bool includeSampledFrames,
        CancellationToken cancellationToken)
    {
        var samplingBudget = includeSampledFrames
            ? CreateSamplingBudget(setupFrame)
            : default;
        var clips = new List<SpineAnimationClipDto>();
        foreach (var animation in skeletonData.Animations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sampledFrames = includeSampledFrames
                ? BuildSampledFrames(
                    skeletonData,
                    skin,
                    animation,
                    attachmentMap,
                    samplingBudget,
                    cancellationToken)
                : Array.Empty<SpineAnimationFrameDto>();
            clips.Add(new SpineAnimationClipDto(
                animation.Name,
                animation.Duration,
                BoneTimelines: Array.Empty<SpineAnimationBoneTimelineDto>(),
                SlotAttachmentTimelines: Array.Empty<SpineAnimationSlotAttachmentTimelineDto>())
            {
                SampledFrames = sampledFrames
            });
        }

        return clips;
    }

    private static bool HasDrawablePreview(IReadOnlyList<SpineAnimationSkinPreviewDto> skinPreviews) =>
        skinPreviews.Any(static skin =>
            skin.SetupFrame?.DrawCommands.Count > 0 ||
            skin.Animations.Any(static clip =>
                clip.SampledFrames.Any(static frame => frame.DrawCommands.Count > 0)));

    private static IReadOnlyList<SpineAnimationFrameDto> BuildSampledFrames(
        Runtime.SkeletonData skeletonData,
        Runtime.Skin? skin,
        Runtime.Animation animation,
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap,
        SpineRuntimeSamplingBudget samplingBudget,
        CancellationToken cancellationToken)
    {
        return SpineRuntimeSamplingPolicy.CreateSampleTimes(animation.Duration, samplingBudget)
            .Select(time =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var skeleton = CreateSkeleton(skeletonData, skin);
                animation.Apply(
                    skeleton,
                    lastTime: -1f,
                    (float)time,
                    loop: false,
                    events: null,
                    alpha: 1f,
                    blend: Runtime.MixBlend.Setup,
                    direction: Runtime.MixDirection.In);
                skeleton.UpdateWorldTransform(Runtime.Skeleton.Physics.None);
                return new SpineAnimationFrameDto(
                    time,
                    BuildDrawCommands(skeleton, attachmentMap));
            })
            .ToArray();
    }

    private static Runtime.Skeleton CreateSkeleton(
        Runtime.SkeletonData skeletonData,
        Runtime.Skin? skin)
    {
        var skeleton = new Runtime.Skeleton(skeletonData);
        if (skin is not null)
        {
            skeleton.SetSkin(skin);
        }

        skeleton.SetToSetupPose();
        return skeleton;
    }

    private static SpineRuntimeSamplingBudget CreateSamplingBudget(SpineAnimationFrameDto setupFrame)
    {
        var drawCommandCount = setupFrame.DrawCommands.Count;
        var meshTriangleCount = setupFrame.DrawCommands.Sum(static command => command.MeshTriangles.Count);
        return SpineRuntimeSamplingPolicy.CreateBudget(drawCommandCount, meshTriangleCount);
    }

    private static IReadOnlyList<SpineAnimationDrawCommandDto> BuildDrawCommands(
        Runtime.Skeleton skeleton,
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap)
    {
        var commands = new List<SpineAnimationDrawCommandDto>();
        foreach (var slot in skeleton.DrawOrder)
        {
            var command = slot.Attachment switch
            {
                Runtime.RegionAttachment regionAttachment => TryCreateRegionDrawCommand(
                    slot,
                    regionAttachment,
                    attachmentMap),
                Runtime.MeshAttachment meshAttachment => TryCreateMeshDrawCommand(
                    slot,
                    meshAttachment,
                    attachmentMap),
                _ => null
            };
            if (command is null)
            {
                continue;
            }

            commands.Add(command);
        }

        return commands;
    }

    private static SpineAnimationDrawCommandDto? TryCreateRegionDrawCommand(
        Runtime.Slot slot,
        Runtime.RegionAttachment attachment,
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap)
    {
        if (attachment.Region is not Runtime.AtlasRegion region ||
            !IsDrawableRegion(region))
        {
            return null;
        }

        var transform = CreateRegionTransform(slot, attachment, region);
        var tint = CreateTint(slot, attachment);
        AddAttachment(attachmentMap, slot, attachment, region);
        return new SpineAnimationDrawCommandDto(
            slot.Data.Name,
            attachment.Name,
            region.name,
            region.page.name,
            region.x,
            region.y,
            region.packedWidth,
            region.packedHeight,
            Math.Max(1d, attachment.Width),
            Math.Max(1d, attachment.Height),
            transform.M11,
            transform.M12,
            transform.M21,
            transform.M22,
            transform.M31,
            transform.M32)
        {
            TintR = tint.R,
            TintG = tint.G,
            TintB = tint.B,
            TintA = tint.A
        };
    }

    private static SpineRuntimeMatrix CreateRegionTransform(
        Runtime.Slot slot,
        Runtime.RegionAttachment attachment,
        Runtime.AtlasRegion region)
    {
        var vertices = new float[8];
        attachment.ComputeWorldVertices(slot, vertices, offset: 0);
        var width = Math.Max(attachment.Width, 1f);
        var height = Math.Max(attachment.Height, 1f);
        var bl = new SpineRuntimePoint(vertices[0], vertices[1]);
        var ul = new SpineRuntimePoint(vertices[2], vertices[3]);
        var br = new SpineRuntimePoint(vertices[6], vertices[7]);
        var xAxis = (br - bl) / width;
        var yAxis = (ul - bl) / height;
        if (region.degrees == 90)
        {
            (xAxis, yAxis) = (yAxis, xAxis * -1f);
        }

        var translation = bl + (xAxis * (width / 2f)) + (yAxis * (height / 2f));
        return new SpineRuntimeMatrix(
            xAxis.X,
            xAxis.Y,
            yAxis.X,
            yAxis.Y,
            translation.X,
            translation.Y);
    }

    private static void AddAttachment(
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap,
        Runtime.Slot slot,
        Runtime.RegionAttachment attachment,
        Runtime.AtlasRegion region)
    {
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"{slot.Data.Name}\n{attachment.Name}\n{region.page.name}\n{region.name}");
        attachmentMap.TryAdd(key, new SpineAnimationAttachmentDto(
            slot.Data.Name,
            attachment.Name,
            region.name,
            region.page.name,
            region.x,
            region.y,
            region.packedWidth,
            region.packedHeight,
            region.degrees == 90,
            attachment.X,
            attachment.Y,
            attachment.Width,
            attachment.Height,
            attachment.Rotation,
            attachment.ScaleX,
            attachment.ScaleY));
    }

    private static bool IsDrawableRegion(Runtime.AtlasRegion region) =>
        region.page is not null &&
        region.packedWidth > 0 &&
        region.packedHeight > 0;

    private sealed class NoOpTextureLoader : Runtime.TextureLoader
    {
        public void Load(Runtime.AtlasPage page, string path)
        {
            page.rendererObject = path;
        }

        public void Unload(object texture)
        {
        }
    }

    private readonly record struct SpineRuntimePoint(float X, float Y)
    {
        public static SpineRuntimePoint operator +(SpineRuntimePoint left, SpineRuntimePoint right) =>
            new(left.X + right.X, left.Y + right.Y);

        public static SpineRuntimePoint operator -(SpineRuntimePoint left, SpineRuntimePoint right) =>
            new(left.X - right.X, left.Y - right.Y);

        public static SpineRuntimePoint operator *(SpineRuntimePoint point, float scalar) =>
            new(point.X * scalar, point.Y * scalar);

        public static SpineRuntimePoint operator /(SpineRuntimePoint point, float scalar) =>
            new(point.X / scalar, point.Y / scalar);
    }

    private readonly record struct SpineRuntimeMatrix(
        double M11,
        double M12,
        double M21,
        double M22,
        double M31,
        double M32);

}
