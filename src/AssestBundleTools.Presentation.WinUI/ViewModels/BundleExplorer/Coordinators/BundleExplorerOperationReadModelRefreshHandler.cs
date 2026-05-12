using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

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

    public async Task RefreshAsync()
    {
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
            await _spineGroupDocumentCoordinator.RefreshSpineGroupDocumentTabAsync(spineGroupTab);
        }
    }
}
