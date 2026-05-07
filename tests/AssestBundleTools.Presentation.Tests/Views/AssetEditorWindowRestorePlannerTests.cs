using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetEditorWindowRestorePlannerTests
{
    [Fact]
    public void CreatePlan_ShouldSplitRestorableAndUnavailableTabs()
    {
        var plan = AssetEditorWindowRestorePlanner.CreatePlan(
        [
            CreateWindow(
                windowStateId: 7,
                activeBundleId: "bundle-a",
                activeAssetId: "asset-1",
                CreateAssetTab("bundle-a", "asset-1"),
                CreateAssetTab("missing-bundle", "asset-2"),
                CreateAssetTab("bundle-a", "asset-3"))
        ],
        tab => tab.BundleId == "bundle-a");

        var restorableWindow = Assert.Single(plan.RestorableWindows);
        var unavailableWindow = Assert.Single(plan.PendingUnavailableWindows);

        Assert.Equal(["asset-1", "asset-3"], restorableWindow.OpenAssetTabs.Select(static tab => tab.AssetId));
        Assert.Equal("bundle-a", restorableWindow.ActiveBundleId);
        Assert.Equal("asset-1", restorableWindow.ActiveAssetId);
        Assert.Equal("missing-bundle", unavailableWindow.ActiveBundleId);
        Assert.Equal("asset-2", unavailableWindow.ActiveAssetId);
        Assert.Equal("asset-2", Assert.Single(unavailableWindow.OpenAssetTabs).AssetId);
    }

    [Fact]
    public void CreatePlan_ShouldMoveFullyUnavailableWindowToPending()
    {
        var plan = AssetEditorWindowRestorePlanner.CreatePlan(
        [
            CreateWindow(
                windowStateId: 9,
                activeBundleId: "missing-bundle",
                activeAssetId: "asset-1",
                CreateAssetTab("missing-bundle", "asset-1"))
        ],
        static _ => false);

        Assert.Empty(plan.RestorableWindows);
        var pendingWindow = Assert.Single(plan.PendingUnavailableWindows);
        Assert.Equal(9, pendingWindow.WindowStateId);
        Assert.Equal("asset-1", Assert.Single(pendingWindow.OpenAssetTabs).AssetId);
    }

    [Fact]
    public void CreatePlan_ShouldKeepAlreadyOpenAssetsPendingAsConflicts()
    {
        var plan = AssetEditorWindowRestorePlanner.CreatePlan(
        [
            CreateWindow(
                windowStateId: 11,
                activeBundleId: "bundle-a",
                activeAssetId: "asset-open",
                CreateAssetTab("bundle-a", "asset-open"),
                CreateAssetTab("bundle-a", "asset-new"),
                CreateAssetTab("bundle-a", "asset-new"))
        ],
        static _ => true,
        new HashSet<string>(StringComparer.Ordinal)
        {
            AssetDocumentIdentityKey.Create("bundle-a", "asset-open")
        });

        var restorableWindow = Assert.Single(plan.RestorableWindows);
        var conflictedWindow = Assert.Single(plan.PendingConflictedWindows);

        Assert.Equal("asset-new", Assert.Single(restorableWindow.OpenAssetTabs).AssetId);
        Assert.Equal("asset-new", restorableWindow.ActiveAssetId);
        Assert.Equal(["asset-open", "asset-new"], conflictedWindow.OpenAssetTabs.Select(static tab => tab.AssetId));
        Assert.Equal("asset-open", conflictedWindow.ActiveAssetId);
        Assert.Empty(plan.PendingUnavailableWindows);
    }

    [Fact]
    public void CreateRestoredLayout_ShouldRejectTooSmallLayout()
    {
        var layout = AssetEditorWindowRestorePlanner.CreateRestoredLayout(
            new WorkspaceAssetEditorWindowLayoutDto(12, 24, 320, 240));

        Assert.Null(layout);
    }

    private static WorkspaceAssetEditorWindowStateDto CreateWindow(
        int windowStateId,
        string activeBundleId,
        string activeAssetId,
        params WorkspaceAssetEditorWindowAssetTabDto[] assetTabs)
    {
        return new WorkspaceAssetEditorWindowStateDto(
            WindowStateId: windowStateId,
            Layout: new WorkspaceAssetEditorWindowLayoutDto(120, 90, 1320, 820),
            OpenAssetTabs: assetTabs,
            ActiveBundleId: activeBundleId,
            ActiveAssetId: activeAssetId);
    }

    private static WorkspaceAssetEditorWindowAssetTabDto CreateAssetTab(
        string bundleId,
        string assetId) =>
        new(bundleId, assetId, assetId);
}
