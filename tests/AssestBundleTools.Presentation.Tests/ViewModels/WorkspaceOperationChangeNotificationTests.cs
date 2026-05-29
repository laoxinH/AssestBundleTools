using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Replacement;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class WorkspaceOperationChangeNotificationTests
{
    [Fact]
    public async Task ExtractAssetAsync_ShouldNotifyWorkspaceOperationChange_WhenOperationSucceeds()
    {
        var workspaceId = new WorkspaceId("workspace-operation-notify");
        var notifier = new WorkspaceOperationChangeNotifier();
        var receivedEvents = new List<WorkspaceOperationChangedEventArgs>();
        notifier.WorkspaceOperationsChanged += (_, e) => receivedEvents.Add(e);
        var gateway = new NotifyingWorkspaceAssetOperationGateway(
            new SuccessfulWorkspaceAssetOperationGateway(),
            notifier);

        var result = await gateway.ExtractAssetAsync(
            new ExtractAssetCommand(
                workspaceId,
                new BundleId("bundle-1"),
                new AssetId("asset-1")));

        Assert.True(result.IsSuccess);
        var notification = Assert.Single(receivedEvents);
        Assert.Equal(workspaceId, notification.WorkspaceId);
        Assert.Equal(WorkspaceOperationChangeKind.Extraction, notification.ChangeKind);
        var affectedAsset = Assert.Single(notification.AffectedAssets);
        Assert.Equal(new BundleId("bundle-1"), affectedAsset.BundleId);
        Assert.Equal(new AssetId("asset-1"), affectedAsset.AssetId);
    }

    [Fact]
    public async Task DeleteWorkspaceOperationRecordAsync_ShouldNotifyAffectedAssetRefresh()
    {
        var workspaceId = new WorkspaceId("workspace-operation-delete-notify");
        var notifier = new WorkspaceOperationChangeNotifier();
        var receivedEvents = new List<WorkspaceOperationChangedEventArgs>();
        notifier.WorkspaceOperationsChanged += (_, e) => receivedEvents.Add(e);
        var gateway = new NotifyingWorkspaceAssetOperationGateway(
            new SuccessfulWorkspaceAssetOperationGateway(),
            notifier);

        var result = await gateway.DeleteWorkspaceOperationRecordAsync(
            new DeleteWorkspaceOperationRecordCommand(
                workspaceId,
                "operation-1",
                "workspace-a",
                @"D:\WorkspaceData\A"));

        Assert.True(result.IsSuccess);
        var notification = Assert.Single(receivedEvents);
        Assert.Equal(workspaceId, notification.WorkspaceId);
        Assert.Equal(
            WorkspaceOperationChangeKind.Extraction | WorkspaceOperationChangeKind.Replacement | WorkspaceOperationChangeKind.Edit,
            notification.ChangeKind);
        var affectedAsset = Assert.Single(notification.AffectedAssets);
        Assert.Equal(new BundleId("bundle-textures"), affectedAsset.BundleId);
        Assert.Equal(new AssetId("asset-texture"), affectedAsset.AssetId);
    }

    [Fact]
    public async Task ApplySpriteEditAsync_ShouldNotifyTargetAssetEditRefresh()
    {
        var workspaceId = new WorkspaceId("workspace-sprite-edit-notify");
        var notifier = new WorkspaceOperationChangeNotifier();
        var receivedEvents = new List<WorkspaceOperationChangedEventArgs>();
        notifier.WorkspaceOperationsChanged += (_, e) => receivedEvents.Add(e);
        var gateway = new NotifyingWorkspaceAssetOperationGateway(
            new SuccessfulWorkspaceAssetOperationGateway(),
            notifier);

        var result = await gateway.ApplySpriteEditAsync(
            new ApplySpriteEditCommand(
                workspaceId,
                new BundleId("bundle-1"),
                new AssetId("sprite-1"),
                new WorkspaceSpriteEditGeometryDto(256, 128, 4, 8, 64, 32, 36, 24)));

        Assert.True(result.IsSuccess);
        var notification = Assert.Single(receivedEvents);
        Assert.Equal(workspaceId, notification.WorkspaceId);
        Assert.Equal(WorkspaceOperationChangeKind.Edit, notification.ChangeKind);
        var affectedAsset = Assert.Single(notification.AffectedAssets);
        Assert.Equal(new BundleId("bundle-1"), affectedAsset.BundleId);
        Assert.Equal(new AssetId("sprite-1"), affectedAsset.AssetId);
    }

    [Fact]
    public async Task ReplaceAssetFromFileAsync_ShouldNotifyAssociatedAssetRefresh_WhenImpactRegistryFindsDependents()
    {
        var workspaceId = new WorkspaceId("workspace-associated-impact-notify");
        var textureBundleId = new BundleId("bundle-1");
        var textureAssetId = new AssetId("texture-1");
        var spriteAssetId = new AssetId("sprite-1");
        var notifier = new WorkspaceOperationChangeNotifier();
        var receivedEvents = new List<WorkspaceOperationChangedEventArgs>();
        notifier.WorkspaceOperationsChanged += (_, e) => receivedEvents.Add(e);
        var gateway = new NotifyingWorkspaceAssetOperationGateway(
            new SuccessfulWorkspaceAssetOperationGateway(),
            notifier,
            new TestAssociatedAssetImpactRegistry(
                new WorkspaceAssociatedAssetImpactDto(
                    textureBundleId,
                    spriteAssetId,
                    "HeroSprite",
                    AssetType.Sprite,
                    PathId: 12,
                    AssetTypeDisplayName: "Sprite")));

        var result = await gateway.ReplaceAssetFromFileAsync(
            new ReplaceAssetFromFileCommand(
                workspaceId,
                textureBundleId,
                textureAssetId,
                @"C:\Imports\texture.png",
                AssetType.Texture2D));

        Assert.True(result.IsSuccess);
        var notification = Assert.Single(receivedEvents);
        Assert.Equal(WorkspaceOperationChangeKind.Extraction | WorkspaceOperationChangeKind.Replacement, notification.ChangeKind);
        Assert.Contains(notification.AffectedAssets, affected =>
            affected.BundleId.Equals(textureBundleId) &&
            affected.AssetId.Equals(textureAssetId));
        Assert.Contains(notification.AffectedAssets, affected =>
            affected.BundleId.Equals(textureBundleId) &&
            affected.AssetId.Equals(spriteAssetId));
    }

    [Fact]
    public async Task UndoAssetReplacementAsync_ShouldNotifyAssociatedAssetRefresh_WhenProjectedRecordResolvesTarget()
    {
        var workspaceId = new WorkspaceId("workspace-associated-undo-notify");
        var spriteBundleId = new BundleId("bundle-1");
        var spriteAssetId = new AssetId("sprite-1");
        var textureBundleId = new BundleId("bundle-1");
        var textureAssetId = new AssetId("texture-1");
        var notifier = new WorkspaceOperationChangeNotifier();
        var receivedEvents = new List<WorkspaceOperationChangedEventArgs>();
        notifier.WorkspaceOperationsChanged += (_, e) => receivedEvents.Add(e);
        var gateway = new NotifyingWorkspaceAssetOperationGateway(
            new SuccessfulWorkspaceAssetOperationGateway
            {
                UndoResult = new AssetReplacementUndoResultDto(
                    workspaceId,
                    textureBundleId,
                    textureAssetId,
                    "HeroTexture")
            },
            notifier,
            new TestAssociatedAssetImpactRegistry(
                new WorkspaceAssociatedAssetImpactDto(
                    textureBundleId,
                    spriteAssetId,
                    "HeroSprite",
                    AssetType.Sprite,
                    PathId: 12,
                    AssetTypeDisplayName: "Sprite")));

        var result = await gateway.UndoAssetReplacementAsync(
            new UndoAssetReplacementCommand(
                workspaceId,
                spriteBundleId,
                spriteAssetId,
                ReplacementOperationId: "replace-texture"));

        Assert.True(result.IsSuccess);
        var notification = Assert.Single(receivedEvents);
        Assert.Equal(WorkspaceOperationChangeKind.Replacement, notification.ChangeKind);
        Assert.Contains(notification.AffectedAssets, affected =>
            affected.BundleId.Equals(spriteBundleId) &&
            affected.AssetId.Equals(spriteAssetId));
        Assert.Contains(notification.AffectedAssets, affected =>
            affected.BundleId.Equals(textureBundleId) &&
            affected.AssetId.Equals(textureAssetId));
    }

    [Fact]
    public async Task RedoAssetReplacementAsync_ShouldNotifyAssociatedAssetRefresh_WhenProjectedRecordResolvesTarget()
    {
        var workspaceId = new WorkspaceId("workspace-associated-redo-notify");
        var spriteBundleId = new BundleId("bundle-1");
        var spriteAssetId = new AssetId("sprite-1");
        var textureBundleId = new BundleId("bundle-1");
        var textureAssetId = new AssetId("texture-1");
        var notifier = new WorkspaceOperationChangeNotifier();
        var receivedEvents = new List<WorkspaceOperationChangedEventArgs>();
        notifier.WorkspaceOperationsChanged += (_, e) => receivedEvents.Add(e);
        var gateway = new NotifyingWorkspaceAssetOperationGateway(
            new SuccessfulWorkspaceAssetOperationGateway
            {
                RedoResult = new AssetReplacementRedoResultDto(
                    workspaceId,
                    textureBundleId,
                    textureAssetId,
                    "HeroTexture",
                    DateTimeOffset.UtcNow)
            },
            notifier,
            new TestAssociatedAssetImpactRegistry(
                new WorkspaceAssociatedAssetImpactDto(
                    textureBundleId,
                    spriteAssetId,
                    "HeroSprite",
                    AssetType.Sprite,
                    PathId: 12,
                    AssetTypeDisplayName: "Sprite")));

        var result = await gateway.RedoAssetReplacementAsync(
            new RedoAssetReplacementCommand(
                workspaceId,
                spriteBundleId,
                spriteAssetId,
                ReplacementOperationId: "replace-texture"));

        Assert.True(result.IsSuccess);
        var notification = Assert.Single(receivedEvents);
        Assert.Equal(WorkspaceOperationChangeKind.Replacement, notification.ChangeKind);
        Assert.Contains(notification.AffectedAssets, affected =>
            affected.BundleId.Equals(spriteBundleId) &&
            affected.AssetId.Equals(spriteAssetId));
        Assert.Contains(notification.AffectedAssets, affected =>
            affected.BundleId.Equals(textureBundleId) &&
            affected.AssetId.Equals(textureAssetId));
    }

    [Fact]
    public void BeginNotificationBatch_ShouldDeferAndCoalesceWorkspaceOperationChanges()
    {
        var workspaceId = new WorkspaceId("workspace-operation-batch");
        var notifier = new WorkspaceOperationChangeNotifier();
        var receivedEvents = new List<WorkspaceOperationChangedEventArgs>();
        notifier.WorkspaceOperationsChanged += (_, e) => receivedEvents.Add(e);

        using (notifier.BeginNotificationBatch())
        {
            notifier.NotifyWorkspaceOperationsChanged(
                workspaceId,
                WorkspaceOperationChangeKind.Extraction,
                [new WorkspaceOperationAffectedAsset(new BundleId("bundle-1"), new AssetId("asset-1"))]);
            notifier.NotifyWorkspaceOperationsChanged(
                workspaceId,
                WorkspaceOperationChangeKind.Replacement,
                [new WorkspaceOperationAffectedAsset(new BundleId("bundle-1"), new AssetId("asset-2"))]);

            Assert.Empty(receivedEvents);
        }

        var notification = Assert.Single(receivedEvents);
        Assert.Equal(workspaceId, notification.WorkspaceId);
        Assert.Equal(
            WorkspaceOperationChangeKind.Extraction | WorkspaceOperationChangeKind.Replacement,
            notification.ChangeKind);
        Assert.Equal(2, notification.AffectedAssets.Count);
    }

    [Fact]
    public async Task BatchTasksViewModel_ShouldRefreshChangeSummary_WhenCurrentWorkspaceOperationChanges()
    {
        var workspaceContextService = new TestWorkspaceContextService();
        var navigationRequest = PresentationTestDataFactory.CreateNavigationRequest("operation-refresh");
        var workspaceSummary = PresentationTestDataFactory.CreateWorkspaceSummary(navigationRequest);
        workspaceContextService.SetCurrentWorkspace(
            workspaceSummary,
            navigationRequest.WorkspaceDataRootPath,
            navigationRequest.AssetBundleDecryptorId);

        var batchGateway = new RecordingWorkspaceBatchOperationsGateway(workspaceSummary);
        var notifier = new WorkspaceOperationChangeNotifier();
        var viewModel = new BatchTasksViewModel(
            batchGateway,
            workspaceContextService,
            notifier,
            new TestLocalizationService(),
            new TestAppLogService(),
            new NoopWorkspaceImportCoordinatorService());

        await viewModel.InitializeAsync();
        Assert.False(viewModel.HasChanges);
        var queryCountBeforeNotification = batchGateway.ChangeSummaryQueryCount;

        batchGateway.ChangeSummary = CreateChangeSummary(
            workspaceSummary,
            [
                CreateChangeItem(
                    new BundleId("bundle-1"),
                    new AssetId("asset-1"),
                    "bundle.ab",
                    "texture.png")
            ]);

        notifier.NotifyWorkspaceOperationsChanged(
            workspaceSummary.WorkspaceId,
            WorkspaceOperationChangeKind.Extraction);

        Assert.True(batchGateway.ChangeSummaryQueryCount > queryCountBeforeNotification);
        Assert.True(viewModel.HasChanges);
        Assert.Single(viewModel.RecentChanges);
    }

    [Fact]
    public async Task BatchTasksViewModel_ShouldIgnoreOperationChange_ForDifferentWorkspace()
    {
        var workspaceContextService = new TestWorkspaceContextService();
        var navigationRequest = PresentationTestDataFactory.CreateNavigationRequest("operation-ignore");
        var workspaceSummary = PresentationTestDataFactory.CreateWorkspaceSummary(navigationRequest);
        workspaceContextService.SetCurrentWorkspace(
            workspaceSummary,
            navigationRequest.WorkspaceDataRootPath,
            navigationRequest.AssetBundleDecryptorId);

        var batchGateway = new RecordingWorkspaceBatchOperationsGateway(workspaceSummary);
        var notifier = new WorkspaceOperationChangeNotifier();
        var viewModel = new BatchTasksViewModel(
            batchGateway,
            workspaceContextService,
            notifier,
            new TestLocalizationService(),
            new TestAppLogService(),
            new NoopWorkspaceImportCoordinatorService());

        await viewModel.InitializeAsync();
        var queryCountBeforeNotification = batchGateway.ChangeSummaryQueryCount;

        batchGateway.ChangeSummary = CreateChangeSummary(
            workspaceSummary,
            [
                CreateChangeItem(
                    new BundleId("bundle-1"),
                    new AssetId("asset-1"),
                    "bundle.ab",
                    "texture.png")
            ]);

        notifier.NotifyWorkspaceOperationsChanged(
            new WorkspaceId("other-workspace"),
            WorkspaceOperationChangeKind.Extraction);

        Assert.Equal(queryCountBeforeNotification, batchGateway.ChangeSummaryQueryCount);
        Assert.False(viewModel.HasChanges);
        Assert.Empty(viewModel.RecentChanges);
    }

    [Fact]
    public async Task BundleExplorerViewModel_ShouldRefreshOpenDetailsWithoutReloadingAssetList_WhenCurrentWorkspaceOperationChanges()
    {
        var localizationService = new TestLocalizationService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("operation-detail-refresh");
        var workspaceSummary = PresentationTestDataFactory.CreateWorkspaceSummary(workspaceRequest);
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-button");
        var inactiveAssetId = new AssetId("asset-hidden-button");
        var extractedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 30, 0, TimeSpan.Zero);
        var loadAssetListCount = 0;
        var getAssetDetailsCount = 0;
        var returnExtractionState = false;
        var notifier = new WorkspaceOperationChangeNotifier();
        var workspaceContextService = new TestWorkspaceContextService();
        var explorerGateway = new TestWorkspaceExplorerGateway
        {
            OpenWorkspaceHandler = _ => Task.FromResult(Result<WorkspaceSummaryDto>.Success(workspaceSummary)),
            LoadBundleTreeHandler = _ => Task.FromResult(Result<IReadOnlyList<BundleTreeNodeDto>>.Success(
                PresentationTestDataFactory.CreateBundleTree("ui"))),
            LoadAssetListHandler = query =>
            {
                loadAssetListCount++;
                return Task.FromResult(Result<AssetListResultDto>.Success(
                    new AssetListResultDto(
                        query.WorkspaceId,
                        query.BundleId,
                        "ui",
                        query.SearchKeyword,
                        query.AssetTypeFilter,
                        TotalCount: 1,
                        FilteredCount: 1,
                        Items:
                        [
                            new AssetListItemDto(
                                assetId,
                                bundleId,
                                "Button",
                                "Assets/UI/Button.png",
                                AssetType.Texture2D,
                                SizeInBytes: 128,
                                PreferredDisplaySizeInBytes: null,
                                HasPreview: true,
                                IsModified: false)
                        ],
                        query.AssetTypeDisplayNameFilter,
                        TypeFacets: Array.Empty<AssetTypeFacetDto>())));
            }
        };
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
            GetAssetDetailsHandler = query =>
            {
                getAssetDetailsCount++;
                var details = BundleExplorerPreviewTestSupport.CreateAssetDetails(
                    query.WorkspaceId,
                    query.BundleId,
                    query.AssetId) with
                {
                    Name = query.AssetId.Equals(inactiveAssetId) ? "HiddenButton" : "Button"
                };
                if (returnExtractionState)
                {
                    details = details with { LastExtractedAtUtc = extractedAtUtc };
                }

                return Task.FromResult(Result<AssetDetailsDto>.Success(details));
            },
            GetAssetPreviewHandler = query => Task.FromResult(BundleExplorerPreviewTestSupport.CreateSummaryPreviewResult(query))
        };
        using var viewModel = new BundleExplorerViewModel(
            explorerGateway,
            inspectionGateway,
            new SuccessfulWorkspaceAssetOperationGateway(),
            new TestAssetEditDraftRestorePlanGateway(),
            new TestAppLogService(),
            new TestAppNotificationService(),
            localizationService,
            notifier,
            new NoopFilePickerService(),
            workspaceContextService,
            new NoopWorkspaceImportCoordinatorService(),
            new TestBackgroundTaskRunner(),
            new TestAssetBundleDecryptorRegistry(),
            new NoopPathNavigationService());

        await viewModel.InitializeAsync(workspaceRequest);
        var assetEntry = Assert.Single(viewModel.AssetWorkflow.PrimaryPane.Items);
        viewModel.AssetWorkflow.PrimaryPane.SelectedAsset = assetEntry;
        await viewModel.HandlePrimaryPaneSelectionChangedAsync();
        var activeAssetTab = Assert.IsType<AssetDocumentTabViewModel>(
            viewModel.DocumentWorkspace.ActiveAssetDocumentTab);
        var inactiveAssetTab = viewModel.AssetWorkflow.CreateAssetDocumentTab(
            bundleId,
            inactiveAssetId,
            "HiddenButton");
        viewModel.DocumentWorkspace.ActiveBundleTab!.AddAssetTab(inactiveAssetTab, activate: false);
        var loadAssetListCountBeforeNotification = loadAssetListCount;
        var getAssetDetailsCountBeforeNotification = getAssetDetailsCount;

        returnExtractionState = true;
        notifier.NotifyWorkspaceOperationsChanged(
            workspaceSummary.WorkspaceId,
            WorkspaceOperationChangeKind.Extraction);

        await BundleExplorerPreviewTestSupport.WaitUntilAsync(
            () => inactiveAssetTab.Details?.LastExtractedAtUtc == extractedAtUtc);
        Assert.Equal(loadAssetListCountBeforeNotification, loadAssetListCount);
        Assert.True(getAssetDetailsCount >= getAssetDetailsCountBeforeNotification + 2);
        Assert.Equal(
            extractedAtUtc,
            activeAssetTab.Details?.LastExtractedAtUtc);
        Assert.Same(activeAssetTab, viewModel.DocumentWorkspace.ActiveAssetDocumentTab);
    }

    [Fact]
    public async Task BundleExplorerViewModel_ShouldRefreshOnlyAffectedAsset_WhenOperationTargetsSingleAsset()
    {
        var localizationService = new TestLocalizationService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest("operation-targeted-refresh");
        var workspaceSummary = PresentationTestDataFactory.CreateWorkspaceSummary(workspaceRequest);
        var bundleId = new BundleId("bundle-ui");
        var assetId = new AssetId("asset-button");
        var inactiveAssetId = new AssetId("asset-hidden-button");
        var loadAssetListCount = 0;
        var getAssetDetailsCount = 0;
        var returnReplacementState = false;
        var notifier = new WorkspaceOperationChangeNotifier();
        var workspaceContextService = new TestWorkspaceContextService();
        var explorerGateway = new TestWorkspaceExplorerGateway
        {
            OpenWorkspaceHandler = _ => Task.FromResult(Result<WorkspaceSummaryDto>.Success(workspaceSummary)),
            LoadBundleTreeHandler = _ => Task.FromResult(Result<IReadOnlyList<BundleTreeNodeDto>>.Success(
                PresentationTestDataFactory.CreateBundleTree("ui"))),
            LoadAssetListHandler = query =>
            {
                loadAssetListCount++;
                return Task.FromResult(Result<AssetListResultDto>.Success(
                    new AssetListResultDto(
                        query.WorkspaceId,
                        query.BundleId,
                        "ui",
                        query.SearchKeyword,
                        query.AssetTypeFilter,
                        TotalCount: 1,
                        FilteredCount: 1,
                        Items:
                        [
                            new AssetListItemDto(
                                assetId,
                                bundleId,
                                "Button",
                                "Assets/UI/Button.png",
                                AssetType.Texture2D,
                                SizeInBytes: 128,
                                PreferredDisplaySizeInBytes: null,
                                HasPreview: true,
                                IsModified: false)
                        ],
                        query.AssetTypeDisplayNameFilter,
                        TypeFacets: Array.Empty<AssetTypeFacetDto>())));
            }
        };
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
            GetAssetDetailsHandler = query =>
            {
                getAssetDetailsCount++;
                var details = BundleExplorerPreviewTestSupport.CreateAssetDetails(
                    query.WorkspaceId,
                    query.BundleId,
                    query.AssetId) with
                {
                    Name = query.AssetId.Equals(inactiveAssetId) ? "HiddenButton" : "Button",
                    IsModified = returnReplacementState && query.AssetId.Equals(assetId),
                    FileReplacementSourceFileName = returnReplacementState && query.AssetId.Equals(assetId)
                        ? "ButtonReplacement.png"
                        : null,
                    FileReplacementSourcePath = returnReplacementState && query.AssetId.Equals(assetId)
                        ? @"D:\Imports\ButtonReplacement.png"
                        : null
                };

                return Task.FromResult(Result<AssetDetailsDto>.Success(details));
            },
            GetAssetPreviewHandler = query => Task.FromResult(BundleExplorerPreviewTestSupport.CreateSummaryPreviewResult(query))
        };
        using var viewModel = new BundleExplorerViewModel(
            explorerGateway,
            inspectionGateway,
            new SuccessfulWorkspaceAssetOperationGateway(),
            new TestAssetEditDraftRestorePlanGateway(),
            new TestAppLogService(),
            new TestAppNotificationService(),
            localizationService,
            notifier,
            new NoopFilePickerService(),
            workspaceContextService,
            new NoopWorkspaceImportCoordinatorService(),
            new TestBackgroundTaskRunner(),
            new TestAssetBundleDecryptorRegistry(),
            new NoopPathNavigationService());

        await viewModel.InitializeAsync(workspaceRequest);
        var assetEntry = Assert.Single(viewModel.AssetWorkflow.PrimaryPane.Items);
        viewModel.AssetWorkflow.PrimaryPane.SelectedAsset = assetEntry;
        await viewModel.HandlePrimaryPaneSelectionChangedAsync();
        var activeAssetTab = Assert.IsType<AssetDocumentTabViewModel>(
            viewModel.DocumentWorkspace.ActiveAssetDocumentTab);
        var inactiveAssetTab = viewModel.AssetWorkflow.CreateAssetDocumentTab(
            bundleId,
            inactiveAssetId,
            "HiddenButton");
        viewModel.DocumentWorkspace.ActiveBundleTab!.AddAssetTab(inactiveAssetTab, activate: false);
        var loadAssetListCountBeforeNotification = loadAssetListCount;
        var getAssetDetailsCountBeforeNotification = getAssetDetailsCount;

        returnReplacementState = true;
        notifier.NotifyWorkspaceOperationsChanged(
            workspaceSummary.WorkspaceId,
            WorkspaceOperationChangeKind.Replacement,
            [new WorkspaceOperationAffectedAsset(bundleId, assetId)]);

        await BundleExplorerPreviewTestSupport.WaitUntilAsync(
            () => activeAssetTab.Details?.IsModified == true);

        Assert.Equal(loadAssetListCountBeforeNotification, loadAssetListCount);
        Assert.Equal(getAssetDetailsCountBeforeNotification + 1, getAssetDetailsCount);
        Assert.Null(inactiveAssetTab.Details);
        Assert.True(assetEntry.Item.IsModified);
        Assert.Equal(ResourceKeys.AssetStateModified, assetEntry.StateText);
    }

    [Fact]
    public async Task WorkspaceOperationChangeRefreshCoordinator_ShouldCoalesceNotifications_WhileViewIsBusy()
    {
        var workspaceId = new WorkspaceId("workspace-operation-coalesce");
        var notifier = new WorkspaceOperationChangeNotifier();
        var isBusy = true;
        var refreshCount = 0;
        using var coordinator = new WorkspaceOperationChangeRefreshCoordinator(
            notifier,
            () => workspaceId,
            () => isBusy,
            () =>
            {
                refreshCount++;
                return Task.CompletedTask;
            });

        notifier.NotifyWorkspaceOperationsChanged(workspaceId, WorkspaceOperationChangeKind.Extraction);
        notifier.NotifyWorkspaceOperationsChanged(workspaceId, WorkspaceOperationChangeKind.Replacement);

        Assert.Equal(0, refreshCount);

        isBusy = false;
        coordinator.HandleBusyStateChanged(isBusy);
        await BundleExplorerPreviewTestSupport.WaitUntilAsync(() => refreshCount == 1);

        notifier.NotifyWorkspaceOperationsChanged(workspaceId, WorkspaceOperationChangeKind.Extraction);
        await BundleExplorerPreviewTestSupport.WaitUntilAsync(() => refreshCount == 2);
    }

    private static WorkspaceChangeSummaryDto CreateChangeSummary(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<WorkspaceChangeItemDto> items)
    {
        return new WorkspaceChangeSummaryDto(
            WorkspaceId: workspaceSummary.WorkspaceId,
            WorkspaceDisplayName: workspaceSummary.DisplayName,
            SourcePath: workspaceSummary.SourcePath,
            BundleCount: workspaceSummary.BundleCount,
            AssetCount: workspaceSummary.AssetCount,
            TotalChangeCount: items.Count,
            ModifiedAssetCount: items.Count(static item => item.IsModified),
            ExtractedAssetCount: items.Count(static item => item.LastExtractedAtUtc.HasValue),
            WorkspaceReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName)),
            FileReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName)),
            Items: items);
    }

    private static WorkspaceChangeItemDto CreateChangeItem(
        BundleId bundleId,
        AssetId assetId,
        string bundleDisplayName,
        string assetName)
    {
        var changedAtUtc = new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero);
        return new WorkspaceChangeItemDto(
            BundleId: bundleId,
            AssetId: assetId,
            BundleDisplayName: bundleDisplayName,
            AssetName: assetName,
            AssetType: AssetType.Texture2D,
            IsModified: false,
            LastExtractedAtUtc: changedAtUtc,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementAppliedAtUtc: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementAppliedAtUtc: null,
            LastChangedAtUtc: changedAtUtc);
    }

    private sealed class RecordingWorkspaceBatchOperationsGateway : IWorkspaceBatchOperationsGateway
    {
        private readonly WorkspaceSummaryDto _workspaceSummary;

        public RecordingWorkspaceBatchOperationsGateway(WorkspaceSummaryDto workspaceSummary)
        {
            _workspaceSummary = workspaceSummary;
            ChangeSummary = CreateChangeSummary(workspaceSummary, Array.Empty<WorkspaceChangeItemDto>());
        }

        public WorkspaceChangeSummaryDto ChangeSummary { get; set; }

        public int ChangeSummaryQueryCount { get; private set; }

        public Task<Result<WorkspaceChangeSummaryDto>> GetWorkspaceChangeSummaryAsync(
            GetWorkspaceChangeSummaryQuery query,
            CancellationToken cancellationToken = default)
        {
            ChangeSummaryQueryCount++;
            return Task.FromResult(Result<WorkspaceChangeSummaryDto>.Success(ChangeSummary));
        }

        public Task<Result<WorkspaceSavePreviewDto>> GetWorkspaceSavePreviewAsync(
            GetWorkspaceSavePreviewQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<WorkspaceSavePreviewDto>.Success(
                new WorkspaceSavePreviewDto(
                    WorkspaceId: _workspaceSummary.WorkspaceId,
                    WorkspaceDisplayName: _workspaceSummary.DisplayName,
                    OutputDirectory: string.Empty,
                    PlannedBundleCount: 0,
                    TotalChangeCount: 0,
                    ModifiedAssetCount: 0,
                    ExtractedAssetCount: 0,
                    WorkspaceReplacementCount: 0,
                    FileReplacementCount: 0,
                    BundlePlans: Array.Empty<WorkspaceSaveBundlePlanDto>())));

        public Task<Result<WorkspaceSaveExecutionResultDto>> SaveWorkspaceAsNewBundleAsync(
            SaveWorkspaceAsNewBundleCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchExtractionPreviewDto>> GetWorkspaceBatchExtractionPreviewAsync(
            GetWorkspaceBatchExtractionPreviewQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<WorkspaceBatchExtractionPreviewDto>.Success(
                new WorkspaceBatchExtractionPreviewDto(
                    WorkspaceId: _workspaceSummary.WorkspaceId,
                    WorkspaceDisplayName: _workspaceSummary.DisplayName,
                    AssetTypeFilter: query.AssetTypeFilter,
                    SuggestedOutputDirectory: string.Empty,
                    CandidateAssetCount: 0,
                    CandidateBundleCount: 0,
                    TotalSizeInBytes: 0,
                    BundlePlans: Array.Empty<WorkspaceBatchExtractionBundlePlanDto>())));

        public Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
            ExtractWorkspaceAssetsCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchReplacementPreviewDto>> GetWorkspaceBatchReplacementPreviewAsync(
            GetWorkspaceBatchReplacementPreviewQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<WorkspaceBatchReplacementPreviewDto>.Success(
                new WorkspaceBatchReplacementPreviewDto(
                    WorkspaceId: _workspaceSummary.WorkspaceId,
                    WorkspaceDisplayName: _workspaceSummary.DisplayName,
                    SourceFilter: query.SourceFilter,
                    CandidateAssetCount: 0,
                    CandidateBundleCount: 0,
                    WorkspaceReplacementCount: 0,
                    FileReplacementCount: 0,
                    BundlePlans: Array.Empty<WorkspaceBatchReplacementBundlePlanDto>())));

        public Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
            ExecuteWorkspaceBatchReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SuccessfulWorkspaceAssetOperationGateway : IWorkspaceAssetOperationGateway
    {
        public AssetReplacementUndoResultDto? UndoResult { get; init; }

        public AssetReplacementRedoResultDto? RedoResult { get; init; }

        public Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
            ExtractAssetCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AssetExtractionResultDto>.Success(
                new AssetExtractionResultDto(
                    WorkspaceId: command.WorkspaceId,
                    BundleId: command.BundleId,
                    AssetId: command.AssetId,
                    AssetName: "texture.png",
                    SuggestedOutputPath: "texture.png",
                    ExtractedAtUtc: DateTimeOffset.UtcNow)));

        public Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineAssetGroupAsync(
            ExtractSpineAssetGroupCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
            GetFileReplacementCandidateQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
            GetWorkspaceReplacementCandidatesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
            ReplaceAssetFromFileCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AssetFileReplacementResultDto>.Success(
                new AssetFileReplacementResultDto(
                    command.WorkspaceId,
                    command.TargetBundleId,
                    command.TargetAssetId,
                    "texture",
                    command.SourceFilePath,
                    Path.GetFileName(command.SourceFilePath),
                    command.SourceAssetType,
                    DateTimeOffset.UtcNow)));

        public Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
            ReplaceAssetFromWorkspaceCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
            UndoAssetReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AssetReplacementUndoResultDto>.Success(
                UndoResult ??
                new AssetReplacementUndoResultDto(
                    command.WorkspaceId,
                    command.BundleId,
                    command.AssetId,
                    "texture")));

        public Task<Result<AssetReplacementRedoResultDto>> RedoAssetReplacementAsync(
            RedoAssetReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AssetReplacementRedoResultDto>.Success(
                RedoResult ??
                new AssetReplacementRedoResultDto(
                    command.WorkspaceId,
                    command.BundleId,
                    command.AssetId,
                    "texture",
                    DateTimeOffset.UtcNow)));

        public Task<Result<AssetSpriteEditAppliedResultDto>> ApplySpriteEditAsync(
            ApplySpriteEditCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Success(
                new AssetSpriteEditAppliedResultDto(
                    command.WorkspaceId,
                    command.BundleId,
                    command.AssetId,
                    "sprite",
                    "draft-sprite",
                    "sprite-geometry",
                    command.Geometry,
                    DateTimeOffset.UtcNow)));

        public Task<Result<WorkspaceOperationRecordDeletedResultDto>> DeleteWorkspaceOperationRecordAsync(
            DeleteWorkspaceOperationRecordCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<WorkspaceOperationRecordDeletedResultDto>.Success(
                new WorkspaceOperationRecordDeletedResultDto(
                    command.WorkspaceId,
                    command.OperationId,
                    new BundleId("bundle-textures"),
                    new AssetId("asset-texture"),
                    DateTimeOffset.UtcNow)));
    }

    private sealed class TestAssociatedAssetImpactRegistry : IWorkspaceAssociatedAssetImpactRegistry
    {
        private readonly IReadOnlyList<WorkspaceAssociatedAssetImpactDto> _impacts;

        public TestAssociatedAssetImpactRegistry(params WorkspaceAssociatedAssetImpactDto[] impacts)
        {
            _impacts = impacts;
        }

        public bool TryGetAssociatedAssetImpacts(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out IReadOnlyList<WorkspaceAssociatedAssetImpactDto> impacts)
        {
            impacts = _impacts;
            return true;
        }
    }

    private sealed class NoopWorkspaceImportCoordinatorService : IWorkspaceImportCoordinatorService
    {
        public bool HasActiveWorkspace => true;

        public Task<bool> RequestBundleFileImportAsync(
            IProgress<WorkspaceImportProgressDto>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> RequestBundleDirectoryImportAsync(
            IProgress<WorkspaceImportProgressDto>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> RequestDragDropImportAsync(
            IReadOnlyList<string> droppedPaths,
            IProgress<WorkspaceImportProgressDto>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class NoopFilePickerService : IFilePickerService
    {
        public Task<string?> PickWorkspaceDataDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyList<string>> PickBundleFilePathsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<string?> PickBundleImportDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickSpineFrameSequenceExportDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickSpineAssetGroupExtractionDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickAssetExtractionOutputFileAsync(
            AssetType assetType,
            string? assetTypeDisplayName,
            string assetName,
            string? assetPath,
            string? containerPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickAssetBatchExtractionOutputDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickReplacementSourceFileAsync(
            AssetType targetAssetType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class NoopPathNavigationService : IPathNavigationService
    {
        public bool TryOpenContainingFolder(string path) => false;
    }
}
