namespace AssestBundleTools.Application.Assets.Dtos;

public sealed record AssetPreviewFontDto(
    int SchemaVersion,
    string? FontName,
    double? FontSize,
    double? LineSpacing,
    double? Ascent,
    double? Descent,
    int? DefaultSize,
    int? AsciiStartOffset,
    double? Tracking,
    int? CharacterSpacing,
    int? CharacterPadding,
    int CharacterCount,
    long? FontDataSizeInBytes,
    AssetPreviewFontReferenceDto? Texture,
    AssetPreviewFontReferenceDto? DefaultMaterial,
    IReadOnlyList<AssetPreviewFontCharacterDto> Characters,
    bool IsCharacterListTruncated = false);

public sealed record AssetPreviewFontReferenceDto(
    long FileId,
    long PathId,
    string? Name,
    string? SerializedFileName,
    string? ExternalReferencePath,
    bool IsExternal);

public sealed record AssetPreviewFontCharacterDto(
    long? CodePoint,
    string? DisplayText,
    double? UvX,
    double? UvY,
    double? UvWidth,
    double? UvHeight,
    double? VertexX,
    double? VertexY,
    double? VertexWidth,
    double? VertexHeight,
    double? Advance,
    bool? IsFlipped);
