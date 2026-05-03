using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetPreviewCoordinatorTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 3)]
    [InlineData(8, 4)]
    [InlineData(16, 4)]
    public void ResolvePreviewWorkerCount_ShouldUseHalfCpuWithBoundedRange(
        int processorCount,
        int expectedWorkerCount)
    {
        Assert.Equal(
            expectedWorkerCount,
            AssetPreviewExecutionOptionsResolver.ResolvePreviewWorkerCount(processorCount));
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldUseBackgroundRunner_ForApplicationPreviewQuery()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-button");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("PreviewBackground");
        var workspaceSummary = new WorkspaceSummaryDto(
            workspaceId,
            workspaceRequest.WorkspaceDisplayName,
            workspaceRequest.SourcePath,
            WorkspaceMode.SinglePane,
            PaneId.New(),
            BundleCount: 1,
            AssetCount: 1,
            OpenedAtUtc: DateTimeOffset.UtcNow);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);
        var backgroundTaskRunner = new TestBackgroundTaskRunner();
        var previewQueryCount = 0;
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                previewQueryCount++;
                Assert.Equal(workspaceId, query.WorkspaceId);
                Assert.Equal(bundleId, query.BundleId);
                Assert.Equal(assetId, query.AssetId);
                return Task.FromResult(Result<AssetPreviewDto>.Success(new AssetPreviewDto(
                    WorkspaceId: query.WorkspaceId,
                    BundleId: query.BundleId,
                    AssetId: query.AssetId,
                    Kind: AssetPreviewKind.Summary,
                    ContentType: "text/plain",
                    TextContent: "summary",
                    BinaryContent: null,
                    PreviewPayloadFilePath: null,
                    PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
                    PreviewMessage: new AssetPreviewMessageDto(AssetPreviewMessageKey.MetadataOnly),
                    PreferredDisplaySizeInBytes: null,
                    IsFromCache: false,
                    GeneratedAtUtc: DateTimeOffset.UtcNow,
                    Diagnostic: new AssetPreviewDiagnosticDto(
                        AssetPreviewCacheStatusKind.Missing,
                        AssetPreviewFallbackReasonKind.None))));
            }
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            backgroundTaskRunner);
        var assetTab = new AssetDocumentTabViewModel(
            bundleId,
            assetId,
            "Button",
            InspectorInformationTextSetFactory.Create(localizationService));
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.Equal(1, previewQueryCount);
        Assert.Equal(1, backgroundTaskRunner.RunCallCount);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldShowUnavailable_WhenBackgroundRunnerThrows()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-button");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("PreviewRunnerThrows");
        var workspaceSummary = CreateWorkspaceSummary(workspaceId, workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);
        var backgroundTaskRunner = new TestBackgroundTaskRunner
        {
            RunAsyncHandler = static (_, _) => throw new InvalidOperationException("runner failed")
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            new TestWorkspaceAssetInspectionGateway(),
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            backgroundTaskRunner);
        var assetTab = CreateAssetTab(bundleId, assetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.False(assetTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(ResourceKeys.InspectorPreviewUnavailableText, assetTab.Inspector.Preview.PreviewStatusText);
        Assert.Contains("runner failed", assetTab.Inspector.Preview.PreviewDiagnosticDetailText);
        Assert.Single(appLogService.WarningMessages);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldShowUnavailable_WhenPreviewQueryThrows()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-button");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("PreviewQueryThrows");
        var workspaceSummary = CreateWorkspaceSummary(workspaceId, workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = static _ => throw new InvalidOperationException("preview failed")
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            new TestBackgroundTaskRunner());
        var assetTab = CreateAssetTab(bundleId, assetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.False(assetTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(ResourceKeys.InspectorPreviewUnavailableText, assetTab.Inspector.Preview.PreviewStatusText);
        Assert.Contains("preview failed", assetTab.Inspector.Preview.PreviewDiagnosticDetailText);
        Assert.Single(appLogService.WarningMessages);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldShowUnavailable_WhenCurrentPreviewQueryIsCancelled()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-button");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("PreviewCancelled");
        var workspaceSummary = CreateWorkspaceSummary(workspaceId, workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = static _ => Task.FromResult(Result<AssetPreviewDto>.Cancelled())
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            new TestBackgroundTaskRunner());
        var assetTab = CreateAssetTab(bundleId, assetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.False(assetTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(ResourceKeys.InspectorPreviewUnavailableText, assetTab.Inspector.Preview.PreviewStatusText);
        Assert.Empty(appLogService.WarningMessages);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldShowUnavailable_WhenPreviewQueryTimesOut()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-button");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("PreviewTimeout");
        var workspaceSummary = CreateWorkspaceSummary(workspaceId, workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);
        var pendingPreview = new TaskCompletionSource<Result<AssetPreviewDto>>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = _ => pendingPreview.Task
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            new TestBackgroundTaskRunner(),
            previewQueryTimeout: TimeSpan.FromMilliseconds(20),
            previewWorkerCount: 1);
        var assetTab = CreateAssetTab(bundleId, assetId, localizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed");

        Assert.False(assetTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(ResourceKeys.InspectorPreviewUnavailableText, assetTab.Inspector.Preview.PreviewStatusText);
        Assert.Equal(ResourceKeys.BundleExplorerPreviewLoadTimedOut, assetTab.Inspector.Preview.PreviewDiagnosticDetailText);
        Assert.Single(appLogService.WarningMessages);

        pendingPreview.SetResult(Result<AssetPreviewDto>.Cancelled());
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldSkipStaleQueuedPreview_WhenTabIsNoLongerRelevant()
    {
        var workspaceId = WorkspaceId.New();
        var firstBundleId = new BundleId("bundle-ui");
        var firstAssetId = new AssetId("asset-button");
        var secondAssetId = new AssetId("asset-panel");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("PreviewRelevance");
        var workspaceSummary = CreateWorkspaceSummary(workspaceId, workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);
        var firstPreviewCanFinish = new TaskCompletionSource<Result<AssetPreviewDto>>();
        var previewQueryAssetIds = new List<AssetId>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                previewQueryAssetIds.Add(query.AssetId);
                return query.AssetId.Equals(firstAssetId)
                    ? firstPreviewCanFinish.Task
                    : Task.FromResult(CreateSummaryPreviewResult(query));
            }
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            new TestBackgroundTaskRunner(),
            previewWorkerCount: 1);
        var firstTab = CreateAssetTab(firstBundleId, firstAssetId, localizationService);
        var secondTab = CreateAssetTab(firstBundleId, secondAssetId, localizationService);
        var firstDetails = CreateAssetDetails(workspaceId, firstBundleId, firstAssetId);
        var secondDetails = CreateAssetDetails(workspaceId, firstBundleId, secondAssetId);
        firstTab.ApplyDetails(firstDetails);
        secondTab.ApplyDetails(secondDetails);
        var activeTab = firstTab;

        var firstLoad = coordinator.LoadInspectorPreviewAsync(
            firstTab,
            firstDetails,
            static _ => "failed",
            () => ReferenceEquals(activeTab, firstTab));
        await WaitUntilAsync(() => previewQueryAssetIds.Count == 1);
        activeTab = secondTab;
        var secondLoad = coordinator.LoadInspectorPreviewAsync(
            secondTab,
            secondDetails,
            static _ => "failed",
            () => ReferenceEquals(activeTab, secondTab));
        var staleReplayLoad = coordinator.LoadInspectorPreviewAsync(
            firstTab,
            firstDetails,
            static _ => "failed",
            () => ReferenceEquals(activeTab, firstTab));

        firstPreviewCanFinish.SetResult(Result<AssetPreviewDto>.Cancelled());
        await Task.WhenAll(firstLoad, secondLoad, staleReplayLoad);

        Assert.Equal([firstAssetId, secondAssetId], previewQueryAssetIds);
        Assert.False(firstTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal("summary", secondTab.Inspector.Preview.PreviewText);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldNotBlockCurrentPreview_WhenPreviousStartedPreviewIsCancelled()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var firstAssetId = new AssetId("asset-button");
        var secondAssetId = new AssetId("asset-panel");
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("PreviewStartedCancellation");
        var workspaceSummary = CreateWorkspaceSummary(workspaceId, workspaceRequest);
        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceSummary,
            workspaceRequest.WorkspaceDataRootPath,
            workspaceRequest.AssetBundleDecryptorId);
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        workspaceShell.ApplyWorkspace(workspaceSummary, workspaceRequest);
        var firstPreviewCanFinish = new TaskCompletionSource<Result<AssetPreviewDto>>();
        var previewQueryAssetIds = new List<AssetId>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                previewQueryAssetIds.Add(query.AssetId);
                return query.AssetId.Equals(firstAssetId)
                    ? firstPreviewCanFinish.Task
                    : Task.FromResult(CreateSummaryPreviewResult(query));
            }
        };
        var coordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            workspaceContextService,
            workspaceShell,
            appLogService,
            localizationService,
            new TestBackgroundTaskRunner(),
            previewWorkerCount: 1);
        var firstTab = CreateAssetTab(bundleId, firstAssetId, localizationService);
        var secondTab = CreateAssetTab(bundleId, secondAssetId, localizationService);
        var firstDetails = CreateAssetDetails(workspaceId, bundleId, firstAssetId);
        var secondDetails = CreateAssetDetails(workspaceId, bundleId, secondAssetId);
        firstTab.ApplyDetails(firstDetails);
        secondTab.ApplyDetails(secondDetails);
        var activeTab = firstTab;

        var firstLoad = coordinator.LoadInspectorPreviewAsync(
            firstTab,
            firstDetails,
            static _ => "failed",
            () => ReferenceEquals(activeTab, firstTab));
        await WaitUntilAsync(() => previewQueryAssetIds.Count == 1);
        activeTab = secondTab;
        firstTab.CancelPreviewRequest();
        var secondLoad = coordinator.LoadInspectorPreviewAsync(
            secondTab,
            secondDetails,
            static _ => "failed",
            () => ReferenceEquals(activeTab, secondTab));
        await WaitUntilAsync(() => secondTab.Inspector.Preview.PreviewText == "summary");

        Assert.Equal([firstAssetId, secondAssetId], previewQueryAssetIds);

        firstPreviewCanFinish.SetResult(Result<AssetPreviewDto>.Cancelled());
        await Task.WhenAll(firstLoad, secondLoad);
    }

}
