namespace AssestBundleTools.Application.Assets.Dtos;

public sealed record AssetPreviewMonoBehaviourDto(
    int SchemaVersion,
    string? Name,
    bool? IsEnabled,
    AssetPreviewMonoBehaviourScriptDto? Script,
    int FieldCount,
    int ScalarFieldCount,
    int ReferenceCount,
    int ArrayFieldCount,
    IReadOnlyList<AssetPreviewMonoBehaviourFieldDto> Fields,
    IReadOnlyList<AssetPreviewMonoBehaviourReferenceDto> References,
    IReadOnlyList<AssetPreviewMonoBehaviourFieldGroupDto>? FieldGroups = null,
    IReadOnlyList<AssetPreviewMonoBehaviourContainerDto>? Containers = null,
    bool IsFieldListTruncated = false,
    bool IsReferenceListTruncated = false);

public sealed record AssetPreviewMonoBehaviourScriptDto(
    long FileId,
    long? PathId,
    string? Name,
    string? ClassName,
    string? Namespace,
    string? AssemblyName,
    string? SerializedFileName,
    string? ExternalReferencePath,
    bool IsExternal);

public sealed record AssetPreviewMonoBehaviourFieldDto(
    string Path,
    string Name,
    string? TypeName,
    AssetPreviewInfoValueKind ValueKind,
    long? Int64Value = null,
    double? DoubleValue = null,
    bool? BooleanValue = null,
    string? StringValue = null,
    int? ChildCount = null);

public sealed record AssetPreviewMonoBehaviourReferenceDto(
    string Path,
    string Name,
    long FileId,
    long? PathId,
    string? TypeName,
    string? TargetName,
    string? SerializedFileName,
    string? ExternalReferencePath,
    bool IsExternal);

public sealed record AssetPreviewMonoBehaviourFieldGroupDto(
    string Name,
    int FieldCount,
    int ScalarFieldCount,
    int ReferenceCount,
    int ArrayFieldCount,
    bool IsUnityBuiltin = false);

public sealed record AssetPreviewMonoBehaviourContainerDto(
    string Path,
    string Name,
    string? TypeName,
    int ChildCount,
    int Depth);
