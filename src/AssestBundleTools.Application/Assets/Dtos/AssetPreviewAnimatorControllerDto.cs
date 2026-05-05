namespace AssestBundleTools.Application.Assets.Dtos;

public sealed record AssetPreviewAnimatorControllerDto(
    int SchemaVersion,
    string? Name,
    int LayerCount,
    int ParameterCount,
    int StateMachineCount,
    int StateCount,
    int TransitionCount,
    int AnyStateTransitionCount,
    int ReferenceCount,
    IReadOnlyList<AssetPreviewAnimatorControllerLayerDto> Layers,
    IReadOnlyList<AssetPreviewAnimatorControllerParameterDto> Parameters,
    IReadOnlyList<AssetPreviewAnimatorControllerReferenceDto> References,
    IReadOnlyList<AssetPreviewAnimatorControllerContainerDto>? Containers = null,
    bool IsLayerListTruncated = false,
    bool IsParameterListTruncated = false,
    bool IsReferenceListTruncated = false,
    bool IsContainerListTruncated = false);

public sealed record AssetPreviewAnimatorControllerLayerDto(
    int Index,
    string? Name,
    double? DefaultWeight,
    int? BlendingMode,
    int? SyncedLayerIndex,
    bool? IkPass,
    long? StateMachinePathId,
    long? AvatarMaskPathId,
    int? StateCount,
    int? TransitionCount);

public sealed record AssetPreviewAnimatorControllerParameterDto(
    int Index,
    string? Name,
    int? Type,
    string? TypeName,
    double? DefaultFloat,
    long? DefaultInt,
    bool? DefaultBool);

public sealed record AssetPreviewAnimatorControllerReferenceDto(
    string Path,
    string Name,
    long FileId,
    long? PathId,
    string? TypeName,
    string? TargetName,
    string? SerializedFileName,
    string? ExternalReferencePath,
    bool IsExternal);

public sealed record AssetPreviewAnimatorControllerContainerDto(
    string Path,
    string Name,
    AssetPreviewAnimatorControllerContainerKind Kind,
    string? TypeName,
    int ChildCount,
    int Depth);

public enum AssetPreviewAnimatorControllerContainerKind
{
    Unknown = 0,
    Layer = 1,
    Parameter = 2,
    StateMachine = 3,
    State = 4,
    Transition = 5,
    AnyStateTransition = 6,
    BlendTree = 7,
    Value = 8
}
