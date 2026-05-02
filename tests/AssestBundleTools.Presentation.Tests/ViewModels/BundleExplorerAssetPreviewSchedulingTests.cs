using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using static AssestBundleTools.Presentation.Tests.ViewModels.BundleExplorerPreviewTestSupport;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerAssetPreviewSchedulingTests
{
    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldRunForegroundAndBackgroundPrewarmOnSeparateSlots()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var foregroundAssetId = new AssetId("asset-current");
        var backgroundAssetId = new AssetId("asset-background");
        var context = CreatePreviewContext(workspaceId, "PreviewPrioritySlots");
        var foregroundCanFinish = new TaskCompletionSource<Result<AssetPreviewDto>>();
        var backgroundCanFinish = new TaskCompletionSource<Result<AssetPreviewDto>>();
        var startedAssetIds = new List<AssetId>();
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                startedAssetIds.Add(query.AssetId);
                return query.AssetId.Equals(foregroundAssetId)
                    ? foregroundCanFinish.Task
                    : backgroundCanFinish.Task;
            }
        };
        var coordinator = CreateCoordinator(context, inspectionGateway, previewWorkerCount: 4);
        var foregroundTab = CreateAssetTab(bundleId, foregroundAssetId, context.LocalizationService);
        var backgroundTab = CreateAssetTab(bundleId, backgroundAssetId, context.LocalizationService);
        var foregroundDetails = CreateAssetDetails(workspaceId, bundleId, foregroundAssetId);
        var backgroundDetails = CreateAssetDetails(workspaceId, bundleId, backgroundAssetId);
        foregroundTab.ApplyDetails(foregroundDetails);
        backgroundTab.ApplyDetails(backgroundDetails);

        var foregroundLoad = coordinator.LoadInspectorPreviewAsync(
            foregroundTab,
            foregroundDetails,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.Foreground);
        var backgroundLoad = coordinator.LoadInspectorPreviewAsync(
            backgroundTab,
            backgroundDetails,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.BackgroundPrewarm);
        await WaitUntilAsync(() => startedAssetIds.Count == 2);

        Assert.Contains(foregroundAssetId, startedAssetIds);
        Assert.Contains(backgroundAssetId, startedAssetIds);

        foregroundCanFinish.SetResult(CreateSummaryPreviewResult(CreatePreviewQuery(
            context,
            workspaceId,
            bundleId,
            foregroundAssetId)));
        backgroundCanFinish.SetResult(CreateSummaryPreviewResult(CreatePreviewQuery(
            context,
            workspaceId,
            bundleId,
            backgroundAssetId)));
        await Task.WhenAll(foregroundLoad, backgroundLoad);
    }

    [Fact]
    public async Task LoadInspectorPreviewAsync_ShouldSkipBackgroundPrewarm_WhenOnlyForegroundSlotExists()
    {
        var workspaceId = WorkspaceId.New();
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-background");
        var context = CreatePreviewContext(workspaceId, "PreviewSingleSlot");
        var previewQueryCount = 0;
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetPreviewHandler = query =>
            {
                previewQueryCount++;
                return Task.FromResult(CreateSummaryPreviewResult(query));
            }
        };
        var coordinator = CreateCoordinator(context, inspectionGateway, previewWorkerCount: 1);
        var assetTab = CreateAssetTab(bundleId, assetId, context.LocalizationService);
        var details = CreateAssetDetails(workspaceId, bundleId, assetId);
        assetTab.ApplyDetails(details);

        await coordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            static _ => "failed",
            () => true,
            AssetPreviewLoadPriority.BackgroundPrewarm);

        Assert.Equal(0, previewQueryCount);
        Assert.False(assetTab.Inspector.Preview.IsPreviewLoading);
        Assert.Equal(string.Empty, assetTab.Inspector.Preview.PreviewText);
    }

    private static PreviewContext CreatePreviewContext(WorkspaceId workspaceId, string workspaceName)
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

        return new PreviewContext(
            LocalizationService: localizationService,
            AppLogService: new TestAppLogService(),
            WorkspaceContextService: workspaceContextService,
            WorkspaceShell: workspaceShell,
            WorkspaceRequest: workspaceRequest);
    }

    private static BundleExplorerAssetPreviewCoordinator CreateCoordinator(
        PreviewContext context,
        TestWorkspaceAssetInspectionGateway inspectionGateway,
        int previewWorkerCount)
    {
        return new BundleExplorerAssetPreviewCoordinator(
            inspectionGateway,
            context.WorkspaceContextService,
            context.WorkspaceShell,
            context.AppLogService,
            context.LocalizationService,
            new TestBackgroundTaskRunner(),
            previewWorkerCount: previewWorkerCount);
    }

    private static GetAssetPreviewQuery CreatePreviewQuery(
        PreviewContext context,
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId)
    {
        return new GetAssetPreviewQuery(
            workspaceId,
            WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
                context.WorkspaceRequest.SourcePath,
                context.WorkspaceRequest.WorkspaceDataRootPath),
            context.WorkspaceRequest.WorkspaceDataRootPath,
            bundleId,
            assetId);
    }

    private sealed record PreviewContext(
        TestLocalizationService LocalizationService,
        TestAppLogService AppLogService,
        TestWorkspaceContextService WorkspaceContextService,
        BundleExplorerWorkspaceShellViewModel WorkspaceShell,
        WorkspaceNavigationRequest WorkspaceRequest);
}
