using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示工作区级批量替换的结构化进度快照。
/// </summary>
public sealed record WorkspaceBatchReplacementProgressDto(
    WorkspaceBatchReplacementProgressKind Kind,
    int CompletedItemCount,
    int TotalItemCount,
    int SucceededItemCount,
    int SkippedItemCount,
    int FailedItemCount,
    string? BundleDisplayName = null,
    string? ItemDisplayName = null,
    AssetType? AssetType = null,
    string? AssetTypeDisplayName = null,
    string? SpineGroupDisplayName = null);

public enum WorkspaceBatchReplacementProgressKind
{
    Preparing,
    Asset,
    SpineGroup
}
