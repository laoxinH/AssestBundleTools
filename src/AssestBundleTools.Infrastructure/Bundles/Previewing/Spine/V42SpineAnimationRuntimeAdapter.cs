using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using Runtime = AssestBundleTools.Infrastructure.ThirdParty.Spine.V42;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal sealed partial class V42SpineAnimationRuntimeAdapter :
    ISpineAnimationRuntimeAdapter,
    ISpineAnimationRuntimePlaybackAdapter
{
    private static readonly int[] QuadTriangleIndices = [0, 1, 2, 2, 3, 0];

    public string AdapterKey => "V42";

    public SpineAnimationRuntimePreviewResultDto TryBuildPreview(
        SpineAnimationRuntimePreviewRequestDto request,
        IAssetPreviewTextSourceReader textSourceReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(textSourceReader);
        if (cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePreviewResultDto.Cancelled;
        }

        if (!SpineRuntimeAtlasInputGuard.CanAttemptRuntimeParse(request.AtlasTexts, request.AtlasTextSources) ||
            (string.IsNullOrWhiteSpace(request.SkeletonJson) &&
             request.SkeletonJsonSource is null &&
             request.SkeletonBinaryContent is not { Length: > 0 }))
        {
            return SpineAnimationRuntimePreviewResultDto.NotHandled;
        }

        try
        {
            var skeletonData = ReadSkeletonData(request, textSourceReader);
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
        var clipper = new Runtime.SkeletonClipping();
        foreach (var slot in skeleton.DrawOrder)
        {
            if (!slot.Bone.Active)
            {
                continue;
            }

            if (slot.Attachment is Runtime.ClippingAttachment clippingAttachment)
            {
                clipper.ClipStart(slot, clippingAttachment);
                continue;
            }

            var command = slot.Attachment switch
            {
                Runtime.RegionAttachment regionAttachment => TryCreateRegionDrawCommand(
                    slot,
                    regionAttachment,
                    attachmentMap,
                    clipper),
                Runtime.MeshAttachment meshAttachment => TryCreateMeshDrawCommand(
                    slot,
                    meshAttachment,
                    attachmentMap,
                    clipper),
                _ => null
            };
            if (command is null)
            {
                clipper.ClipEnd(slot);
                continue;
            }

            commands.Add(command);
            clipper.ClipEnd(slot);
        }

        clipper.ClipEnd();
        return commands;
    }

    private static SpineAnimationDrawCommandDto? TryCreateRegionDrawCommand(
        Runtime.Slot slot,
        Runtime.RegionAttachment attachment,
        Dictionary<string, SpineAnimationAttachmentDto> attachmentMap,
        Runtime.SkeletonClipping clipper)
    {
        if (attachment.Region is not Runtime.AtlasRegion region ||
            !IsDrawableRegion(region))
        {
            return null;
        }

        var geometry = TryCreateRegionGeometry(slot, attachment, clipper);
        if (geometry is null)
        {
            return null;
        }

        var bounds = geometry.Value.Bounds;
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
            bounds.Width,
            bounds.Height,
            1d,
            0d,
            0d,
            1d,
            bounds.CenterX,
            bounds.CenterY)
        {
            TintR = tint.R,
            TintG = tint.G,
            TintB = tint.B,
            TintA = tint.A,
            MeshVertices = geometry.Value.Vertices,
            MeshTriangles = geometry.Value.Triangles
        };
    }

    private static SpineRuntimeMeshGeometry? TryCreateRegionGeometry(
        Runtime.Slot slot,
        Runtime.RegionAttachment attachment,
        Runtime.SkeletonClipping clipper)
    {
        var worldVertices = new float[8];
        attachment.ComputeWorldVertices(slot, worldVertices, offset: 0);
        var uvs = attachment.UVs;
        if (uvs is not { Length: >= 8 })
        {
            return null;
        }

        return clipper.IsClipping
            ? CreateClippedGeometry(clipper, worldVertices, uvs, QuadTriangleIndices)
            : CreateGeometry(worldVertices, uvs, QuadTriangleIndices);
    }

    private static SpineRuntimeMeshGeometry? CreateClippedGeometry(
        Runtime.SkeletonClipping clipper,
        float[] worldVertices,
        float[] uvs,
        int[] triangles)
    {
        clipper.ClipTriangles(
            worldVertices,
            triangles,
            triangles.Length,
            uvs);
        if (clipper.ClippedVertices.Count < 6 ||
            clipper.ClippedTriangles.Count < 3 ||
            clipper.ClippedUVs.Count < clipper.ClippedVertices.Count)
        {
            return null;
        }

        var vertices = clipper.ClippedVertices.Items;
        var clippedUvs = clipper.ClippedUVs.Items;
        var clippedTriangles = clipper.ClippedTriangles.Items;
        return CreateGeometry(
            vertices,
            clippedUvs,
            clippedTriangles,
            clipper.ClippedVertices.Count,
            clipper.ClippedTriangles.Count);
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

}
