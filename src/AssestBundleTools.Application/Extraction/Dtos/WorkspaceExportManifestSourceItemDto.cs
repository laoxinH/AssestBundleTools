using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 描述 manifest 写出前由 Application 确定的稳定资产身份。
/// </summary>
public sealed record WorkspaceExportManifestSourceItemDto(
    string OutputPath,
    BundleId BundleId,
    string BundleDisplayName,
    AssetId AssetId,
    long PathId,
    string AssetType,
    string? AssetTypeDisplayName,
    string OriginalName,
    string DisambiguatedName,
    string? ContainerPath,
    WorkspaceExportManifestOutputRole OutputRole,
    string? SpineGroupId = null,
    string? SpineGroupDisplayName = null);

public sealed record WorkspaceExportManifestSaveRequestDto(
    string WorkspaceKey,
    DateTimeOffset ExportedAtUtc,
    string OutputRoot,
    IReadOnlyList<WorkspaceExportManifestSourceItemDto> Items);

public sealed record WorkspaceExportManifestSaveResultDto(
    string ManifestPath,
    WorkspaceExportManifestDto Manifest);
