namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine JSON skeleton 的实验动画预览读模型。
/// </summary>
public sealed record SpineAnimationPreviewDto(
    SpineAssetGroupPartDto SkeletonAsset,
    string? SpineVersion,
    IReadOnlyList<SpineAnimationBoneDto> Bones,
    IReadOnlyList<SpineAnimationSlotDto> Slots,
    IReadOnlyList<SpineAnimationAttachmentDto> Attachments,
    IReadOnlyList<SpineAnimationClipDto> Animations,
    SpineAnimationRuntimeDto? Runtime = null);

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
    int DrawOrder);

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
