using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示编辑保存时随主资产一起应用的关联资源文件替换候选。
/// </summary>
internal sealed record AssociatedAssetFileReplacementCandidate(
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string TargetAssetName,
    AssetType TargetAssetType,
    FileReplacementCandidateDto Candidate);
