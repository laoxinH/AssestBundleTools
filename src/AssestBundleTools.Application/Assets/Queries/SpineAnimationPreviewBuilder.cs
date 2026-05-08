using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private readonly IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> _assetPreviewQueryHandler;

    public SpineAnimationPreviewBuilder(
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewQueryHandler,
        ISpineAnimationRuntimePreviewService? runtimePreviewService = null)
    {
        _assetPreviewQueryHandler = assetPreviewQueryHandler ?? throw new ArgumentNullException(nameof(assetPreviewQueryHandler));
        _runtimePreviewService = runtimePreviewService ?? NoOpRuntimePreviewService;
    }

    public async Task<SpineAnimationPreviewBuildResult> BuildAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        IReadOnlyList<SpineAssetGroupPartDto> skeletonAssets,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<SpineAssetGroupDiagnosticDto>();
        if (atlasPages.Count == 0)
        {
            return SpineAnimationPreviewBuildResult.Empty;
        }

        var skeletonAsset = skeletonAssets.FirstOrDefault(static asset => !IsBinarySkeleton(asset));
        if (skeletonAsset is null)
        {
            var binarySkeletonAsset = skeletonAssets.FirstOrDefault(static asset => IsBinarySkeleton(asset));
            if (binarySkeletonAsset is not null)
            {
                diagnostics.Add(CreateUnsupportedFeatureDiagnostic(
                    groupKey,
                    binarySkeletonAsset,
                    SpineAnimationUnsupportedFeatureKind.BinarySkeleton));
            }

            return SpineAnimationPreviewBuildResult.EmptyWithDiagnostics(diagnostics);
        }

        var previewResult = await _assetPreviewQueryHandler.HandleAsync(
            new GetAssetPreviewQuery(
                query.WorkspaceId,
                query.WorkspaceKey,
                query.WorkspaceDataRootPath,
                skeletonAsset.BundleId,
                skeletonAsset.AssetId),
            cancellationToken);
        if (previewResult.IsCancelled)
        {
            return SpineAnimationPreviewBuildResult.Cancelled;
        }

        if (!TryReadSkeletonJson(previewResult, out var skeletonJson))
        {
            return SpineAnimationPreviewBuildResult.Empty;
        }

        var runtimePreviewResult = await TryBuildRuntimePreviewAsync(
            skeletonAsset,
            groupKey,
            skeletonJson,
            atlasPages,
            cancellationToken);
        if (runtimePreviewResult.IsCancelled)
        {
            return SpineAnimationPreviewBuildResult.Cancelled;
        }

        if (runtimePreviewResult.IsHandled)
        {
            return new SpineAnimationPreviewBuildResult(
                false,
                runtimePreviewResult.Preview,
                runtimePreviewResult.Diagnostics);
        }

        return TryBuildPreview(skeletonAsset, groupKey, skeletonJson, atlasPages, out var preview, out var previewDiagnostics)
            ? new SpineAnimationPreviewBuildResult(false, preview, previewDiagnostics)
            : SpineAnimationPreviewBuildResult.EmptyWithDiagnostics(previewDiagnostics);
    }

    private static bool TryBuildPreview(
        SpineAssetGroupPartDto skeletonAsset,
        string groupKey,
        string skeletonJson,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        out SpineAnimationPreviewDto? preview,
        out IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics)
    {
        preview = null;
        diagnostics = Array.Empty<SpineAssetGroupDiagnosticDto>();
        try
        {
            using var document = JsonDocument.Parse(skeletonJson);
            var root = document.RootElement;
            var spineVersion = ResolveSkeletonMetadataText(root, "spine");
            var runtime = SpineAnimationRuntimeResolver.Resolve(spineVersion);
            var unsupportedFeatureDiagnostics = BuildUnsupportedFeatureDiagnostics(skeletonAsset, groupKey, root);
            var runtimeDiagnostics = BuildRuntimeDiagnostics(skeletonAsset, groupKey, runtime);
            var atlasRegions = CreateAtlasRegionMap(atlasPages);
            var bones = ParseBones(root);
            var slots = ParseSlots(root);
            var attachments = ParseAttachments(root, atlasRegions);
            var animations = ParseAnimations(root);
            if (bones.Count == 0 || slots.Count == 0 || attachments.Count == 0 || animations.Count == 0)
            {
                return false;
            }

            diagnostics = unsupportedFeatureDiagnostics
                .Concat(runtimeDiagnostics)
                .ToArray();
            preview = new SpineAnimationPreviewDto(
                skeletonAsset,
                spineVersion,
                bones,
                slots,
                attachments,
                BuildSampledAnimationClips(bones, slots, attachments, animations),
                runtime);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

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

            slots.Add(new SpineAnimationSlotDto(
                name,
                boneName,
                GetString(slot, "attachment"),
                index++));
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
            var duration = ResolveDuration(boneTimelines, slotAttachmentTimelines);
            animations.Add(new SpineAnimationClipDto(
                animationProperty.Name,
                duration,
                boneTimelines,
                slotAttachmentTimelines));
        }

        return animations
            .Where(static animation =>
                animation.Duration > 0d ||
                animation.BoneTimelines.Count > 0 ||
                animation.SlotAttachmentTimelines.Count > 0)
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
        IReadOnlyList<SpineAnimationSlotAttachmentTimelineDto> slotAttachmentTimelines)
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
        return Math.Max(boneDuration, slotDuration);
    }

    private sealed record SpineAtlasRegionMatch(
        string PageName,
        SpineAtlasRegionDto Region);
}

internal sealed record SpineAnimationPreviewBuildResult(
    bool IsCancelled,
    SpineAnimationPreviewDto? Preview,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
{
    public static SpineAnimationPreviewBuildResult Empty { get; } =
        new(false, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationPreviewBuildResult Cancelled { get; } =
        new(true, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationPreviewBuildResult EmptyWithDiagnostics(
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics) =>
        new(false, null, diagnostics);
}
