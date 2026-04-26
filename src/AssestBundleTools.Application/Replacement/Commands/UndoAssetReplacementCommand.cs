using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Commands;

/// <summary>
/// 请求撤销目标资源当前已应用的工作区替换。
/// </summary>
public sealed record UndoAssetReplacementCommand(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId);
