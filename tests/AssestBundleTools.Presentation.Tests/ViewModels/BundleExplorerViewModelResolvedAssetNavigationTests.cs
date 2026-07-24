using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
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
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class BundleExplorerViewModelResolvedAssetNavigationTests
{
    [Fact]
    public async Task OpenResolvedAssetAsync_ShouldWarn_WhenNoWorkspaceIsActive()
    {
        var context = CreateContext("NoWorkspace");
        var target = new ResolvedAssetNavigationTarget(
            new AssetId("asset-texture"),
            new BundleId("bundle-textures"),
            200,
            "HeroTexture",
            "Texture2D");

        await context.ViewModel.OpenResolvedAssetAsync(target);

        Assert.Equal(1, context.NotificationService.ShowCallCount);
        Assert.Equal(ResourceKeys.AppNotificationWarningTitle, context.NotificationService.Title);
        Assert.Equal(
            ResourceKeys.BundleExplorerNotificationResolvedAssetNavigationNoWorkspace,
            context.NotificationService.Message);
        Assert.Equal(AppNotificationSeverity.Warning, context.NotificationService.Severity);
        Assert.Contains(
            ResourceKeys.BundleExplorerNotificationResolvedAssetNavigationNoWorkspace,
            context.AppLogService.WarningMessages);
    }

    [Fact]
    public async Task OpenResolvedAssetAsync_ShouldWarn_WhenTargetBundleIsUnavailable()
    {
        var context = CreateContext("MissingBundle");
        await context.ViewModel.InitializeAsync(context.WorkspaceRequest);
        var notificationCountBeforeJump = context.NotificationService.ShowCallCount;
        var target = new ResolvedAssetNavigationTarget(
            new AssetId("asset-texture"),
            new BundleId("bundle-textures"),
            200,
            "HeroTexture",
            "Texture2D");

        await context.ViewModel.OpenResolvedAssetAsync(target);

        Assert.Equal(notificationCountBeforeJump + 1, context.NotificationService.ShowCallCount);
        Assert.Equal(ResourceKeys.AppNotificationWarningTitle, context.NotificationService.Title);
        Assert.Equal(
            ResourceKeys.BundleExplorerNotificationResolvedAssetBundleUnavailableFormat + ":HeroTexture",
            context.NotificationService.Message);
        Assert.Equal(AppNotificationSeverity.Warning, context.NotificationService.Severity);
        Assert.Contains(
            ResourceKeys.BundleExplorerNotificationResolvedAssetBundleUnavailableFormat + ":HeroTexture",
            context.AppLogService.WarningMessages);
    }

    private static ResolvedNavigationContext CreateContext(string workspaceName)
    {
        var localizationService = new TestLocalizationService();
        var workspaceRequest = PresentationTestDataFactory.CreateNavigationRequest(workspaceName);
        var workspaceSummary = PresentationTestDataFactory.CreateWorkspaceSummary(workspaceRequest);
        var workspaceExplorerGateway = new TestWorkspaceExplorerGateway
        {
            OpenWorkspaceHandler = _ => Task.FromResult(Result<WorkspaceSummaryDto>.Success(workspaceSummary)),
            LoadBundleTreeHandler = _ => Task.FromResult(Result<IReadOnlyList<BundleTreeNodeDto>>.Success(
                PresentationTestDataFactory.CreateBundleTree("ui"))),
            LoadAssetListHandler = query => Task.FromResult(Result<AssetListResultDto>.Success(
                new AssetListResultDto(
                    query.WorkspaceId,
                    query.BundleId,
                    "ui.ab",
                    query.SearchKeyword,
                    query.AssetTypeFilter,
                    TotalCount: 0,
                    FilteredCount: 0,
                    Items: Array.Empty<AssetListItemDto>())))
        };
        var appLogService = new TestAppLogService();
        var notificationService = new TestAppNotificationService();
        var workspaceContextService = new TestWorkspaceContextService();
        var viewModel = new BundleExplorerViewModel(
            workspaceExplorerGateway,
            new TestWorkspaceAssetInspectionGateway
            {
                OpenAssetInPaneHandler = _ => throw new NotSupportedException(),
                GetAssetDetailsHandler = _ => throw new NotSupportedException(),
                GetAssetPreviewHandler = _ => throw new NotSupportedException()
            },
            new StubWorkspaceAssetOperationGateway(),
            new TestAssetEditDraftRestorePlanGateway(),
            appLogService,
            notificationService,
            localizationService,
            new WorkspaceOperationChangeNotifier(),
            new StubFilePickerService(),
            new StubWorkspaceBatchOperationsGateway(),
            workspaceContextService,
            new StubWorkspaceImportCoordinatorService(),
            new TestBackgroundTaskRunner(),
            new TestAssetBundleDecryptorRegistry(),
            new StubPathNavigationService());

        return new ResolvedNavigationContext(
            viewModel,
            workspaceRequest,
            appLogService,
            notificationService);
    }

    private sealed record ResolvedNavigationContext(
        BundleExplorerViewModel ViewModel,
        WorkspaceNavigationRequest WorkspaceRequest,
        TestAppLogService AppLogService,
        TestAppNotificationService NotificationService);

    private sealed class StubFilePickerService : IFilePickerService
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

        public Task<string?> PickWorkspaceBatchReplacementSourceDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickWorkspaceExportOutputDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickReplacementSourceFileAsync(
            AssetType targetAssetType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickSpineReplacementSourceDirectoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubWorkspaceImportCoordinatorService : IWorkspaceImportCoordinatorService
    {
        public bool HasActiveWorkspace => false;

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

    private sealed class StubWorkspaceBatchOperationsGateway : IWorkspaceBatchOperationsGateway
    {
        public Task<Result<WorkspaceChangeSummaryDto>> GetWorkspaceChangeSummaryAsync(
            GetWorkspaceChangeSummaryQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceSavePreviewDto>> GetWorkspaceSavePreviewAsync(
            GetWorkspaceSavePreviewQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceSaveExecutionResultDto>> SaveWorkspaceAsNewBundleAsync(
            SaveWorkspaceAsNewBundleCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchExtractionPreviewDto>> GetWorkspaceBatchExtractionPreviewAsync(
            GetWorkspaceBatchExtractionPreviewQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
            ExtractWorkspaceAssetsCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchReplacementPlanDto>> GetWorkspaceBatchReplacementPlanAsync(
            GetWorkspaceBatchReplacementPlanQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceBatchReplacementResultDto>> ExecuteWorkspaceBatchReplacementAsync(
            ExecuteWorkspaceBatchReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubPathNavigationService : IPathNavigationService
    {
        public bool TryOpenContainingFolder(string path) => false;
    }

    private sealed class StubWorkspaceAssetOperationGateway : IWorkspaceAssetOperationGateway
    {
        public Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
            ExtractAssetCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineAssetGroupAsync(
            ExtractSpineAssetGroupCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
            GetFileReplacementCandidateQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementSourceMatchPlanDto>> MatchAssetReplacementSourcesAsync(
            MatchAssetReplacementSourcesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
            GetWorkspaceReplacementCandidatesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
            ReplaceAssetFromFileCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
            ReplaceAssetFromWorkspaceCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
            UndoAssetReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementRedoResultDto>> RedoAssetReplacementAsync(
            RedoAssetReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetSpriteEditAppliedResultDto>> ApplySpriteEditAsync(
            ApplySpriteEditCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<SpineReplacementSourceMatchDto>>> MatchSpineReplacementSourcesAsync(
            MatchSpineReplacementSourcesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<SpineSkeletonFormatCompatibilityDto>> CheckSpineSkeletonReplacementCompatibilityAsync(
            CheckSpineSkeletonReplacementCompatibilityQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetTextEditDraftDto>> CreateTextAssetEditDraftAsync(
            CreateTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetTextEditDraftDto>> OpenTextAssetEditDraftAsync(
            OpenTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetTextEditAppliedResultDto>> ApplyTextAssetEditDraftAsync(
            ApplyTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetTextEditDraftUpdatedResultDto>> UpdateTextAssetEditDraftAsync(
            UpdateTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetTextEditDraftDiscardedResultDto>> DiscardTextAssetEditDraftAsync(
            DiscardTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceOperationRecordDeletedResultDto>> DeleteWorkspaceOperationRecordAsync(
            DeleteWorkspaceOperationRecordCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
