using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerBrowserStateCoordinatorTests
{
    [Fact]
    public void PersistWorkspaceBrowserStateNow_ShouldIncludeAssetPreviewImageDisplayOptions()
    {
        var context = CreateContext();
        var assetTab = CreateAssetTab(
            new BundleId("bundle-ui"),
            new AssetId("asset-button"),
            "Button",
            context.LocalizationService);
        assetTab.Inspector.Preview.Image.DisplayOptions.UseSolidBackground();
        assetTab.Inspector.Preview.Image.DisplayOptions.UseSpriteCropResult();
        context.BundleTab.AddAssetTab(assetTab, activate: true);

        context.Coordinator.PersistWorkspaceBrowserStateNow();

        Assert.NotNull(context.WorkspaceContextService.CurrentBrowserState);
        var browserState = context.WorkspaceContextService.CurrentBrowserState!;
        var assetTabState = Assert.Single(Assert.Single(browserState.OpenBundleTabs).OpenAssetTabs!);
        Assert.Equal("Solid", assetTabState.PreviewImageBackgroundMode);
        Assert.Equal("CropResult", assetTabState.SpritePreviewDisplayMode);
    }

    [Fact]
    public async Task TryRestoreWorkspaceBrowserStateAsync_ShouldRestoreAssetPreviewImageDisplayOptions()
    {
        var context = CreateContext();
        context.WorkspaceContextService.CurrentBrowserState = new WorkspaceBrowserStateSnapshotDto(
            BundleSearchKeyword: string.Empty,
            AssetSearchKeyword: string.Empty,
            AssetTypeFilter: null,
            ShowModifiedOnly: false,
            ShowPreviewableOnly: false,
            OpenBundleTabs:
            [
                new WorkspaceMetadataBundleTabDto(
                    BundleId: "bundle-ui",
                    LogicalPath: "ui.ab",
                    IsPreview: false,
                    SelectedAssetId: "asset-button",
                    SearchKeyword: string.Empty,
                    AssetTypeFilter: null,
                    ShowModifiedOnly: false,
                    ShowPreviewableOnly: false,
                    OpenAssetTabs:
                    [
                        new WorkspaceMetadataAssetTabDto(
                            AssetId: "asset-button",
                            AssetName: "Button",
                            SelectedInspectorSection: "details",
                            PreviewImageBackgroundMode: "Solid",
                            SpritePreviewDisplayMode: "CropResult")
                    ],
                    ActiveAssetTabId: "asset-button")
            ],
            ActiveBundleTabId: "bundle-ui",
            LastBundleId: "bundle-ui",
            LastAssetId: "asset-button");

        var restored = await context.Coordinator.TryRestoreWorkspaceBrowserStateAsync();

        Assert.True(restored);
        var restoredAssetTab = Assert.Single(context.DocumentWorkspace.ActiveBundleTab!.AssetTabs);
        Assert.True(restoredAssetTab.Inspector.Preview.Image.DisplayOptions.IsSolidBackgroundSelected);
        Assert.True(restoredAssetTab.Inspector.Preview.Image.DisplayOptions.IsSpriteCropResultSelected);
    }

    private static BrowserStateCoordinatorContext CreateContext()
    {
        var localizationService = new TestLocalizationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        bundlePane.RebuildBundleGroups(PresentationTestDataFactory.CreateBundleTree("ui"));
        var documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        var bundleTab = new BundleDocumentTabViewModel(
            new BundleId("bundle-ui"),
            "ui.ab",
            "ui.ab",
            isPreview: false);
        documentWorkspace.BundleTabs.Add(bundleTab);
        documentWorkspace.SetActiveBundleTab(bundleTab);

        var coordinator = new BundleExplorerBrowserStateCoordinator(
            workspaceContextService,
            bundlePane,
            documentWorkspace,
            hasWorkspaceProvider: static () => true,
            captureActiveTabStateWithoutPersistence: static () => { },
            createAssetDocumentTab: (bundleId, assetId, assetName) =>
                CreateAssetTab(bundleId, assetId, assetName, localizationService),
            activateBundleTabAsync: tab =>
            {
                documentWorkspace.SetActiveBundleTab(tab);
                return Task.CompletedTask;
            });

        return new BrowserStateCoordinatorContext(
            localizationService,
            workspaceContextService,
            documentWorkspace,
            bundleTab,
            coordinator);
    }

    private static AssetDocumentTabViewModel CreateAssetTab(
        BundleId bundleId,
        AssetId assetId,
        string assetName,
        TestLocalizationService localizationService)
    {
        return new AssetDocumentTabViewModel(
            bundleId,
            assetId,
            assetName,
            InspectorInformationTextSetFactory.Create(localizationService));
    }

    private sealed record BrowserStateCoordinatorContext(
        TestLocalizationService LocalizationService,
        TestWorkspaceContextService WorkspaceContextService,
        BundleExplorerDocumentWorkspaceViewModel DocumentWorkspace,
        BundleDocumentTabViewModel BundleTab,
        BundleExplorerBrowserStateCoordinator Coordinator);
}
