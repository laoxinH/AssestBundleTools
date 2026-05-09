using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private static IReadOnlyDictionary<string, SpineAtlasRegionMatch> CreateAtlasRegionMap(
        IReadOnlyList<SpineAtlasPageDto> atlasPages)
    {
        var map = new Dictionary<string, SpineAtlasRegionMatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in atlasPages)
        {
            foreach (var region in page.Regions)
            {
                var key = NormalizeRegionName(region.Name);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                map.TryAdd(key, new SpineAtlasRegionMatch(page.Name, region));
            }
        }

        return map;
    }

    private static IReadOnlyList<SpineAnimationBoneDto> ParseBones(JsonElement root)
    {
        if (!TryGetArray(root, "bones", out var bonesElement))
        {
            return Array.Empty<SpineAnimationBoneDto>();
        }

        var bones = new List<SpineAnimationBoneDto>();
        foreach (var bone in bonesElement.EnumerateArray())
        {
            var name = GetString(bone, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            bones.Add(new SpineAnimationBoneDto(
                name,
                GetString(bone, "parent"),
                GetDouble(bone, "x", 0d),
                GetDouble(bone, "y", 0d),
                GetDouble(bone, "rotation", 0d),
                GetDouble(bone, "scaleX", 1d),
                GetDouble(bone, "scaleY", 1d)));
        }

        return bones;
    }

    private static IReadOnlyList<SpineAnimationSlotDto> ParseSlots(JsonElement root)
    {
        if (!TryGetArray(root, "slots", out var slotsElement))
        {
            return Array.Empty<SpineAnimationSlotDto>();
        }

        var slots = new List<SpineAnimationSlotDto>();
        var index = 0;
        foreach (var slot in slotsElement.EnumerateArray())
        {
            var name = GetString(slot, "name");
            var boneName = GetString(slot, "bone");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(boneName))
            {
                continue;
            }

            var color = ParseColor(GetString(slot, "color"), expectedLength: 8);
            slots.Add(new SpineAnimationSlotDto(
                name,
                boneName,
                GetString(slot, "attachment"),
                index++)
            {
                ColorR = color.R,
                ColorG = color.G,
                ColorB = color.B,
                ColorA = color.A
            });
        }

        return slots;
    }

    private static IReadOnlyList<SpineAnimationAttachmentDto> ParseAttachments(
        JsonElement root,
        IReadOnlyDictionary<string, SpineAtlasRegionMatch> atlasRegions)
    {
        if (!TryGetSkinAttachmentsRoot(root, out var attachmentsRoot))
        {
            return Array.Empty<SpineAnimationAttachmentDto>();
        }

        var attachments = new List<SpineAnimationAttachmentDto>();
        foreach (var slotProperty in attachmentsRoot.EnumerateObject())
        {
            if (slotProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var attachmentProperty in slotProperty.Value.EnumerateObject())
            {
                if (!TryCreateAttachment(slotProperty.Name, attachmentProperty, atlasRegions, out var attachment))
                {
                    continue;
                }

                attachments.Add(attachment);
            }
        }

        return attachments;
    }

    private static IReadOnlyList<SpineAnimationSkinAttachmentSet> ParseSkinAttachmentSets(
        JsonElement root,
        IReadOnlyDictionary<string, SpineAtlasRegionMatch> atlasRegions)
    {
        if (!root.TryGetProperty("skins", out var skinsElement))
        {
            return Array.Empty<SpineAnimationSkinAttachmentSet>();
        }

        var rawSets = skinsElement.ValueKind switch
        {
            JsonValueKind.Object => ParseObjectSkinAttachmentSets(skinsElement, atlasRegions),
            JsonValueKind.Array => ParseArraySkinAttachmentSets(skinsElement, atlasRegions),
            _ => Array.Empty<SpineAnimationSkinAttachmentSet>()
        };
        return ComposeSkinAttachmentSets(rawSets);
    }

    private static IReadOnlyList<SpineAnimationSkinAttachmentSet> ParseObjectSkinAttachmentSets(
        JsonElement skinsElement,
        IReadOnlyDictionary<string, SpineAtlasRegionMatch> atlasRegions)
    {
        var skins = new List<SpineAnimationSkinAttachmentSet>();
        foreach (var skinProperty in skinsElement.EnumerateObject())
        {
            if (skinProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var attachments = ParseAttachmentsFromRoot(skinProperty.Value, atlasRegions);
            if (attachments.Count > 0)
            {
                skins.Add(new SpineAnimationSkinAttachmentSet(skinProperty.Name, attachments));
            }
        }

        return skins;
    }

    private static IReadOnlyList<SpineAnimationSkinAttachmentSet> ParseArraySkinAttachmentSets(
        JsonElement skinsElement,
        IReadOnlyDictionary<string, SpineAtlasRegionMatch> atlasRegions)
    {
        var skins = new List<SpineAnimationSkinAttachmentSet>();
        foreach (var skinElement in skinsElement.EnumerateArray())
        {
            if (skinElement.ValueKind != JsonValueKind.Object ||
                !skinElement.TryGetProperty("attachments", out var attachmentsRoot) ||
                attachmentsRoot.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetString(skinElement, "name");
            var attachments = ParseAttachmentsFromRoot(attachmentsRoot, atlasRegions);
            if (!string.IsNullOrWhiteSpace(name) && attachments.Count > 0)
            {
                skins.Add(new SpineAnimationSkinAttachmentSet(name, attachments));
            }
        }

        return skins;
    }

    private static IReadOnlyList<SpineAnimationAttachmentDto> ParseAttachmentsFromRoot(
        JsonElement attachmentsRoot,
        IReadOnlyDictionary<string, SpineAtlasRegionMatch> atlasRegions)
    {
        var attachments = new List<SpineAnimationAttachmentDto>();
        foreach (var slotProperty in attachmentsRoot.EnumerateObject())
        {
            if (slotProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var attachmentProperty in slotProperty.Value.EnumerateObject())
            {
                if (TryCreateAttachment(slotProperty.Name, attachmentProperty, atlasRegions, out var attachment))
                {
                    attachments.Add(attachment);
                }
            }
        }

        return attachments;
    }

    private static IReadOnlyList<SpineAnimationSkinAttachmentSet> ComposeSkinAttachmentSets(
        IReadOnlyList<SpineAnimationSkinAttachmentSet> rawSets)
    {
        if (rawSets.Count <= 1)
        {
            return rawSets;
        }

        var defaultSet = rawSets.FirstOrDefault(static skin =>
            string.Equals(skin.Name, "default", StringComparison.OrdinalIgnoreCase));
        if (defaultSet is null)
        {
            return rawSets;
        }

        var defaultAttachments = defaultSet.Attachments
            .ToDictionary(
                static attachment => CreateAttachmentKey(attachment.SlotName, attachment.Name),
                static attachment => attachment,
                StringComparer.Ordinal);
        return rawSets
            .Select(skin => new SpineAnimationSkinAttachmentSet(
                skin.Name,
                ComposeSkinAttachments(defaultAttachments, skin.Attachments)))
            .ToArray();
    }

    private static IReadOnlyList<SpineAnimationAttachmentDto> ComposeSkinAttachments(
        IReadOnlyDictionary<string, SpineAnimationAttachmentDto> defaultAttachments,
        IReadOnlyList<SpineAnimationAttachmentDto> skinAttachments)
    {
        var composed = new Dictionary<string, SpineAnimationAttachmentDto>(defaultAttachments, StringComparer.Ordinal);
        foreach (var attachment in skinAttachments)
        {
            composed[CreateAttachmentKey(attachment.SlotName, attachment.Name)] = attachment;
        }

        return composed.Values.ToArray();
    }

    private static IReadOnlyList<string> ParseSkinNames(JsonElement root)
    {
        if (!root.TryGetProperty("skins", out var skinsElement))
        {
            return Array.Empty<string>();
        }

        if (skinsElement.ValueKind == JsonValueKind.Object)
        {
            return skinsElement
                .EnumerateObject()
                .Select(static property => property.Name)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        if (skinsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return skinsElement
            .EnumerateArray()
            .Where(static skin => skin.ValueKind == JsonValueKind.Object)
            .Select(static skin => GetString(skin, "name"))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryCreateAttachment(
        string slotName,
        JsonProperty attachmentProperty,
        IReadOnlyDictionary<string, SpineAtlasRegionMatch> atlasRegions,
        out SpineAnimationAttachmentDto attachment)
    {
        attachment = null!;
        var element = attachmentProperty.Value;
        if (element.ValueKind != JsonValueKind.Object ||
            !IsRegionAttachment(element))
        {
            return false;
        }

        var regionName = GetString(element, "path") ?? attachmentProperty.Name;
        if (!atlasRegions.TryGetValue(NormalizeRegionName(regionName), out var atlasRegion))
        {
            return false;
        }

        attachment = new SpineAnimationAttachmentDto(
            slotName,
            attachmentProperty.Name,
            atlasRegion.Region.Name,
            atlasRegion.PageName,
            atlasRegion.Region.X,
            atlasRegion.Region.Y,
            atlasRegion.Region.Width,
            atlasRegion.Region.Height,
            atlasRegion.Region.IsRotated,
            GetDouble(element, "x", 0d),
            GetDouble(element, "y", 0d),
            GetDouble(element, "width", atlasRegion.Region.Width),
            GetDouble(element, "height", atlasRegion.Region.Height),
            GetDouble(element, "rotation", 0d),
            GetDouble(element, "scaleX", 1d),
            GetDouble(element, "scaleY", 1d));
        return true;
    }

    private static IReadOnlyList<SpineAnimationClipDto> ParseAnimations(JsonElement root)
    {
        if (!TryGetObject(root, "animations", out var animationsElement))
        {
            return Array.Empty<SpineAnimationClipDto>();
        }

        var animations = new List<SpineAnimationClipDto>();
        foreach (var animationProperty in animationsElement.EnumerateObject())
        {
            var boneTimelines = ParseBoneTimelines(animationProperty.Value);
            var slotAttachmentTimelines = ParseSlotAttachmentTimelines(animationProperty.Value);
            var slotColorTimelines = ParseSlotColorTimelines(animationProperty.Value);
            var drawOrderTimelineFrames = ParseDrawOrderTimelineFrames(animationProperty.Value);
            var duration = ResolveDuration(
                boneTimelines,
                slotAttachmentTimelines,
                slotColorTimelines,
                drawOrderTimelineFrames);
            animations.Add(new SpineAnimationClipDto(
                animationProperty.Name,
                duration,
                boneTimelines,
                slotAttachmentTimelines)
            {
                SlotColorTimelines = slotColorTimelines,
                DrawOrderTimelineFrames = drawOrderTimelineFrames
            });
        }

        return animations
            .Where(static animation =>
                animation.Duration > 0d ||
                animation.BoneTimelines.Count > 0 ||
                animation.SlotAttachmentTimelines.Count > 0 ||
                animation.SlotColorTimelines.Count > 0 ||
                animation.DrawOrderTimelineFrames.Count > 0)
            .ToArray();
    }

    private static IReadOnlyList<SpineAnimationBoneTimelineDto> ParseBoneTimelines(JsonElement animationElement)
    {
        if (!TryGetObject(animationElement, "bones", out var bonesElement))
        {
            return Array.Empty<SpineAnimationBoneTimelineDto>();
        }

        var timelines = new List<SpineAnimationBoneTimelineDto>();
        foreach (var boneProperty in bonesElement.EnumerateObject())
        {
            if (boneProperty.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            AddBoneTimeline(timelines, boneProperty.Name, boneProperty.Value, "rotate", SpineAnimationBoneTimelineKind.Rotate);
            AddBoneTimeline(timelines, boneProperty.Name, boneProperty.Value, "translate", SpineAnimationBoneTimelineKind.Translate);
            AddBoneTimeline(timelines, boneProperty.Name, boneProperty.Value, "scale", SpineAnimationBoneTimelineKind.Scale);
        }

        return timelines;
    }

    private static void AddBoneTimeline(
        List<SpineAnimationBoneTimelineDto> timelines,
        string boneName,
        JsonElement boneTimelinesElement,
        string propertyName,
        SpineAnimationBoneTimelineKind kind)
    {
        if (!TryGetArray(boneTimelinesElement, propertyName, out var framesElement))
        {
            return;
        }

        var frames = framesElement
            .EnumerateArray()
            .Where(static frame => frame.ValueKind == JsonValueKind.Object)
            .Select(frame => CreateTimelineFrame(kind, frame))
            .OrderBy(static frame => frame.Time)
            .ToArray();
        if (frames.Length > 0)
        {
            timelines.Add(new SpineAnimationBoneTimelineDto(boneName, kind, frames));
        }
    }

    private static SpineAnimationTimelineFrameDto CreateTimelineFrame(
        SpineAnimationBoneTimelineKind kind,
        JsonElement frame)
    {
        return kind switch
        {
            SpineAnimationBoneTimelineKind.Rotate => new SpineAnimationTimelineFrameDto(
                GetDouble(frame, "time", 0d),
                Angle: GetDouble(frame, "angle", 0d)),
            SpineAnimationBoneTimelineKind.Scale => new SpineAnimationTimelineFrameDto(
                GetDouble(frame, "time", 0d),
                ScaleX: GetDouble(frame, "x", 1d),
                ScaleY: GetDouble(frame, "y", 1d)),
            _ => new SpineAnimationTimelineFrameDto(
                GetDouble(frame, "time", 0d),
                X: GetDouble(frame, "x", 0d),
                Y: GetDouble(frame, "y", 0d))
        };
    }

    private static IReadOnlyList<SpineAnimationSlotAttachmentTimelineDto> ParseSlotAttachmentTimelines(
        JsonElement animationElement)
    {
        if (!TryGetObject(animationElement, "slots", out var slotsElement))
        {
            return Array.Empty<SpineAnimationSlotAttachmentTimelineDto>();
        }

        var timelines = new List<SpineAnimationSlotAttachmentTimelineDto>();
        foreach (var slotProperty in slotsElement.EnumerateObject())
        {
            if (!TryGetArray(slotProperty.Value, "attachment", out var framesElement))
            {
                continue;
            }

            var frames = framesElement
                .EnumerateArray()
                .Where(static frame => frame.ValueKind == JsonValueKind.Object)
                .Select(static frame => new SpineAnimationSlotAttachmentFrameDto(
                    GetDouble(frame, "time", 0d),
                    GetString(frame, "name")))
                .OrderBy(static frame => frame.Time)
                .ToArray();
            if (frames.Length > 0)
            {
                timelines.Add(new SpineAnimationSlotAttachmentTimelineDto(slotProperty.Name, frames));
            }
        }

        return timelines;
    }

    private static double ResolveDuration(
        IReadOnlyList<SpineAnimationBoneTimelineDto> boneTimelines,
        IReadOnlyList<SpineAnimationSlotAttachmentTimelineDto> slotAttachmentTimelines,
        IReadOnlyList<SpineAnimationSlotColorTimelineDto> slotColorTimelines,
        IReadOnlyList<SpineAnimationDrawOrderTimelineFrameDto> drawOrderTimelineFrames)
    {
        var boneDuration = boneTimelines
            .SelectMany(static timeline => timeline.Frames)
            .Select(static frame => frame.Time)
            .DefaultIfEmpty(0d)
            .Max();
        var slotDuration = slotAttachmentTimelines
            .SelectMany(static timeline => timeline.Frames)
            .Select(static frame => frame.Time)
            .DefaultIfEmpty(0d)
            .Max();
        var colorDuration = slotColorTimelines
            .SelectMany(static timeline => timeline.Frames)
            .Select(static frame => frame.Time)
            .DefaultIfEmpty(0d)
            .Max();
        var drawOrderDuration = drawOrderTimelineFrames
            .Select(static frame => frame.Time)
            .DefaultIfEmpty(0d)
            .Max();
        return Math.Max(Math.Max(boneDuration, slotDuration), Math.Max(colorDuration, drawOrderDuration));
    }

    private sealed record SpineAtlasRegionMatch(
        string PageName,
        SpineAtlasRegionDto Region);

    private sealed record SpineAnimationSkinAttachmentSet(
        string Name,
        IReadOnlyList<SpineAnimationAttachmentDto> Attachments);
}
