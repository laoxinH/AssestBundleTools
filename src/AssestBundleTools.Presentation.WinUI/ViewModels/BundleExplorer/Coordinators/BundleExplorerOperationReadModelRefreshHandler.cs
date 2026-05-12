using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 操作记录变化后刷新主工作台当前可见读模型。
/// </summary>
internal sealed class BundleExplorerOperationReadModelRefreshHandler
{
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly BundleExplorerAssetDocumentTabCoordinator _assetDocumentTabCoordinator;
    private readonly BundleExplorerSpineGroupDocumentCoordinator _spineGroupDocumentCoordinator;
    private readonly Func<Task> _refreshPrimaryPaneAsync;

    public BundleExplorerOperationReadModelRefreshHandler(
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        BundleExplorerAssetDocumentTabCoordinator assetDocumentTabCoordinator,
        BundleExplorerSpineGroupDocumentCoordinator spineGroupDocumentCoordinator,
        Func<Task> refreshPrimaryPaneAsync)
    {
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _assetDocumentTabCoordinator = assetDocumentTabCoordinator ?? throw new ArgumentNullException(nameof(assetDocumentTabCoordinator));
        _spineGroupDocumentCoordinator = spineGroupDocumentCoordinator ?? throw new ArgumentNullException(nameof(spineGroupDocumentCoordinator));
        _refreshPrimaryPaneAsync = refreshPrimaryPaneAsync ?? throw new ArgumentNullException(nameof(refreshPrimaryPaneAsync));
    }

    public async Task RefreshAsync()
    {
        await _refreshPrimaryPaneAsync();

        if (_documentWorkspace.ActiveAssetDocumentTab is { } assetTab)
        {
            await _assetDocumentTabCoordinator.RefreshAssetDocumentTabDetailsAsync(assetTab);
            return;
        }

        if (_documentWorkspace.ActiveSpineGroupDocumentTab is { } spineGroupTab)
        {
            await _spineGroupDocumentCoordinator.RefreshSpineGroupDocumentTabAsync(spineGroupTab);
        }
    }
}
