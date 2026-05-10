using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Queries;

internal static class SpineAssetGroupDetectionDetailsCollector
{
    public static IReadOnlyDictionary<string, AssetDetailsDto> Collect(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        IReadOnlyList<AssetListResultDto> assetLists,
        CancellationToken cancellationToken)
    {
        var detailsByAssetKey = new Dictionary<string, AssetDetailsDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var assetList in assetLists)
        {
            foreach (var item in assetList.Items)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return detailsByAssetKey;
                }

                if (!IsSpineLinkCandidate(item))
                {
                    continue;
                }

                var assetKey = SpineAssetGroupDetector.CreateAssetKey(item.BundleId, item.AssetId);
                if (detailsByAssetKey.ContainsKey(assetKey))
                {
                    continue;
                }

                if (workspaceSessionRegistry.TryGetAssetDetails(
                        workspaceId,
                        item.BundleId,
                        item.AssetId,
                        out var details) &&
                    details is not null)
                {
                    detailsByAssetKey[assetKey] = details;
                }
            }
        }

        return detailsByAssetKey;
    }

    private static bool IsSpineLinkCandidate(AssetListItemDto item) =>
        item.AssetType == AssetType.Material ||
        string.Equals(item.AssetTypeDisplayName, "MonoBehaviour", StringComparison.OrdinalIgnoreCase);
}
