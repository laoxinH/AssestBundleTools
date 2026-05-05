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
    bool IsEventListTruncated = false,
    int EventCount = 0,
    int CurveBindingCount = 0,
    IReadOnlyList<AssetPreviewAnimationCurveBindingDto>? CurveBindings = null,
    bool IsCurveBindingListTruncated = false);

public sealed record AssetPreviewAnimationEventDto(
    double? Time,
    string? FunctionName,
    string? StringParameter,
    double? FloatParameter,
    long? IntParameter);

public enum AssetPreviewAnimationCurveBindingKind
{
    Unknown = 0,
    Float = 1,
    Position = 2,
    Rotation = 3,
    Scale = 4,
    PPtr = 5,
    Generic = 6
}

public sealed record AssetPreviewAnimationCurveBindingDto(
    AssetPreviewAnimationCurveBindingKind Kind,
    string? Path,
    string? Attribute,
    int? ClassId,
    long? ScriptFileId,
    long? ScriptPathId,
    long? PathHash,
    long? AttributeId,
    int? TypeId,
    byte? CustomType,
    bool? IsPPtrCurve,
    int CurveCount,
    int? KeyframeCount);
