using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 为当前资源基于已选文件生成文件替换候选。
/// </summary>
public sealed record GetFileReplacementCandidateQuery(
    WorkspaceId WorkspaceId,
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    string SourceFilePath);
