using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;

namespace AssestBundleTools.Application.Assets.Queries;

internal static class SpineAssetGroupPreviewQueryFactory
{
    public static GetAssetPreviewQuery Create(
        LoadSpineAssetGroupDetailsQuery query,
        SpineAssetGroupPartDto part) =>
        Create(query, part.BundleId, part.AssetId);

    public static GetAssetPreviewQuery Create(
        LoadSpineAssetGroupDetailsQuery query,
        BundleId bundleId,
        AssetId assetId) =>
        new(
            query.WorkspaceId,
            query.WorkspaceKey,
            query.WorkspaceDataRootPath,
            bundleId,
            assetId,
            ResolveOverride(query, bundleId, assetId));

    private static AssetPreviewSourceOverrideDto? ResolveOverride(
        LoadSpineAssetGroupDetailsQuery query,
        BundleId bundleId,
        AssetId assetId) =>
        query.PreviewSourceOverrides?
            .FirstOrDefault(candidate =>
                candidate.BundleId.Equals(bundleId) &&
                candidate.AssetId.Equals(assetId) &&
                !string.IsNullOrWhiteSpace(candidate.SourceFilePath));
}
