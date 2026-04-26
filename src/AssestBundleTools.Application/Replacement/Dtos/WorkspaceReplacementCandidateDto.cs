using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示可作为工作区内替换来源的候选资源。
/// </summary>
public sealed record WorkspaceReplacementCandidateDto(
    BundleId SourceBundleId,
    AssetId SourceAssetId,
    string SourceBundleDisplayName,
    string SourceAssetName,
    AssetType SourceAssetType);
