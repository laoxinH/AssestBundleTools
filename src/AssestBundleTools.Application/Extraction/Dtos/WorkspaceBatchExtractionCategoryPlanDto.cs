namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示工作区批量提取窗口中可选导出类别的候选统计。
/// </summary>
public sealed record WorkspaceBatchExtractionCategoryPlanDto(
    WorkspaceBatchExtractionCategoryDto Category,
    int CandidateCount,
    long TotalSizeInBytes);
