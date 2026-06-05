using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 操作记录变化后刷新主工作台已打开详情标签的读模型。
/// </summary>
internal sealed class BundleExplorerOperationReadModelRefreshHandler
{
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly BundleExplorerAssetDocumentTabCoordinator _assetDocumentTabCoordinator;
    private readonly BundleExplorerSpineGroupDocumentCoordinator _spineGroupDocumentCoordinator;

    public BundleExplorerOperationReadModelRefreshHandler(
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        BundleExplorerAssetDocumentTabCoordinator assetDocumentTabCoordinator,
        BundleExplorerSpineGroupDocumentCoordinator spineGroupDocumentCoordinator)
    {
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _assetDocumentTabCoordinator = assetDocumentTabCoordinator ?? throw new ArgumentNullException(nameof(assetDocumentTabCoordinator));
        _spineGroupDocumentCoordinator = spineGroupDocumentCoordinator ?? throw new ArgumentNullException(nameof(spineGroupDocumentCoordinator));
    }

    public async Task RefreshAsync(WorkspaceOperationChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (!eventArgs.IsWorkspaceWide)
        {
            await RefreshAffectedTargetsAsync(eventArgs.AffectedAssets);
            return;
        }

        var assetTabs = _documentWorkspace.OpenBundleTabs
            .SelectMany(static bundleTab => bundleTab.AssetTabs)
            .Distinct()
            .ToArray();
        foreach (var assetTab in assetTabs)
        {
            await _assetDocumentTabCoordinator.RefreshAssetDocumentTabDetailsAsync(assetTab);
        }

        var spineGroupTabs = _documentWorkspace.OpenBundleTabs
            .SelectMany(static bundleTab => bundleTab.SpineGroupTabs)
            .Distinct()
            .ToArray();
        foreach (var spineGroupTab in spineGroupTabs)
        {
            _spineGroupDocumentCoordinator.QueueSpineGroupDocumentTabRefresh(spineGroupTab);
        }
    }

    private async Task RefreshAffectedTargetsAsync(
        IReadOnlyList<WorkspaceOperationAffectedAsset> affectedAssets)
    {
        foreach (var affectedAsset in affectedAssets)
        {
            var matchedAssetTabs = _documentWorkspace.OpenBundleTabs
                .SelectMany(static bundleTab => bundleTab.AssetTabs)
                .Where(assetTab => MatchesAsset(assetTab, affectedAsset))
                .Distinct()
                .ToArray();

            if (matchedAssetTabs.Length == 0)
            {
                await _assetDocumentTabCoordinator.RefreshAssetListEntryDetailsAsync(
                    affectedAsset.BundleId,
                    affectedAsset.AssetId);
            }
            else
            {
                foreach (var assetTab in matchedAssetTabs)
                {
                    await _assetDocumentTabCoordinator.RefreshAssetDocumentTabDetailsAsync(assetTab);
                }
            }

            var matchedSpineGroupTabs = _documentWorkspace.OpenBundleTabs
                .SelectMany(static bundleTab => bundleTab.SpineGroupTabs)
                .Where(spineGroupTab => SpineGroupContainsAsset(spineGroupTab, affectedAsset))
                .Distinct()
                .ToArray();
            foreach (var spineGroupTab in matchedSpineGroupTabs)
            {
                _spineGroupDocumentCoordinator.QueueSpineGroupDocumentTabRefresh(spineGroupTab);
            }
        }
    }

    private static bool MatchesAsset(
        AssetDocumentTabViewModel assetTab,
        WorkspaceOperationAffectedAsset affectedAsset) =>
        assetTab.BundleId.Equals(affectedAsset.BundleId) &&
        assetTab.AssetId.Equals(affectedAsset.AssetId);

    private static bool SpineGroupContainsAsset(
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        WorkspaceOperationAffectedAsset affectedAsset) =>
        spineGroupTab.Item.SkeletonAssets
            .Concat(spineGroupTab.Item.AtlasAssets)
            .Concat(spineGroupTab.Item.TexturePageAssets)
            .Any(part =>
                part.BundleId.Equals(affectedAsset.BundleId) &&
                part.AssetId.Equals(affectedAsset.AssetId));
}
