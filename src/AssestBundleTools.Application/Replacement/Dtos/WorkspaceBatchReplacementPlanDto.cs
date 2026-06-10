using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示从外部目录生成的工作区批量替换计划。
/// </summary>
public sealed record WorkspaceBatchReplacementPlanDto(
    WorkspaceId WorkspaceId,
    string SourceRootDirectory,
    WorkspaceBatchReplacementMatchingMode MatchingMode,
    string? ManifestPath,
    string? ManifestWorkspaceKey,
    int TotalManifestItemCount,
    int ChangedCount,
    int UnchangedCount,
    int MissingCount,
    int IssueCount,
    IReadOnlyList<WorkspaceBatchReplacementCandidateDto> Candidates);

public sealed record WorkspaceBatchReplacementCandidateDto(
    string CandidateId,
    BundleId? BundleId,
    string BundleDisplayName,
    AssetId? AssetId,
    long PathId,
    AssetType AssetType,
    string? AssetTypeDisplayName,
    string AssetName,
    string? ContainerPath,
    string RelativePath,
    string FullPath,
    long? CurrentFileSizeInBytes,
    string? CurrentFileHashAlgorithm,
    string? CurrentFileHash,
    long ExportedFileSizeInBytes,
    string ExportedFileHashAlgorithm,
    string ExportedFileHash,
    string? SpineGroupId,
    string? SpineGroupDisplayName,
    string OutputRole,
    WorkspaceBatchReplacementMatchStatus MatchStatus,
    WorkspaceBatchReplacementMatchingMode MatchMode,
    bool IsDefaultSelected,
    IReadOnlyList<string> Diagnostics);
