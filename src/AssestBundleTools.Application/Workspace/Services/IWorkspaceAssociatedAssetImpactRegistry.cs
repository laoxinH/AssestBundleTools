using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 查询资源修改对依赖资源造成的读模型影响。
/// </summary>
public interface IWorkspaceAssociatedAssetImpactRegistry
{
    bool TryGetAssociatedAssetImpacts(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out IReadOnlyList<WorkspaceAssociatedAssetImpactDto> impacts);
}
