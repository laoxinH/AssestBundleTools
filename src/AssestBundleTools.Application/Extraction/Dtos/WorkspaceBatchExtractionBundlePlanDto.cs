using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示批量提取预览中的单个资源包计划项。
/// </summary>
public sealed record WorkspaceBatchExtractionBundlePlanDto(
    BundleId BundleId,
    string BundleDisplayName,
    int AssetCount,
    long TotalSizeInBytes,
    int SpineGroupCount = 0);
