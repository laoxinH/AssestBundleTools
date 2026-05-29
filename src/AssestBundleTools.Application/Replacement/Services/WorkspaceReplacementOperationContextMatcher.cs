using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 校验替换操作记录是否可从当前资源上下文执行撤销或重做。
/// </summary>
internal static class WorkspaceReplacementOperationContextMatcher
{
    public static bool CanUseFromContext(
        WorkspaceOperationRecordDto operation,
        WorkspaceId workspaceId,
        BundleId contextBundleId,
        AssetId contextAssetId,
        IWorkspaceAssociatedAssetImpactRegistry? associatedAssetImpactRegistry)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (WorkspaceOperationRelationMatcher.OperationConcernsTarget(
                operation,
                contextBundleId.Value,
                contextAssetId.Value))
        {
            return true;
        }

        if (associatedAssetImpactRegistry is null ||
            !BundleId.TryParse(operation.Target?.BundleId, out var targetBundleId) ||
            !AssetId.TryParse(operation.Target?.AssetId, out var targetAssetId) ||
            !associatedAssetImpactRegistry.TryGetAssociatedAssetImpacts(
                workspaceId,
                targetBundleId,
                targetAssetId,
                out var impacts))
        {
            return false;
        }

        return impacts.Any(impact =>
            impact.BundleId.Equals(contextBundleId) &&
            impact.AssetId.Equals(contextAssetId));
    }
}
