namespace AssestBundleTools.Application.Assets.Dtos;

public sealed record AssetPreviewGameObjectDto(
    int SchemaVersion,
    string? Name,
    int? Layer,
    string? Tag,
    bool? IsActive,
    int? StaticEditorFlags,
    int ComponentCount,
    IReadOnlyList<AssetPreviewGameObjectComponentDto> Components,
    bool IsComponentListTruncated = false,
    AssetPreviewGameObjectTransformDto? Transform = null);

public sealed record AssetPreviewGameObjectComponentDto(
    int Index,
    long FileId,
    long? PathId,
    int? TypeId,
    string? TypeName,
    string? Name,
    string? SerializedFileName,
    string? ExternalReferencePath,
    bool IsExternal,
    bool? IsEnabled,
    long? ScriptPathId,
    string? ScriptName,
    IReadOnlyList<AssetPreviewGameObjectReferenceDto>? References = null,
    IReadOnlyList<AssetPreviewGameObjectComponentSummaryDto>? Summaries = null);

public sealed record AssetPreviewGameObjectComponentSummaryDto(
    AssetPreviewGameObjectComponentSummaryKey Key,
    AssetPreviewInfoValueKind ValueKind,
    long? Int64Value = null,
    double? DoubleValue = null,
    bool? BooleanValue = null,
    string? StringValue = null);

public enum AssetPreviewGameObjectComponentSummaryKey
{
    MaterialCount = 0,
    MeshReference = 1,
    Script = 2,
    Enabled = 3,
    CastShadows = 4,
    ReceiveShadows = 5,
    Controller = 6,
    Avatar = 7,
    AudioClip = 8,
    Loop = 9,
    PlayOnAwake = 10,
    Volume = 11,
    IsTrigger = 12,
    RootMotion = 13,
    ParticleLooping = 14,
    ParticlePrewarm = 15,
    ParticleDuration = 16,
    SpriteReference = 17
}

public sealed record AssetPreviewGameObjectReferenceDto(
    string Role,
    long FileId,
    long? PathId,
    string? TypeName,
    string? Name,
    string? SerializedFileName,
    string? ExternalReferencePath,
    bool IsExternal);

public sealed record AssetPreviewGameObjectTransformDto(
    long? PathId,
    string? TypeName,
    long? ParentPathId,
    int ChildCount,
    double? LocalPositionX,
    double? LocalPositionY,
    double? LocalPositionZ,
    double? LocalRotationX,
    double? LocalRotationY,
    double? LocalRotationZ,
    double? LocalRotationW,
    double? LocalScaleX,
    double? LocalScaleY,
    double? LocalScaleZ);
