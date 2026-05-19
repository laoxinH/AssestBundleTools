using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示批量提取执行后的结果摘要。
/// </summary>
public sealed record WorkspaceBatchExtractionResultDto(
    WorkspaceId WorkspaceId,
    AssetType? AssetTypeFilter,
    string SuggestedOutputDirectory,
    int ExtractedAssetCount,
    int ExtractedBundleCount,
    DateTimeOffset CompletedAtUtc,
    int FailedAssetCount = 0,
    IReadOnlyList<AssetExtractionResultDto>? AssetResults = null,
    IReadOnlyList<WorkspaceBatchExtractionCategoryDto>? SelectedCategories = null,
    int ExtractedSpineGroupCount = 0,
    IReadOnlyList<SpineAssetGroupExtractionResultDto>? SpineGroupResults = null);
