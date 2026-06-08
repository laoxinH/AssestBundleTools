using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
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

    [Fact]
    public async Task RefreshPrimaryPaneAsync_ShouldKeepPrimaryPaneSynchronizationActiveUntilInspectorSync()
    {
        var context = CreateContext();
        context.PrimaryPane.ViewMode = AssetPaneViewMode.Assets;

        await context.Coordinator.RefreshPrimaryPaneAsync(
            context.CaptureActiveTabState,
            preserveCurrentDocumentTab: false);

        Assert.True(context.ObservedSynchronizationDuringInspectorSync.GetValueOrDefault());
        Assert.False(context.Coordinator.IsSynchronizingPrimaryPaneState);
    }

    [Fact]
    public async Task RefreshPrimaryPaneAsync_ShouldRunSpineGroupListQueryThroughBackgroundRunner()
    {
        var context = CreateContext();
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;

        await context.Coordinator.RefreshPrimaryPaneAsync(
            context.CaptureActiveTabState,
            preserveCurrentDocumentTab: true);

        Assert.Equal(1, context.BackgroundTaskRunner.RunCallCount);
        Assert.Equal(typeof(Result<SpineAssetGroupListResultDto>), context.BackgroundTaskRunner.LastResultType);
        Assert.Single(context.PrimaryPane.SpineGroupItems);
    }

    [Fact]
    public async Task RefreshPrimaryPaneAsync_ShouldClearStaleSpineGroups_WhileLoadingCurrentBundle()
    {
        var operationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var backgroundTaskRunner = new TestBackgroundTaskRunner
        {
            RunAsyncHandler = async (operation, token) =>
            {
                operationEntered.SetResult();
                await releaseOperation.Task.WaitAsync(token);
                return await operation(token);
            }
        };
        var context = CreateContext(backgroundTaskRunner: backgroundTaskRunner);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        SpineAssetGroupDto[] previousGroups =
        [
            CreateSpineGroup(new BundleId("bundle-previous"), "previous")
        ];
        context.PrimaryPane.ReplaceSpineGroups(
            "ui",
            previousGroups.Select(context.CreateSpineGroupEntry),
            "empty");

        var refreshTask = context.Coordinator.RefreshPrimaryPaneAsync(
            context.CaptureActiveTabState,
            preserveCurrentDocumentTab: true);
        await operationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(context.PrimaryPane.SpineGroupItems);
        Assert.Equal("ui", context.PrimaryPane.CurrentBundleDisplayName);
        Assert.Equal(ResourceKeys.BundleExplorerLoadingSpineGroups, context.PrimaryPane.EmptyStateText);

        releaseOperation.SetResult();
        await refreshTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RefreshPrimaryPaneAsync_ShouldIgnoreStaleSpineGroupResult_AfterBundleSwitch()
    {
        var firstOperationEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstOperation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runCallIndex = 0;
        var backgroundTaskRunner = new TestBackgroundTaskRunner
        {
            RunAsyncHandler = async (operation, token) =>
            {
                if (Interlocked.Increment(ref runCallIndex) == 1)
                {
                    firstOperationEntered.SetResult();
                    await releaseFirstOperation.Task.WaitAsync(token);
                }

                return await operation(token);
            }
        };
        var context = CreateContext(
            bundleNames: ["first", "second"],
            backgroundTaskRunner: backgroundTaskRunner);
        context.PrimaryPane.ViewMode = AssetPaneViewMode.SpineGroups;
        var firstBundleNode = context.FindBundleNode("first");
        var secondBundleNode = context.FindBundleNode("second");
        var firstBundleTab = context.BundleTab;
        var secondBundleTab = new BundleDocumentTabViewModel(
            secondBundleNode.BundleId,
            secondBundleNode.DisplayName,
            secondBundleNode.LogicalPath,
            isPreview: false);
        secondBundleTab.CaptureFromPane(context.PrimaryPane);

        var firstRefreshTask = context.Coordinator.LoadActiveBundleTabAsync(
            firstBundleTab,
            firstBundleNode,
            writeSelectionLog: false);
        await firstOperationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await context.Coordinator.LoadActiveBundleTabAsync(
            secondBundleTab,
            secondBundleNode,
            writeSelectionLog: false);

        Assert.Collection(
            context.PrimaryPane.SpineGroupItems,
            item => Assert.Equal("bundle-second-group", item.Item.GroupId));

        releaseFirstOperation.SetResult();
        await firstRefreshTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Collection(
            context.PrimaryPane.SpineGroupItems,
            item => Assert.Equal("bundle-second-group", item.Item.GroupId));
    }

    private static TestContext CreateContext(
        IReadOnlyList<string>? bundleNames = null,
        TestBackgroundTaskRunner? backgroundTaskRunner = null)
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
        var normalizedBundleNames = bundleNames is { Count: > 0 } ? bundleNames : ["ui"];
        bundlePane.RebuildBundleGroups(CreateBundleTree(normalizedBundleNames));
        var firstBundleName = normalizedBundleNames[0];
        var bundleNode = bundlePane.FindBundleNode(new BundleId($"bundle-{firstBundleName}"))!;
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
        bool? observedSynchronizationDuringInspectorSync = null;
        BundleExplorerAssetPaneCoordinator? coordinator = null;
        backgroundTaskRunner ??= new TestBackgroundTaskRunner();
        coordinator = new BundleExplorerAssetPaneCoordinator(
            CreateWorkspaceExplorerGateway(workspaceSummary.WorkspaceId),
            new TestAppLogService(),
            localizationService,
            workspaceShell,
            bundlePane,
            new BundleExplorerAssetInspectorPresenter(bundlePane, localizationService),
            primaryPane,
            () => bundleTab,
            result => result.IsSuccess ? result.Value : null,
            result => result.IsSuccess ? result.Value : null,
            backgroundTaskRunner,
            _ =>
            {
                synchronizeInspectorCallCount++;
                observedSynchronizationDuringInspectorSync = coordinator?.IsSynchronizingPrimaryPaneState;
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
            backgroundTaskRunner,
            () => synchronizeInspectorCallCount,
            () => observedSynchronizationDuringInspectorSync);
    }

    private static TestWorkspaceExplorerGateway CreateWorkspaceExplorerGateway(WorkspaceId workspaceId)
    {
        return new TestWorkspaceExplorerGateway
        {
            OpenWorkspaceHandler = _ => throw new NotSupportedException(),
            LoadBundleTreeHandler = _ => throw new NotSupportedException(),
            LoadAssetListHandler = query => Task.FromResult(Result<AssetListResultDto>.Success(
                new AssetListResultDto(
                    workspaceId,
                    query.BundleId,
                    query.BundleId.Value,
                    query.SearchKeyword,
                    query.AssetTypeFilter,
                    TotalCount: 1,
                    FilteredCount: 1,
                    Items:
                    [
                        new AssetListItemDto(
                            new AssetId("asset-button"),
                            query.BundleId,
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
                    query.BundleId,
                    query.BundleId.Value,
                    query.SearchKeyword,
                    TotalCount: 1,
                    FilteredCount: 1,
                    Items:
                    [
                        CreateSpineGroup(query.BundleId, $"{query.BundleId.Value}-group")
                    ])))
        };
    }

    private static IReadOnlyList<BundleTreeNodeDto> CreateBundleTree(IReadOnlyList<string> bundleNames)
    {
        return
        [
            new BundleTreeNodeDto(
                BundleId: new BundleId("group-root"),
                DisplayName: "Root",
                LogicalPath: "Root",
                SourcePath: string.Empty,
                IsVirtualFolder: true,
                AssetCount: 0,
                Children: bundleNames
                    .Select(name => new BundleTreeNodeDto(
                        BundleId: new BundleId($"bundle-{name}"),
                        DisplayName: name,
                        LogicalPath: $"{name}.ab",
                        SourcePath: $@"C:\Bundles\{name}.ab",
                        IsVirtualFolder: false,
                        AssetCount: 2,
                        Children: Array.Empty<BundleTreeNodeDto>()))
                    .ToArray())
        ];
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
        TestBackgroundTaskRunner BackgroundTaskRunner,
        Func<int> GetSynchronizeInspectorCallCount,
        Func<bool?> GetObservedSynchronizationDuringInspectorSync)
    {
        public int SynchronizeInspectorCallCount => GetSynchronizeInspectorCallCount();

        public bool? ObservedSynchronizationDuringInspectorSync => GetObservedSynchronizationDuringInspectorSync();

        public BundleNodeViewModel FindBundleNode(string displayName)
        {
            return new BundleNodeViewModel(
                new BundleId($"bundle-{displayName}"),
                displayName,
                $"{displayName}.ab",
                $@"C:\Bundles\{displayName}.ab",
                assetCount: 2);
        }

        public void CaptureActiveTabState()
        {
            BundleTab.CaptureFromPane(PrimaryPane);
        }

        public SpineAssetGroupEntryViewModel CreateSpineGroupEntry(SpineAssetGroupDto group) =>
            new(
                group,
                "Skeleton 1 / Atlas 0",
                "0 pages",
                "Ready");
    }
}
