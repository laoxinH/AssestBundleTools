using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示工作区级批量提取的结构化进度快照。
/// </summary>
public sealed record WorkspaceBatchExtractionProgressDto(
    WorkspaceBatchExtractionProgressKind Kind,
    int CompletedItemCount,
    int TotalItemCount,
    int CandidateBundleCount,
    int SucceededItemCount,
    int FailedItemCount,
    string? BundleDisplayName = null,
    string? ItemDisplayName = null,
    AssetType? AssetType = null,
    string? AssetTypeDisplayName = null,
    bool IsMemoryThrottled = false);

public enum WorkspaceBatchExtractionProgressKind
{
    Preparing,
    Asset,
    SpineGroup
}
