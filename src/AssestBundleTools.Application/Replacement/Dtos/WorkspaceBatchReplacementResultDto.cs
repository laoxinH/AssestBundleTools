using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示批量替换执行后的摘要结果。
/// </summary>
public sealed record WorkspaceBatchReplacementResultDto(
    WorkspaceId WorkspaceId,
    string SourceRootDirectory,
    WorkspaceBatchReplacementMatchingMode MatchingMode,
    int RequestedCandidateCount,
    int ReplacedAssetCount,
    int SkippedAssetCount,
    int FailedAssetCount,
    int ProcessedBundleCount,
    int ProcessedSpineGroupCount,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<WorkspaceBatchReplacementItemResultDto> ItemResults);

public sealed record WorkspaceBatchReplacementItemResultDto(
    string CandidateId,
    BundleId? BundleId,
    AssetId? AssetId,
    AssetType AssetType,
    string AssetName,
    string SourceFilePath,
    string? SpineGroupId,
    string? SpineGroupDisplayName,
    WorkspaceBatchReplacementItemStatus Status,
    string? DiagnosticCode = null);

public enum WorkspaceBatchReplacementItemStatus
{
    Replaced,
    Skipped,
    Failed
}
