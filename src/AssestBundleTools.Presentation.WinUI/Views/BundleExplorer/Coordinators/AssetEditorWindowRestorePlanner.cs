using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetEditorWindowRestorePlanner
{
    public static AssetEditorWindowRestorePlan CreatePlan(
        IReadOnlyList<WorkspaceAssetEditorWindowStateDto> pendingWindows,
        Func<WorkspaceAssetEditorWindowAssetTabDto, bool> canRestoreAssetTab,
        IReadOnlySet<string>? openAssetKeys = null)
    {
        ArgumentNullException.ThrowIfNull(pendingWindows);
        ArgumentNullException.ThrowIfNull(canRestoreAssetTab);

        var restorableWindows = new List<WorkspaceAssetEditorWindowStateDto>(pendingWindows.Count);
        var unavailableWindows = new List<WorkspaceAssetEditorWindowStateDto>();
        var conflictedWindows = new List<WorkspaceAssetEditorWindowStateDto>();
        var reservedAssetKeys = new HashSet<string>(
            openAssetKeys is null ? Array.Empty<string>() : openAssetKeys,
            StringComparer.Ordinal);

        foreach (var pendingWindow in pendingWindows)
        {
            var restorableTabs = new List<WorkspaceAssetEditorWindowAssetTabDto>(pendingWindow.OpenAssetTabs.Count);
            var unavailableTabs = new List<WorkspaceAssetEditorWindowAssetTabDto>();
            var conflictedTabs = new List<WorkspaceAssetEditorWindowAssetTabDto>();

            foreach (var assetTab in pendingWindow.OpenAssetTabs)
            {
                if (!canRestoreAssetTab(assetTab))
                {
                    unavailableTabs.Add(assetTab);
                    continue;
                }

                if (!AssetDocumentIdentityKey.TryCreate(assetTab.BundleId, assetTab.AssetId, out var assetKey) ||
                    !reservedAssetKeys.Add(assetKey))
                {
                    conflictedTabs.Add(assetTab);
                    continue;
                }

                restorableTabs.Add(assetTab);
            }

            if (restorableTabs.Count > 0)
            {
                restorableWindows.Add(CreateWindowSnapshotWithTabs(pendingWindow, restorableTabs));
            }

            if (unavailableTabs.Count > 0)
            {
                unavailableWindows.Add(CreateWindowSnapshotWithTabs(pendingWindow, unavailableTabs));
            }

            if (conflictedTabs.Count > 0)
            {
                conflictedWindows.Add(CreateWindowSnapshotWithTabs(pendingWindow, conflictedTabs));
            }
        }

        return new AssetEditorWindowRestorePlan(restorableWindows, unavailableWindows, conflictedWindows);
    }

    public static AssetEditorWindowLayoutSnapshot? CreateRestoredLayout(
        WorkspaceAssetEditorWindowLayoutDto layout)
    {
        var restoredLayout = new AssetEditorWindowLayoutSnapshot(
            layout.X,
            layout.Y,
            layout.Width,
            layout.Height);
        return AssetEditorWindowLayoutSafety.CanRestoreSize(restoredLayout)
            ? restoredLayout
            : null;
    }

    private static WorkspaceAssetEditorWindowStateDto CreateWindowSnapshotWithTabs(
        WorkspaceAssetEditorWindowStateDto source,
        IReadOnlyList<WorkspaceAssetEditorWindowAssetTabDto> assetTabs)
    {
        var activeTab = assetTabs.FirstOrDefault(tab =>
                            string.Equals(tab.BundleId, source.ActiveBundleId, StringComparison.Ordinal) &&
                            string.Equals(tab.AssetId, source.ActiveAssetId, StringComparison.Ordinal))
                        ?? assetTabs[0];

        return source with
        {
            OpenAssetTabs = assetTabs.ToArray(),
            ActiveBundleId = activeTab.BundleId,
            ActiveAssetId = activeTab.AssetId
        };
    }
}

internal sealed record AssetEditorWindowRestorePlan(
    IReadOnlyList<WorkspaceAssetEditorWindowStateDto> RestorableWindows,
    IReadOnlyList<WorkspaceAssetEditorWindowStateDto> PendingUnavailableWindows,
    IReadOnlyList<WorkspaceAssetEditorWindowStateDto> PendingConflictedWindows);
