namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 记录一次工作区批量导出的稳定身份映射，供后续批量替换精确回读。
/// </summary>
public sealed record WorkspaceExportManifestDto(
    int SchemaVersion,
    string WorkspaceKey,
    DateTimeOffset ExportedAtUtc,
    string OutputRoot,
    IReadOnlyList<WorkspaceExportManifestItemDto> Items);

public sealed record WorkspaceExportManifestItemDto(
    string RelativePath,
    string BundleId,
    string BundleDisplayName,
    string AssetId,
    long PathId,
    string AssetType,
    string? AssetTypeDisplayName,
    string OriginalName,
    string DisambiguatedName,
    string? ContainerPath,
    WorkspaceExportManifestOutputRole OutputRole,
    string? SpineGroupId,
    string? SpineGroupDisplayName,
    long ExportedFileSizeInBytes,
    string ExportedFileHashAlgorithm,
    string ExportedFileHash,
    DateTimeOffset ExportedFileLastWriteTimeUtc);
