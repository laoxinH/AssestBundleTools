using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerWorkspaceBindingCoordinatorTests
{
    [Fact]
    public async Task LoadWorkspaceAsync_WhenTargetBundleTreeLoadFails_ShouldRestorePreviousSessionContext()
    {
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var notificationService = new TestAppNotificationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var assetBundleDecryptorRegistry = new TestAssetBundleDecryptorRegistry();
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            assetBundleDecryptorRegistry);
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        var assetWorkflow = new TestAssetWorkflowBindingHost();
        var browserStateCoordinator = new TestBrowserStateCoordinator(restoreResult: false);
        var openBundleCalls = 0;
        var workspaceARequest = PresentationTestDataFactory.CreateNavigationRequest("WorkspaceA");
        var workspaceASummary = PresentationTestDataFactory.CreateWorkspaceSummary(workspaceARequest);
        var workspaceBRequest = PresentationTestDataFactory.CreateNavigationRequest("WorkspaceB");
        var workspaceBSummary = PresentationTestDataFactory.CreateWorkspaceSummary(workspaceBRequest);

        workspaceContextService.RegisterOrActivateWorkspace(
            workspaceASummary,
            workspaceARequest.WorkspaceDataRootPath,
            workspaceARequest.AssetBundleDecryptorId);

        var workspaceExplorerGateway = new TestWorkspaceExplorerGateway
        {
            OpenWorkspaceHandler = command => Task.FromResult(
                string.Equals(command.WorkspacePath, workspaceBRequest.SourcePath, StringComparison.Ordinal)
                    ? Result<AssestBundleTools.Application.Workspace.Dtos.WorkspaceSummaryDto>.Success(workspaceBSummary)
                    : Result<AssestBundleTools.Application.Workspace.Dtos.WorkspaceSummaryDto>.Failure(
                        PresentationTestDataFactory.CreateFailure("tests.unexpected-open"))),
            LoadBundleTreeHandler = query => Task.FromResult(
                string.Equals(query.WorkspaceId.Value, workspaceBSummary.WorkspaceId.Value, StringComparison.Ordinal)
                    ? Result<IReadOnlyList<AssestBundleTools.Application.Bundles.Dtos.BundleTreeNodeDto>>.Failure(
                        PresentationTestDataFactory.CreateFailure("tests.bundle-tree-failed"))
                    : Result<IReadOnlyList<AssestBundleTools.Application.Bundles.Dtos.BundleTreeNodeDto>>.Success(
                        PresentationTestDataFactory.CreateBundleTree("WorkspaceA")))
        };

        var coordinator = new BundleExplorerWorkspaceBindingCoordinator(
            workspaceExplorerGateway,
            appLogService,
            localizationService,
            workspaceContextService,
            new BundleExplorerOperationFeedbackBridge(
                appLogService,
                notificationService,
                localizationService),
            workspaceShell,
            bundlePane,
            documentWorkspace,
            assetWorkflow,
            browserStateCoordinator,
            isBusyProvider: () => false,
            setBusyState: static _ => { },
            openBundleAsync: (_, _) =>
            {
                openBundleCalls++;
                return Task.CompletedTask;
            });

        await coordinator.LoadWorkspaceAsync(
            clearExistingLogs: true,
            workspaceNavigationRequest: workspaceBRequest,
            showLoadedNotification: false);

        Assert.Equal(workspaceASummary.WorkspaceId, workspaceContextService.CurrentWorkspaceSummary?.WorkspaceId);
        Assert.Equal(workspaceASummary.WorkspaceId, workspaceShell.WorkspaceSummary?.WorkspaceId);
        Assert.True(bundlePane.HasAnyBundles);
        Assert.Equal(0, workspaceContextService.ClearCurrentWorkspaceCallCount);
        Assert.Equal(1, openBundleCalls);
        Assert.Equal(1, browserStateCoordinator.RestoreCallCount);
    }

    [Fact]
    public async Task LoadWorkspaceAsync_WhenNoRestorePointExistsAndOpenFails_ShouldResetToEmptyState()
    {
        var localizationService = new TestLocalizationService();
        var appLogService = new TestAppLogService();
        var notificationService = new TestAppNotificationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var workspaceShell = new BundleExplorerWorkspaceShellViewModel(
            localizationService,
            workspaceContextService,
            new TestAssetBundleDecryptorRegistry());
        var bundlePane = new BundleExplorerBundlePaneViewModel(localizationService);
        var documentWorkspace = new BundleExplorerDocumentWorkspaceViewModel();
        var assetWorkflow = new TestAssetWorkflowBindingHost();
        var browserStateCoordinator = new TestBrowserStateCoordinator(restoreResult: false);
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("WorkspaceFailure");

        bundlePane.RebuildBundleGroups(PresentationTestDataFactory.CreateBundleTree("StaleWorkspace"));
        documentWorkspace.BundleTabs.Add(new BundleDocumentTabViewModel(
            new AssestBundleTools.Domain.Bundles.BundleId("stale-bundle"),
            "StaleBundle",
            "StaleBundle.ab",
            isPreview: false));

        var workspaceExplorerGateway = new TestWorkspaceExplorerGateway
        {
            OpenWorkspaceHandler = _ => Task.FromResult(
                Result<AssestBundleTools.Application.Workspace.Dtos.WorkspaceSummaryDto>.Failure(
                    PresentationTestDataFactory.CreateFailure("tests.open-failed"))),
            LoadBundleTreeHandler = _ => Task.FromResult(
                Result<IReadOnlyList<AssestBundleTools.Application.Bundles.Dtos.BundleTreeNodeDto>>.Failure(
                    PresentationTestDataFactory.CreateFailure("tests.unexpected-tree")))
        };

        var coordinator = new BundleExplorerWorkspaceBindingCoordinator(
            workspaceExplorerGateway,
            appLogService,
            localizationService,
            workspaceContextService,
            new BundleExplorerOperationFeedbackBridge(
                appLogService,
                notificationService,
                localizationService),
            workspaceShell,
            bundlePane,
            documentWorkspace,
            assetWorkflow,
            browserStateCoordinator,
            isBusyProvider: () => false,
            setBusyState: static _ => { },
            openBundleAsync: static (_, _) => Task.CompletedTask);

        await coordinator.LoadWorkspaceAsync(
            clearExistingLogs: true,
            workspaceNavigationRequest: workspaceRequest,
            showLoadedNotification: false);

        Assert.Null(workspaceContextService.CurrentWorkspaceSummary);
        Assert.Null(workspaceShell.WorkspaceSummary);
        Assert.False(bundlePane.HasAnyBundles);
        Assert.Empty(documentWorkspace.BundleTabs);
        Assert.Equal(1, workspaceContextService.ClearCurrentWorkspaceCallCount);
        Assert.Equal(1, assetWorkflow.ClearActiveTabStateCallCount);
        Assert.Equal(1, browserStateCoordinator.CancelCallCount);
    }
}
