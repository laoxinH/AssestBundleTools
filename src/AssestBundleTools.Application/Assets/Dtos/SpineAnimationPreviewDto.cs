namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine JSON skeleton 的实验动画预览读模型。
/// </summary>
public sealed partial record SpineAnimationPreviewDto(
    SpineAssetGroupPartDto SkeletonAsset,
    string? SpineVersion,
    IReadOnlyList<SpineAnimationBoneDto> Bones,
    IReadOnlyList<SpineAnimationSlotDto> Slots,
    IReadOnlyList<SpineAnimationAttachmentDto> Attachments,
    IReadOnlyList<SpineAnimationClipDto> Animations,
    SpineAnimationRuntimeDto? Runtime = null);

public sealed partial record SpineAnimationPreviewDto
{
    public IReadOnlyList<string> SkinNames { get; init; } = Array.Empty<string>();

    public SpineAnimationFrameDto? SetupFrame { get; init; }

    public IReadOnlyList<SpineAnimationSkinPreviewDto> SkinPreviews { get; init; } =
        Array.Empty<SpineAnimationSkinPreviewDto>();

    public SpineAnimationRuntimePlaybackSourceDto? PlaybackSource { get; init; }
}

public sealed record SpineAnimationSkinPreviewDto(
    string Name,
    IReadOnlyList<SpineAnimationAttachmentDto> Attachments,
    IReadOnlyList<SpineAnimationClipDto> Animations,
    SpineAnimationFrameDto? SetupFrame);

public sealed record SpineAnimationBoneDto(
    string Name,
    string? ParentName,
    double X,
    double Y,
    double Rotation,
    double ScaleX,
    double ScaleY);

public sealed record SpineAnimationSlotDto(
    string Name,
    string BoneName,
    string? SetupAttachmentName,
    int DrawOrder)
{
    public double ColorR { get; init; } = 1d;

    public double ColorG { get; init; } = 1d;

    public double ColorB { get; init; } = 1d;

    public double ColorA { get; init; } = 1d;
}

public sealed record SpineAnimationAttachmentDto(
    string SlotName,
    string Name,
    string RegionName,
    string PageName,
    int RegionX,
    int RegionY,
    int RegionWidth,
    int RegionHeight,
    bool IsRegionRotated,
    double X,
    double Y,
    double Width,
    double Height,
    double Rotation,
    double ScaleX,
    double ScaleY);

public sealed record SpineAnimationClipDto(
    string Name,
    double Duration,
    IReadOnlyList<SpineAnimationBoneTimelineDto> BoneTimelines,
    IReadOnlyList<SpineAnimationSlotAttachmentTimelineDto> SlotAttachmentTimelines)
{
    public IReadOnlyList<SpineAnimationSlotColorTimelineDto> SlotColorTimelines { get; init; } =
        Array.Empty<SpineAnimationSlotColorTimelineDto>();

    public IReadOnlyList<SpineAnimationDrawOrderTimelineFrameDto> DrawOrderTimelineFrames { get; init; } =
        Array.Empty<SpineAnimationDrawOrderTimelineFrameDto>();

    public IReadOnlyList<SpineAnimationFrameDto> SampledFrames { get; init; } =
        Array.Empty<SpineAnimationFrameDto>();
}

public sealed record SpineAnimationBoneTimelineDto(
    string BoneName,
    SpineAnimationBoneTimelineKind Kind,
    IReadOnlyList<SpineAnimationTimelineFrameDto> Frames);

public enum SpineAnimationBoneTimelineKind
{
    Rotate = 0,
    Translate = 1,
    Scale = 2
}

public sealed record SpineAnimationTimelineFrameDto(
    double Time,
    double? X = null,
    double? Y = null,
    double? Angle = null,
    double? ScaleX = null,
    double? ScaleY = null);

public sealed record SpineAnimationSlotAttachmentTimelineDto(
    string SlotName,
    IReadOnlyList<SpineAnimationSlotAttachmentFrameDto> Frames);

public sealed record SpineAnimationSlotAttachmentFrameDto(
    double Time,
    string? AttachmentName);

public sealed record SpineAnimationSlotColorTimelineDto(
    string SlotName,
    SpineAnimationSlotColorTimelineKind Kind,
    IReadOnlyList<SpineAnimationSlotColorFrameDto> Frames);

public enum SpineAnimationSlotColorTimelineKind
{
    Rgba = 0,
    Rgb = 1,
    Alpha = 2
}

public sealed record SpineAnimationSlotColorFrameDto(
    double Time,
    double? R = null,
    double? G = null,
    double? B = null,
    double? A = null);

public sealed record SpineAnimationDrawOrderTimelineFrameDto(
    double Time,
    IReadOnlyList<SpineAnimationDrawOrderOffsetDto> Offsets);

public sealed record SpineAnimationDrawOrderOffsetDto(
    string SlotName,
    int Offset);
