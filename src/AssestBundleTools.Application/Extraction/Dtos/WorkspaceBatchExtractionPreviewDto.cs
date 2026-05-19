using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示当前工作区的批量提取预览摘要。
/// </summary>
/// <remarks>
/// 当前阶段仍然只负责“预览”和“状态回写骨架”，
/// 因此 DTO 重点承载筛选结果、建议输出目录和资源包级聚合信息。
/// </remarks>
public sealed record WorkspaceBatchExtractionPreviewDto(
    WorkspaceId WorkspaceId,
    string WorkspaceDisplayName,
    AssetType? AssetTypeFilter,
    string SuggestedOutputDirectory,
    int CandidateAssetCount,
    int CandidateBundleCount,
    long TotalSizeInBytes,
    IReadOnlyList<WorkspaceBatchExtractionBundlePlanDto> BundlePlans,
    IReadOnlyList<WorkspaceBatchExtractionCategoryDto>? SelectedCategories = null,
    int CandidateSpineGroupCount = 0,
    IReadOnlyList<WorkspaceBatchExtractionCategoryPlanDto>? CategoryPlans = null);
