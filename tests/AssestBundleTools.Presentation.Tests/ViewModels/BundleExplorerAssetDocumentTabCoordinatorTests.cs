using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetDocumentTabCoordinatorTests
{
    [Fact]
    public async Task LoadAssetDocumentTabAsync_ShouldUseBackgroundRunner_ForOpenAndDetailsQueries()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-button");
        var context = CreateCoordinatorContext(workspaceId, "DetailsBackground");
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            OpenAssetInPaneHandler = command => Task.FromResult(Result<AssetPaneContextDto>.Success(
                new AssetPaneContextDto(
                    command.WorkspaceId,
                    command.PaneId,
                    command.BundleId,
                    command.AssetId,
                    "Button",
                    WorkspaceMode.SinglePane))),
            GetAssetDetailsHandler = _ => Task.FromResult(Result<AssetDetailsDto>.Success(details)),
            GetAssetPreviewHandler = query => Task.FromResult(CreateSummaryPreviewResult(query))
        };
        var coordinator = CreateCoordinator(context, inspectionGateway);
        var assetTab = context.HostCoordinator.CreateAssetDocumentTab(bundleId, assetId, "Button");
        context.BundleTab.AddAssetTab(assetTab, activate: false);

        await coordinator.ActivateAssetDocumentTabAsync(assetTab);

        Assert.NotNull(assetTab.Details);
        Assert.True(context.BackgroundTaskRunner.RunCallCount >= 2);
    }

    [Fact]
    public async Task ActivateAssetDocumentTabAsync_ShouldPromoteCurrentPreviewWithoutCancellingPreviousTab()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var firstAssetId = new AssetId("asset-button");
        var secondAssetId = new AssetId("asset-panel");
        var context = CreateCoordinatorContext(workspaceId, "PreviewTabSwitch");
        var firstDetails = CreateAssetDetails(workspaceId, bundleId, firstAssetId);
        var secondDetails = CreateAssetDetails(workspaceId, bundleId, secondAssetId);
        var firstPreviewCanFinish = new TaskCompletionSource<Result<AssetPreviewDto>>();
        var previewQueryAssetIds = new List<AssetId>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            OpenAssetInPaneHandler = command => Task.FromResult(Result<AssetPaneContextDto>.Success(
                new AssetPaneContextDto(
                    command.WorkspaceId,
                    command.PaneId,
                    command.BundleId,
                    command.AssetId,
                    "Asset",
                    WorkspaceMode.SinglePane))),
            GetAssetDetailsHandler = query => Task.FromResult(Result<AssetDetailsDto>.Success(
                query.AssetId.Equals(firstAssetId) ? firstDetails : secondDetails)),
            GetAssetPreviewHandler = query =>
            {
                previewQueryAssetIds.Add(query.AssetId);
                return query.AssetId.Equals(firstAssetId) && previewQueryAssetIds.Count == 1
                    ? firstPreviewCanFinish.Task
                    : Task.FromResult(CreateSummaryPreviewResult(query));
            }
        };
        var coordinator = CreateCoordinator(context, inspectionGateway);
        var firstTab = context.HostCoordinator.CreateAssetDocumentTab(bundleId, firstAssetId, "Button");
        var secondTab = context.HostCoordinator.CreateAssetDocumentTab(bundleId, secondAssetId, "Panel");
        context.BundleTab.AddAssetTab(firstTab, activate: false);
        context.BundleTab.AddAssetTab(secondTab, activate: false);

        await coordinator.ActivateAssetDocumentTabAsync(firstTab);
        await WaitUntilAsync(() => firstTab.Inspector.Preview.IsPreviewLoading);
        await coordinator.ActivateAssetDocumentTabAsync(secondTab);
        await WaitUntilAsync(() => secondTab.Inspector.Preview.PreviewText == "summary");
        firstPreviewCanFinish.SetResult(CreateSummaryPreviewResult(new GetAssetPreviewQuery(
            workspaceId,
            WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
                context.WorkspaceRequest.SourcePath,
                context.WorkspaceRequest.WorkspaceDataRootPath),
            context.WorkspaceRequest.WorkspaceDataRootPath,
            bundleId,
            firstAssetId)));
        await coordinator.ActivateAssetDocumentTabAsync(firstTab);
        await WaitUntilAsync(() => firstTab.Inspector.Preview.PreviewText == "summary");

        Assert.False(secondTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal([firstAssetId, secondAssetId, firstAssetId], previewQueryAssetIds);
    }

    private static CoordinatorContext CreateCoordinatorContext(WorkspaceId workspaceId, string workspaceName)
    {
        var localizationService = new TestLocalizationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest(workspaceName);
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
        var documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        var bundleTab = new BundleDocumentTabViewModel(new BundleId("bundle-ui"), "ui.ab", "ui.ab", isPreview: false);
        documentWorkspace.BundleTabs.Add(bundleTab);
        documentWorkspace.SetActiveBundleTab(bundleTab);
        var hostCoordinator = new BundleExplorerAssetDocumentHostCoordinator(
            documentWorkspace,
            localizationService,
            static () => { });

        return new CoordinatorContext(
            LocalizationService: localizationService,
            AppLogService: new TestAppLogService(),
            WorkspaceContextService: workspaceContextService,
            WorkspaceShell: workspaceShell,
            DocumentWorkspace: documentWorkspace,
            BundleTab: bundleTab,
            InspectorPresenter: new BundleExplorerAssetInspectorPresenter(
                new BundleExplorerBundlePaneViewModel(localizationService),
                localizationService),
            HostCoordinator: hostCoordinator,
            PrimaryPane: new AssetPaneViewModel(),
            BackgroundTaskRunner: new TestBackgroundTaskRunner(),
            WorkspaceRequest: workspaceRequest);
    }

    private static BundleExplorerAssetDocumentTabCoordinator CreateCoordinator(
        CoordinatorContext context,
        TestWorkspaceAssetInspectionGateway inspectionGateway)
    {
        var previewCoordinator = new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            context.WorkspaceContextService,
            context.WorkspaceShell,
            context.AppLogService,
            context.LocalizationService,
            context.BackgroundTaskRunner);

        return new BundleExplorerAssetDocumentTabCoordinator(
            inspectionGateway,
            context.WorkspaceShell,
            context.DocumentWorkspace,
            context.InspectorPresenter,
            previewCoordinator,
            context.AppLogService,
            context.LocalizationService,
            context.BackgroundTaskRunner,
            context.PrimaryPane,
            context.HostCoordinator,
            isBusyProvider: static () => false,
            requestWorkspaceBrowserStatePersistence: static () => { },
            notifyActionStates: static () => { },
            appendResultFailure: static _ => { },
            formatFirstErrorMessage: static _ => "failed");
    }

    private sealed record CoordinatorContext(
        TestLocalizationService LocalizationService,
        TestAppLogService AppLogService,
        TestWorkspaceContextService WorkspaceContextService,
        BundleExplorerWorkspaceShellViewModel WorkspaceShell,
        BundleExplorerDocumentWorkspaceViewModel DocumentWorkspace,
        BundleDocumentTabViewModel BundleTab,
        BundleExplorerAssetInspectorPresenter InspectorPresenter,
        BundleExplorerAssetDocumentHostCoordinator HostCoordinator,
        AssetPaneViewModel PrimaryPane,
        TestBackgroundTaskRunner BackgroundTaskRunner,
        WorkspaceNavigationRequest WorkspaceRequest);
}
