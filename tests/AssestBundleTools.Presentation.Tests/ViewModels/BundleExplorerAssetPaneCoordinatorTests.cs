using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetPaneCoordinatorTests
{
    [Fact]
    public async Task RefreshPrimaryPaneAsync_ShouldPreserveActiveSpineDocument_WhenSwitchingBackToAssets()
    {
        var context = CreateContext();
        var assetTab = CreateAssetDocumentTab(context.BundleTab.BundleId, "asset-button", context.LocalizationService);
        var spineGroupTab = CreateSpineGroupDocumentTab(context.BundleTab.BundleId, "hero", context.LocalizationService);
        context.BundleTab.AddAssetTab(assetTab, activate: false);
        context.BundleTab.AddSpineGroupTab(spineGroupTab, activate: true);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.Assets;

        await context.Coordinator.RefreshPrimaryPaneAsync(
            context.CaptureActiveTabState,
            preserveCurrentDocumentTab: true);

        Assert.Same(spineGroupTab, context.BundleTab.ActiveSpineGroupTab);
        Assert.Same(spineGroupTab, context.BundleTab.ActiveDocumentTab?.SpineGroupTab);
        Assert.Equal(0, context.SynchronizeInspectorCallCount);
    }

    [Fact]
    public async Task RefreshPrimaryPaneAsync_ShouldPreserveActiveAssetDocument_WhenSwitchingToSpineGroups()
    {
        var context = CreateContext();
        var assetTab = CreateAssetDocumentTab(context.BundleTab.BundleId, "asset-button", context.LocalizationService);
        var spineGroupTab = CreateSpineGroupDocumentTab(context.BundleTab.BundleId, "hero", context.LocalizationService);
        context.BundleTab.AddAssetTab(assetTab, activate: true);
        context.BundleTab.AddSpineGroupTab(spineGroupTab, activate: false);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;

        await context.Coordinator.RefreshPrimaryPaneAsync(
            context.CaptureActiveTabState,
            preserveCurrentDocumentTab: true);

        Assert.Same(assetTab, context.BundleTab.ActiveAssetTab);
        Assert.Same(assetTab, context.BundleTab.ActiveDocumentTab?.AssetTab);
        Assert.Equal(0, context.SynchronizeInspectorCallCount);
    }

    private static TestContext CreateContext()
    {
        var localizationService = new TestLocalizationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("AssetPane");
        var workspaceSummary = PresentationTestDataFactory.CreateWorkspaceSummary(workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);

        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);

        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        bundlePane.RebuildBundleGroups(PresentationTestDataFactory.CreateBundleTree("ui"));
        var bundleNode = bundlePane.FindBundleNode(new BundleId("bundle-ui"))!;
        var primaryPane = new AssetPaneViewModel();
        primaryPane.ConfigurePane(
            PaneId.New(),
            ResourceKeys.BundleExplorerPrimaryPaneTitle,
            ResourceKeys.BundleExplorerNoAssets);
        primaryPane.ReplaceItems(
            bundleNode.DisplayName,
            [CreateAssetListEntry(bundleNode.BundleId, new AssetId("asset-button"))],
            ResourceKeys.BundleExplorerNoAssets);

        var bundleTab = new BundleDocumentTabViewModel(
            bundleNode.BundleId,
            bundleNode.DisplayName,
            bundleNode.LogicalPath,
            isPreview: false);
        var documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        documentWorkspace.BundleTabs.Add(bundleTab);
        documentWorkspace.SetActiveBundleTab(bundleTab);

        var synchronizeInspectorCallCount = 0;
        var coordinator = new BundleExplorerAssetPaneCoordinator(
            CreateWorkspaceExplorerGateway(workspaceSummary.WorkspaceId, bundleNode.BundleId, bundleNode.DisplayName),
            new TestAppLogService(),
            localizationService,
            workspaceShell,
            bundlePane,
            new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService),
            primaryPane,
            () => bundleTab,
            result => result.IsSuccess ? result.Value : null,
            result => result.IsSuccess ? result.Value : null,
            _ =>
            {
                synchronizeInspectorCallCount++;
                return Task.CompletedTask;
            },
            static _ => { },
            static () => { },
            static () => { });

        return new TestContext(
            localizationService,
            primaryPane,
            bundleTab,
            coordinator,
            () => synchronizeInspectorCallCount);
    }

    private static TestWorkspaceExplorerGateway CreateWorkspaceExplorerGateway(
        WorkspaceId workspaceId,
        BundleId bundleId,
        string bundleDisplayName)
    {
        return new TestWorkspaceExplorerGateway
        {
            OpenWorkspaceHandler = _ => throw new NotSupportedException(),
            LoadBundleTreeHandler = _ => throw new NotSupportedException(),
            LoadAssetListHandler = query => Task.FromResult(Result<AssetListResultDto>.Success(
                new AssetListResultDto(
                    workspaceId,
                    bundleId,
                    bundleDisplayName,
                    query.SearchKeyword,
                    query.AssetTypeFilter,
                    TotalCount: 1,
                    FilteredCount: 1,
                    Items:
                    [
                        new AssetListItemDto(
                            new AssetId("asset-button"),
                            bundleId,
                            "Button",
                            "Assets/Button.png",
                            AssetType.Texture2D,
                            SizeInBytes: 128,
                            PreferredDisplaySizeInBytes: null,
                            HasPreview: true,
                            IsModified: false)
                    ],
                    AssetTypeDisplayNameFilter: query.AssetTypeDisplayNameFilter))),
            LoadSpineAssetGroupsHandler = query => Task.FromResult(Result<SpineAssetGroupListResultDto>.Success(
                new SpineAssetGroupListResultDto(
                    workspaceId,
                    bundleId,
                    bundleDisplayName,
                    query.SearchKeyword,
                    TotalCount: 1,
                    FilteredCount: 1,
                    Items:
                    [
                        CreateSpineGroup(bundleId, "hero")
                    ])))
        };
    }

    private static AssetListEntryViewModel CreateAssetListEntry(BundleId bundleId, AssetId assetId)
    {
        return new AssetListEntryViewModel(
            new AssetListItemDto(
                assetId,
                bundleId,
                "Button",
                "Assets/Button.png",
                AssetType.Texture2D,
                SizeInBytes: 128,
                PreferredDisplaySizeInBytes: null,
                HasPreview: true,
                IsModified: false),
            "Texture2D",
            "128 B",
            "Original");
    }

    private static AssetDocumentTabViewModel CreateAssetDocumentTab(
        BundleId bundleId,
        string assetId,
        TestLocalizationService localizationService)
    {
        return new AssetDocumentTabViewModel(
            bundleId,
            new AssetId(assetId),
            assetId,
            InspectorInformationTextSetFactory.Create(localizationService));
    }

    private static SpineAssetGroupDocumentTabViewModel CreateSpineGroupDocumentTab(
        BundleId bundleId,
        string groupId,
        TestLocalizationService localizationService)
    {
        return new SpineAssetGroupDocumentTabViewModel(
            CreateSpineGroup(bundleId, groupId),
            InspectorInformationTextSetFactory.Create(localizationService));
    }

    private static SpineAssetGroupDto CreateSpineGroup(BundleId bundleId, string groupId)
    {
        return new SpineAssetGroupDto(
            groupId,
            bundleId,
            groupId,
            groupId,
            SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
            AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
            TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>());
    }

    private sealed record TestContext(
        TestLocalizationService LocalizationService,
        AssetPaneViewModel PrimaryPane,
        BundleDocumentTabViewModel BundleTab,
        BundleExplorerAssetPaneCoordinator Coordinator,
        Func<int> GetSynchronizeInspectorCallCount)
    {
        public int SynchronizeInspectorCallCount => GetSynchronizeInspectorCallCount();

        public void CaptureActiveTabState()
        {
            BundleTab.CaptureFromPane(PrimaryPane);
        }
    }
}
