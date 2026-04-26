using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Commands;

/// <summary>
/// 请求在当前主窗口的指定窗格中打开某个资源。
/// </summary>
public sealed record OpenAssetInPaneCommand(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    PaneId PaneId);
