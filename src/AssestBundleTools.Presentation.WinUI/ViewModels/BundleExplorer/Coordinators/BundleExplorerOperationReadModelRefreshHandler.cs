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
    private readonly AssociatedAssetOperationProjectionRefreshCoordinator _operationProjectionRefreshCoordinator;

    public BundleExplorerOperationReadModelRefreshHandler(
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        BundleExplorerAssetDocumentTabCoordinator assetDocumentTabCoordinator,
        BundleExplorerSpineGroupDocumentCoordinator spineGroupDocumentCoordinator,
        AssociatedAssetOperationProjectionRefreshCoordinator operationProjectionRefreshCoordinator)
    {
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _assetDocumentTabCoordinator = assetDocumentTabCoordinator ?? throw new ArgumentNullException(nameof(assetDocumentTabCoordinator));
        _spineGroupDocumentCoordinator = spineGroupDocumentCoordinator ?? throw new ArgumentNullException(nameof(spineGroupDocumentCoordinator));
        _operationProjectionRefreshCoordinator = operationProjectionRefreshCoordinator ?? throw new ArgumentNullException(nameof(operationProjectionRefreshCoordinator));
    }

    public async Task RefreshAsync(WorkspaceOperationChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var shouldRefreshPreview = ShouldRefreshPreview(eventArgs);

        if (!eventArgs.IsWorkspaceWide)
        {
            await RefreshAffectedTargetsAsync(eventArgs.AffectedAssets, shouldRefreshPreview);
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
            await _operationProjectionRefreshCoordinator.RefreshSpineGroupOperationProjectionAsync(spineGroupTab);
            if (shouldRefreshPreview)
            {
                _spineGroupDocumentCoordinator.QueueSpineGroupDocumentPreviewRefresh(spineGroupTab);
            }
        }
    }

    private async Task RefreshAffectedTargetsAsync(
        IReadOnlyList<WorkspaceOperationAffectedAsset> affectedAssets,
        bool shouldRefreshPreview)
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
                    await _operationProjectionRefreshCoordinator.RefreshAssetDocumentOperationProjectionAsync(assetTab);
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
                await _operationProjectionRefreshCoordinator.RefreshSpineGroupOperationProjectionAsync(
                    spineGroupTab,
                    [affectedAsset]);
                if (shouldRefreshPreview)
                {
                    _spineGroupDocumentCoordinator.QueueSpineGroupDocumentPreviewRefresh(spineGroupTab);
                }
            }
        }
    }

    private static bool ShouldRefreshPreview(WorkspaceOperationChangedEventArgs eventArgs)
    {
        var contentChangingKinds =
            WorkspaceOperationChangeKind.Replacement |
            WorkspaceOperationChangeKind.Edit;
        return (eventArgs.ChangeKind & contentChangingKinds) != WorkspaceOperationChangeKind.None ||
               eventArgs.Reason is WorkspaceOperationChangeReason.ReplacementApplied or
                   WorkspaceOperationChangeReason.ReplacementUndone or
                   WorkspaceOperationChangeReason.ReplacementRedone or
                   WorkspaceOperationChangeReason.EditApplied;
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
