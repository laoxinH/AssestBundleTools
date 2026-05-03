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

    [Fact]
    public void AssetDocumentTabPreviewImageDisplayOptions_ShouldRequestBrowserStatePersistence_WhenUserChangesModes()
    {
        var context = CreateCoordinatorContext(WorkspaceId.New(), "PreviewOptionsPersistence");
        var assetTab = context.HostCoordinator.CreateAssetDocumentTab(
            new BundleId("bundle-ui"),
            new AssetId("asset-button"),
            "Button");

        assetTab.Inspector.Preview.Image.DisplayOptions.ToggleBackgroundMode();
        assetTab.Inspector.Preview.Image.DisplayOptions.ToggleSpriteDisplayMode();

        Assert.Equal(2, context.BrowserStatePersistenceRequestCount);
    }

    [Fact]
    public void AssetDocumentTabPreviewImageDisplayOptions_ShouldNotRequestBrowserStatePersistence_WhenRestoringModes()
    {
        var context = CreateCoordinatorContext(WorkspaceId.New(), "PreviewOptionsRestore");
        var assetTab = context.HostCoordinator.CreateAssetDocumentTab(
            new BundleId("bundle-ui"),
            new AssetId("asset-button"),
            "Button");

        assetTab.RestorePreviewImageDisplayOptions("Solid", "CropResult");

        Assert.Equal(0, context.BrowserStatePersistenceRequestCount);
        Assert.True(assetTab.Inspector.Preview.Image.DisplayOptions.IsSolidBackgroundSelected);
        Assert.True(assetTab.Inspector.Preview.Image.DisplayOptions.IsSpriteCropResultSelected);
    }

    [Fact]
    public void AssetDocumentTabPreviewMeshDisplayOptions_ShouldRequestBrowserStatePersistence_WhenUserChangesToolbarState()
    {
        var context = CreateCoordinatorContext(WorkspaceId.New(), "MeshPreviewOptionsPersistence");
        var assetTab = context.HostCoordinator.CreateAssetDocumentTab(
            new BundleId("bundle-ui"),
            new AssetId("asset-mesh"),
            "Mesh");
        assetTab.Inspector.Preview.Mesh.Apply(
            CreateMeshGeometry(),
            new MeshPreviewDisplayState("wire", "uv"));
        assetTab.Inspector.Preview.Mesh.ApplyTextureOverlay([1, 2, 3, 4]);

        assetTab.Inspector.Preview.Mesh.ToggleDisplayMode();
        assetTab.Inspector.Preview.Mesh.ToggleMeshGrid();
        assetTab.Inspector.Preview.Mesh.ToggleTextureOverlay();
        assetTab.Inspector.Preview.Mesh.ZoomBy(1.18d);
        assetTab.Inspector.Preview.Mesh.ResetView();

        Assert.Equal(5, context.BrowserStatePersistenceRequestCount);
    }

    [Fact]
    public void AssetDocumentTabPreviewMeshDisplayOptions_ShouldNotRequestBrowserStatePersistence_WhenRestoringMode()
    {
        var context = CreateCoordinatorContext(WorkspaceId.New(), "MeshPreviewOptionsRestore");
        var assetTab = context.HostCoordinator.CreateAssetDocumentTab(
            new BundleId("bundle-ui"),
            new AssetId("asset-mesh"),
            "Mesh");

        assetTab.RestorePreviewMeshDisplayOptions(
            "Uv",
            textureOverlaySelected: false,
            meshGridSelected: false,
            zoom: 1.5d);
        assetTab.Inspector.Preview.Mesh.Apply(
            CreateMeshGeometry(),
            new MeshPreviewDisplayState("wire", "uv"));
        assetTab.Inspector.Preview.Mesh.ApplyTextureOverlay([1, 2, 3, 4]);

        Assert.Equal(0, context.BrowserStatePersistenceRequestCount);
        Assert.True(assetTab.Inspector.Preview.Mesh.IsUvSelected);
        Assert.False(assetTab.Inspector.Preview.Mesh.IsTextureOverlaySelected);
        Assert.False(assetTab.Inspector.Preview.Mesh.IsMeshGridSelected);
        Assert.Equal(1.5d, assetTab.Inspector.Preview.Mesh.Zoom);
    }

    private static CoordinatorContext CreateCoordinatorContext(WorkspaceId workspaceId, string workspaceName)
    {
        var browserStatePersistenceRequestCount = 0;
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
            static () => { },
            () => browserStatePersistenceRequestCount++);

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
            WorkspaceRequest: workspaceRequest,
            GetBrowserStatePersistenceRequestCount: () => browserStatePersistenceRequestCount);
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

    private static AssetPreviewMeshGeometryDto CreateMeshGeometry()
    {
        return new AssetPreviewMeshGeometryDto(
            SchemaVersion: 3,
            SourceVertexCount: 3,
            SourceIndexCount: 3,
            SourceTriangleCount: 1,
            IsGeometryTruncated: false,
            Vertices:
            [
                new AssetPreviewMeshVertexDto(0, 0, 0),
                new AssetPreviewMeshVertexDto(1, 0, 0),
                new AssetPreviewMeshVertexDto(0, 1, 0)
            ],
            Edges:
            [
                new AssetPreviewMeshEdgeDto(0, 1),
                new AssetPreviewMeshEdgeDto(1, 2),
                new AssetPreviewMeshEdgeDto(0, 2)
            ],
            SubMeshes: Array.Empty<AssetPreviewMeshSubMeshDto>(),
            Bounds: null,
            Uv0:
            [
                new AssetPreviewMeshUvDto(0, 0),
                new AssetPreviewMeshUvDto(1, 0),
                new AssetPreviewMeshUvDto(0, 1)
            ],
            Channels: Array.Empty<AssetPreviewMeshChannelDto>(),
            Triangles:
            [
                new AssetPreviewMeshTriangleDto(0, 1, 2, 0)
            ]);
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
        WorkspaceNavigationRequest WorkspaceRequest,
        Func<int> GetBrowserStatePersistenceRequestCount)
    {
        public int BrowserStatePersistenceRequestCount => GetBrowserStatePersistenceRequestCount();
    }
}
