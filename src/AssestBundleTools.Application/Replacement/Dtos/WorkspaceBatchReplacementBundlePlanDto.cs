using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示批量替换计划中某个资源包的聚合摘要。
/// </summary>
public sealed record WorkspaceBatchReplacementBundlePlanDto(
    BundleId BundleId,
    string BundleDisplayName,
    int AssetCount,
    int WorkspaceReplacementCount,
    int FileReplacementCount);
