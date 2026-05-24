using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 描述编辑保存时需要一并应用的关联资源文件替换。
/// </summary>
public sealed record AssociatedAssetFileReplacementDto(
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string SourceFilePath,
    AssetType SourceAssetType);
