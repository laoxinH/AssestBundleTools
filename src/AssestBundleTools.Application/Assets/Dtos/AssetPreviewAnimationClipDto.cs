namespace AssestBundleTools.Application.Assets.Dtos;

public sealed record AssetPreviewAnimationClipDto(
    int SchemaVersion,
    double? SampleRate,
    double? StartTime,
    double? StopTime,
    double? Duration,
    long? EstimatedFrameCount,
    bool? IsLegacy,
    bool? LoopTime,
    bool? LoopBlend,
    bool? LoopBlendOrientation,
    bool? LoopBlendPositionY,
    bool? LoopBlendPositionXZ,
    int FloatCurveCount,
    int PositionCurveCount,
    int RotationCurveCount,
    int ScaleCurveCount,
    int PPtrCurveCount,
    IReadOnlyList<AssetPreviewAnimationEventDto> Events,
    bool IsEventListTruncated = false);

public sealed record AssetPreviewAnimationEventDto(
    double? Time,
    string? FunctionName,
    string? StringParameter,
    double? FloatParameter,
    long? IntParameter);
