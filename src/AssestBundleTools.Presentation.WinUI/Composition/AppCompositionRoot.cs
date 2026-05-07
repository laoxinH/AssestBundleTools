using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Editing.Queries;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Infrastructure.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Bundles.Previewing;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Settings.Persistence;
using AssestBundleTools.Infrastructure.Workspace.Importing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;
using AssestBundleTools.Infrastructure.Workspace.Runtime;
using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.Composition;

/// <summary>
/// WinUI 组合根：这是 Presentation 中唯一允许直接装配 Infrastructure 具体实现的边界。
/// </summary>
/// <remarks>
/// 这里负责两件事：
/// 1. new 具体 Infrastructure 实现与 Application Handler；
/// 2. 把它们适配回 Application 端口或 Presentation 自己的窄抽象。
/// 除 Composition 目录外，其余 Presentation 代码不应直接引用 Infrastructure 命名空间。
/// </remarks>
internal static class AppCompositionRoot
{
    public static AppCompositionServices Build(AppCompositionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        StartupDiagnostics.Log("Creating composition root services.");

        var appPreferencesStore = new InfrastructureAppPreferencesPersistenceStore(new JsonAppPreferencesStore());
        var workspaceCatalogStore = new JsonWorkspaceCatalogStore();
        var workspaceMetadataStoreFactory = new JsonWorkspaceMetadataStoreFactory();
        var workspaceDataStoreFactory = new JsonWorkspaceDataStoreFactory();
        var workspaceSessionRegistry = new CachedWorkspaceSessionRegistry(workspaceDataStoreFactory);
        var workspaceDataRootService = new JsonWorkspaceDataRootService(
            workspaceMetadataStoreFactory,
            workspaceDataStoreFactory);

        var localizationService = new ResourceLocalizationService();
        var appPreferencesService = new AppPreferencesService(appPreferencesStore);

        var assetBundleDecryptorRegistry = new ReloadableAssetBundleDecryptorRegistry();
        Action reloadAssetBundleDecryptorRegistry = () => assetBundleDecryptorRegistry.Reload();
        var assetBundleDecryptorCatalogStore = new FileSystemAssetBundleDecryptorCatalogStore();

        var workspaceImportSourceDiscoveryService = new FileSystemWorkspaceImportSourceDiscoveryService();
        var workspaceImportFileSystemService = new FileSystemWorkspaceImportFileSystemService();
        var workspaceContextPersistenceService = new WorkspaceContextPersistenceService(
            workspaceCatalogStore,
            workspaceMetadataStoreFactory,
            workspaceImportFileSystemService);
        var workspaceContextCatalogService = new WorkspaceContextCatalogService(
            workspaceContextPersistenceService);
        var workspaceImportExecutionGate = new WorkspaceImportExecutionGate();
        var workspaceImportExecutionOptionsResolver = new WorkspaceImportExecutionOptionsResolver(
            () => appPreferencesService.WorkspaceImportWorkerCount);
        var assetBundleSourceInspector = new UnityAssetBundleSourceInspector();
        var assetBundleReader = new FallbackAssetBundleReader(
            new AssetsToolsAssetBundleReader(),
            new HeuristicAssetBundleReader());
        var audioPayloadTranscodingService = new AudioPayloadTranscodingService();
        var assetPreviewGenerator = new AssetsToolsAssetPreviewGenerator(audioPayloadTranscodingService);

        var openWorkspaceHandler = new OpenWorkspaceHandler(
            workspaceSessionRegistry: workspaceSessionRegistry,
            workspaceMetadataStoreFactory: workspaceMetadataStoreFactory,
            workspaceDataStoreFactory: workspaceDataStoreFactory,
            workspaceImportSourceDiscoveryService: workspaceImportSourceDiscoveryService,
            workspaceImportFileSystemService: workspaceImportFileSystemService,
            workspaceImportExecutionGate: workspaceImportExecutionGate,
            workspaceImportExecutionOptionsResolver: workspaceImportExecutionOptionsResolver,
            assetBundleSourceInspector: assetBundleSourceInspector,
            assetBundleDecryptorRegistry: assetBundleDecryptorRegistry,
            assetBundleReader: assetBundleReader);
        var registerWorkspaceImportHandler = new RegisterWorkspaceImportHandler(
            workspaceMetadataStoreFactory,
            workspaceImportFileSystemService);
        var processWorkspaceImportsHandler = new ProcessWorkspaceImportsHandler(
            workspaceMetadataStoreFactory,
            workspaceDataStoreFactory,
            workspaceImportSourceDiscoveryService,
            workspaceImportFileSystemService,
            workspaceImportExecutionGate,
            workspaceImportExecutionOptionsResolver,
            assetBundleSourceInspector,
            assetBundleDecryptorRegistry,
            assetBundleReader);
        var removeWorkspaceBundleHandler = new RemoveWorkspaceBundleHandler(
            workspaceMetadataStoreFactory,
            workspaceDataStoreFactory);
        var loadBundleTreeHandler = new LoadBundleTreeHandler(workspaceSessionRegistry);
        var loadAssetListHandler = new LoadAssetListHandler(workspaceSessionRegistry);
        var getAssetDetailsHandler = new GetAssetDetailsHandler(workspaceSessionRegistry);
        var getAssetPreviewHandler = new GetAssetPreviewHandler(
            workspaceSessionRegistry,
            workspaceDataStoreFactory,
            assetBundleDecryptorRegistry,
            assetPreviewGenerator,
            audioPayloadTranscodingService);
        var getWorkspaceChangeSummaryHandler = new GetWorkspaceChangeSummaryHandler(workspaceSessionRegistry);
        var getWorkspaceSavePreviewHandler = new GetWorkspaceSavePreviewHandler(workspaceSessionRegistry);
        var saveWorkspaceAsNewBundleHandler = new SaveWorkspaceAsNewBundleHandler(workspaceSessionRegistry);
        var extractAssetHandler = new ExtractAssetHandler(workspaceSessionRegistry);
        var getWorkspaceBatchExtractionPreviewHandler = new GetWorkspaceBatchExtractionPreviewHandler(workspaceSessionRegistry);
        var extractWorkspaceAssetsHandler = new ExtractWorkspaceAssetsHandler(workspaceSessionRegistry);
        var getWorkspaceBatchReplacementPreviewHandler = new GetWorkspaceBatchReplacementPreviewHandler(workspaceSessionRegistry);
        var executeWorkspaceBatchReplacementHandler = new ExecuteWorkspaceBatchReplacementHandler(workspaceSessionRegistry);
        var getFileReplacementCandidateHandler = new GetFileReplacementCandidateHandler(workspaceSessionRegistry);
        var getWorkspaceReplacementCandidatesHandler = new GetWorkspaceReplacementCandidatesHandler(workspaceSessionRegistry);
        var replaceAssetFromFileHandler = new ReplaceAssetFromFileHandler(workspaceSessionRegistry);
        var replaceAssetFromWorkspaceHandler = new ReplaceAssetFromWorkspaceHandler(workspaceSessionRegistry);
        var undoAssetReplacementHandler = new UndoAssetReplacementHandler(workspaceSessionRegistry);
        var openAssetInPaneHandler = new OpenAssetInPaneHandler(workspaceSessionRegistry);
        var getWorkspaceCatalogSnapshotHandler = new GetWorkspaceCatalogSnapshotHandler(workspaceCatalogStore);
        var registerWorkspaceCatalogEntryHandler = new RegisterWorkspaceCatalogEntryHandler(workspaceCatalogStore);
        var removeWorkspaceCatalogEntryHandler = new RemoveWorkspaceCatalogEntryHandler(
            workspaceCatalogStore,
            workspaceMetadataStoreFactory,
            workspaceDataStoreFactory);
        var setWorkspaceCatalogEntryPinnedStateHandler = new SetWorkspaceCatalogEntryPinnedStateHandler(workspaceCatalogStore);
        var getAssetBundleDecryptorCatalogEntriesHandler = new GetAssetBundleDecryptorCatalogEntriesHandler(assetBundleDecryptorCatalogStore);
        var upsertAssetBundleDecryptorHandler = new UpsertAssetBundleDecryptorHandler(assetBundleDecryptorCatalogStore);
        var deleteAssetBundleDecryptorHandler = new DeleteAssetBundleDecryptorHandler(assetBundleDecryptorCatalogStore);
        var assetEditDraftStore = new WorkspaceChangeSetAssetEditDraftStore(
            workspaceCatalogStore,
            workspaceDataStoreFactory);
        var getAssetEditDraftRestorePlanHandler = new GetAssetEditDraftRestorePlanHandler(
            assetEditDraftStore);

        var workspaceExplorerGateway = new WorkspaceExplorerGateway(
            openWorkspaceHandler: openWorkspaceHandler,
            registerWorkspaceImportHandler: registerWorkspaceImportHandler,
            processWorkspaceImportsHandler: processWorkspaceImportsHandler,
            removeWorkspaceBundleHandler: removeWorkspaceBundleHandler,
            loadBundleTreeHandler: loadBundleTreeHandler,
            loadAssetListHandler: loadAssetListHandler);
        var workspaceAssetInspectionGateway = new WorkspaceAssetInspectionGateway(
            getAssetDetailsHandler: getAssetDetailsHandler,
            getAssetPreviewHandler: getAssetPreviewHandler,
            openAssetInPaneHandler: openAssetInPaneHandler);
        var workspaceAssetOperationGateway = new WorkspaceAssetOperationGateway(
            extractAssetHandler: extractAssetHandler,
            getFileReplacementCandidateHandler: getFileReplacementCandidateHandler,
            getWorkspaceReplacementCandidatesHandler: getWorkspaceReplacementCandidatesHandler,
            replaceAssetFromFileHandler: replaceAssetFromFileHandler,
            replaceAssetFromWorkspaceHandler: replaceAssetFromWorkspaceHandler,
            undoAssetReplacementHandler: undoAssetReplacementHandler);
        var workspaceBatchOperationsGateway = new WorkspaceBatchOperationsGateway(
            getWorkspaceChangeSummaryHandler: getWorkspaceChangeSummaryHandler,
            getWorkspaceSavePreviewHandler: getWorkspaceSavePreviewHandler,
            saveWorkspaceAsNewBundleHandler: saveWorkspaceAsNewBundleHandler,
            getWorkspaceBatchExtractionPreviewHandler: getWorkspaceBatchExtractionPreviewHandler,
            extractWorkspaceAssetsHandler: extractWorkspaceAssetsHandler,
            getWorkspaceBatchReplacementPreviewHandler: getWorkspaceBatchReplacementPreviewHandler,
            executeWorkspaceBatchReplacementHandler: executeWorkspaceBatchReplacementHandler);
        var workspaceCatalogGateway = new WorkspaceCatalogGateway(
            getWorkspaceCatalogSnapshotHandler: getWorkspaceCatalogSnapshotHandler,
            registerWorkspaceCatalogEntryHandler: registerWorkspaceCatalogEntryHandler,
            removeWorkspaceCatalogEntryHandler: removeWorkspaceCatalogEntryHandler,
            setWorkspaceCatalogEntryPinnedStateHandler: setWorkspaceCatalogEntryPinnedStateHandler);
        var assetEditDraftRestorePlanGateway = new AssetEditDraftRestorePlanGateway(
            getAssetEditDraftRestorePlanHandler);

        var workspaceContextService = new WorkspaceContextService(workspaceContextCatalogService);
        var workspaceProcessCoordinatorService = new WorkspaceProcessCoordinatorService(localizationService);
        workspaceProcessCoordinatorService.WorkspaceActivationRequested += context.WorkspaceActivationRequestedHandler;
        var filePickerService = new WinUIFilePickerService(context.ActiveWindowAccessor, localizationService);
        var pathNavigationService = new WindowsExplorerPathNavigationService();
        var assetBundleDecryptorCatalogService = new AssetBundleDecryptorCatalogService(
            getEntriesHandler: getAssetBundleDecryptorCatalogEntriesHandler,
            upsertHandler: upsertAssetBundleDecryptorHandler,
            deleteHandler: deleteAssetBundleDecryptorHandler,
            onCatalogChanged: reloadAssetBundleDecryptorRegistry);
        var appInteractionFeedbackService = new AppInteractionFeedbackService();
        var workspaceDialogService = new WorkspaceDialogService(
            localizationService,
            workspaceDataRootService,
            appInteractionFeedbackService,
            assetBundleDecryptorRegistry);
        var workspaceWindowService = new WorkspaceWindowService(workspaceProcessCoordinatorService);
        var appNotificationService = new AppNotificationService();
        var appLogService = new AppLogService(localizationService);
        var backgroundTaskRunner = new ThreadPoolBackgroundTaskRunner();
        var workspaceImportCoordinatorService = new WorkspaceImportCoordinatorService(
            workspaceContextService,
            workspaceExplorerGateway,
            filePickerService,
            appLogService,
            appNotificationService,
            localizationService,
            backgroundTaskRunner);

        StartupDiagnostics.Log("Composition root services created.");

        return new AppCompositionServices(
            LocalizationService: localizationService,
            AppPreferencesService: appPreferencesService,
            AssetBundleDecryptorRegistry: assetBundleDecryptorRegistry,
            ReloadAssetBundleDecryptorRegistry: reloadAssetBundleDecryptorRegistry,
            WorkspaceExplorerGateway: workspaceExplorerGateway,
            WorkspaceAssetInspectionGateway: workspaceAssetInspectionGateway,
            WorkspaceAssetOperationGateway: workspaceAssetOperationGateway,
            WorkspaceBatchOperationsGateway: workspaceBatchOperationsGateway,
            AssetEditDraftRestorePlanGateway: assetEditDraftRestorePlanGateway,
            WorkspaceContextService: workspaceContextService,
            WorkspaceProcessCoordinatorService: workspaceProcessCoordinatorService,
            FilePickerService: filePickerService,
            PathNavigationService: pathNavigationService,
            WorkspaceCatalogGateway: workspaceCatalogGateway,
            AssetBundleDecryptorCatalogService: assetBundleDecryptorCatalogService,
            WorkspaceDialogService: workspaceDialogService,
            AppInteractionFeedbackService: appInteractionFeedbackService,
            WorkspaceWindowService: workspaceWindowService,
            AppNotificationService: appNotificationService,
            AppLogService: appLogService,
            BackgroundTaskRunner: backgroundTaskRunner,
            WorkspaceImportCoordinatorService: workspaceImportCoordinatorService);
    }
}
