namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示 Spine 动画在某一时间点可由展示层直接绘制的中立帧。
/// </summary>
public sealed record SpineAnimationFrameDto(
    double Time,
    IReadOnlyList<SpineAnimationDrawCommandDto> DrawCommands);

public sealed record SpineAnimationDrawCommandDto(
    string SlotName,
    string AttachmentName,
    string RegionName,
    string PageName,
    int SourceX,
    int SourceY,
    int SourceWidth,
    int SourceHeight,
    double Width,
    double Height,
    double TransformM11,
    double TransformM12,
    double TransformM21,
    double TransformM22,
    double TransformM31,
    double TransformM32);
