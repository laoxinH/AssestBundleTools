using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.State;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal sealed class WorkspaceOperationPendingProjectionCoordinator : IDisposable
{
    private readonly WorkspaceOperationPendingTracker _pendingTracker;
    private readonly Func<IEnumerable<AssetDocumentTabViewModel>> _assetTabsProvider;
    private readonly Func<IEnumerable<SpineAssetGroupDocumentTabViewModel>> _spineGroupTabsProvider;
    private readonly Func<WorkspaceId?> _workspaceIdProvider;

    public WorkspaceOperationPendingProjectionCoordinator(
        WorkspaceOperationPendingTracker pendingTracker,
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        Func<WorkspaceId?> workspaceIdProvider)
        : this(
            pendingTracker,
            () => documentWorkspace.OpenBundleTabs.SelectMany(static tab => tab.AssetTabs),
            () => documentWorkspace.OpenBundleTabs.SelectMany(static tab => tab.SpineGroupTabs),
            workspaceIdProvider)
    {
        ArgumentNullException.ThrowIfNull(documentWorkspace);
    }

    public WorkspaceOperationPendingProjectionCoordinator(
        WorkspaceOperationPendingTracker pendingTracker,
        Func<IEnumerable<AssetDocumentTabViewModel>> assetTabsProvider,
        Func<IEnumerable<SpineAssetGroupDocumentTabViewModel>> spineGroupTabsProvider,
        Func<WorkspaceId?> workspaceIdProvider)
    {
        _pendingTracker = pendingTracker ?? throw new ArgumentNullException(nameof(pendingTracker));
        _assetTabsProvider = assetTabsProvider ?? throw new ArgumentNullException(nameof(assetTabsProvider));
        _spineGroupTabsProvider = spineGroupTabsProvider ?? throw new ArgumentNullException(nameof(spineGroupTabsProvider));
        _workspaceIdProvider = workspaceIdProvider ?? throw new ArgumentNullException(nameof(workspaceIdProvider));
        _pendingTracker.PendingStateChanged += OnPendingStateChanged;
    }

    public void Apply()
    {
        var workspaceId = _workspaceIdProvider();
        if (!workspaceId.HasValue)
        {
            return;
        }

        foreach (var assetTab in _assetTabsProvider())
        {
            Apply(assetTab.Inspector, workspaceId.Value);
        }

        foreach (var spineGroupTab in _spineGroupTabsProvider())
        {
            Apply(spineGroupTab.Inspector, workspaceId.Value);
        }
    }

    public void Dispose()
    {
        _pendingTracker.PendingStateChanged -= OnPendingStateChanged;
    }

    private void OnPendingStateChanged(object? sender, EventArgs e)
    {
        Apply();
    }

    private void Apply(InspectorPanelViewModel inspector, WorkspaceId workspaceId)
    {
        foreach (var record in inspector.ChangeRecords)
        {
            record.SetPending(_pendingTracker.IsPending(workspaceId, record.OperationId));
        }
    }
}
