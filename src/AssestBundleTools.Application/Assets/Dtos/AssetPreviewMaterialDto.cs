namespace AssestBundleTools.Application.Assets.Dtos;

public sealed record AssetPreviewMaterialDto(
    int SchemaVersion,
    AssetPreviewMaterialShaderDto? Shader,
    IReadOnlyList<AssetPreviewMaterialTexturePropertyDto> TextureProperties,
    IReadOnlyList<AssetPreviewMaterialFloatPropertyDto> FloatProperties,
    IReadOnlyList<AssetPreviewMaterialIntegerPropertyDto> IntegerProperties,
    IReadOnlyList<AssetPreviewMaterialColorPropertyDto> ColorProperties,
    bool IsTexturePropertyListTruncated = false,
    bool IsFloatPropertyListTruncated = false,
    bool IsIntegerPropertyListTruncated = false,
    bool IsColorPropertyListTruncated = false);

public sealed record AssetPreviewMaterialShaderDto(
    long FileId,
    long PathId,
    string? Name,
    string? SerializedFileName,
    string? ExternalReferencePath,
    bool IsExternal);

public sealed record AssetPreviewMaterialTexturePropertyDto(
    string Name,
    long FileId,
    long? PathId,
    string? SerializedFileName,
    string? ExternalReferencePath,
    bool IsExternal,
    double? ScaleX,
    double? ScaleY,
    double? OffsetX,
    double? OffsetY);

public sealed record AssetPreviewMaterialFloatPropertyDto(
    string Name,
    double Value);

public sealed record AssetPreviewMaterialIntegerPropertyDto(
    string Name,
    long Value);

public sealed record AssetPreviewMaterialColorPropertyDto(
    string Name,
    double R,
    double G,
    double B,
    double A);
