using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示右侧详情检查器所需的资源详情数据。
/// </summary>
public sealed record AssetDetailsDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string Name,
    string AssetPath,
    string ContainerPath,
    AssetType AssetType,
    long SizeInBytes,
    long? PreferredDisplaySizeInBytes,
    long PathId,
    bool IsModified,
    bool CanExtract,
    bool CanReplaceFromFile,
    bool CanReplaceFromWorkspace,
    string? WorkspaceReplacementSourceBundleDisplayName,
    string? WorkspaceReplacementSourceAssetName,
    AssetType? WorkspaceReplacementSourceAssetType,
    string? FileReplacementSourceFileName,
    string? FileReplacementSourcePath,
    AssetType? FileReplacementSourceAssetType,
    DateTimeOffset? LastExtractedAtUtc,
    IReadOnlyList<AssetDependencyDto> Dependencies,
    IReadOnlyList<AssetDependencyDto>? ReferencedBy = null,
    IReadOnlyList<AssetDependencyDto>? DependencyClosure = null,
    string? AssetTypeDisplayName = null,
    IReadOnlyList<BundleExternalDependencyDto>? BundleExternalDependencies = null,
    string? SerializedFileName = null,
    IReadOnlyList<AssetDependencyDto>? TextureAssociations = null,
    BundleId? WorkspaceReplacementSourceBundleId = null,
    AssetId? WorkspaceReplacementSourceAssetId = null,
    bool CanRedoReplacement = false,
    IReadOnlyList<AssetChangeRecordDto>? ChangeRecords = null);
