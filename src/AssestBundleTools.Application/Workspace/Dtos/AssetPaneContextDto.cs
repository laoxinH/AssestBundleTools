using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示资源在某个窗格中打开后的上下文信息。
/// </summary>
public sealed record AssetPaneContextDto(
    WorkspaceId WorkspaceId,
    PaneId PaneId,
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    WorkspaceMode WorkspaceMode);
