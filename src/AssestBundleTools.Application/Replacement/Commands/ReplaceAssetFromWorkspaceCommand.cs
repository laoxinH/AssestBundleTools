using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 请求把当前工作区中的某个资源作为来源，替换目标资源。
/// </summary>
public sealed record ReplaceAssetFromWorkspaceCommand(
    WorkspaceId WorkspaceId,
    BundleId TargetBundleId,
    AssetId TargetAssetId,
    BundleId SourceBundleId,
    AssetId SourceAssetId,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null);
