using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 描述一次预览查询内的临时外部来源覆盖，不改变工作区已应用替换状态。
/// </summary>
public sealed record AssetPreviewSourceOverrideDto(
    BundleId BundleId,
    AssetId AssetId,
    string SourceFilePath);
