using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Queries;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Infrastructure.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Bundles.Processing;
using AssestBundleTools.Infrastructure.Bundles.Previewing;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Settings.Persistence;
using AssestBundleTools.Infrastructure.Workspace.Extraction;
using AssestBundleTools.Infrastructure.Workspace.Importing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;
using AssestBundleTools.Infrastructure.Workspace.Replacement;
using AssestBundleTools.Infrastructure.Workspace.Runtime;
using AssestBundleTools.Infrastructure.Workspace.Saving;
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
        var localizationService = new ResourceLocalizationService();
        var appPreferencesService = new AppPreferencesService(appPreferencesStore);
        var workspaceCatalogStore = new JsonWorkspaceCatalogStore();
        var workspaceMetadataStoreFactory = new JsonWorkspaceMetadataStoreFactory();
        var workspaceDataStoreFactory = new JsonWorkspaceDataStoreFactory();
        var workspaceOperationLogService = new WorkspaceOperationLogService(
            () => appPreferencesService.OperationRecordLimit);
        var workspaceSessionRegistry = new CachedWorkspaceSessionRegistry(
            workspaceDataStoreFactory,
            workspaceOperationLogService);
        var workspaceDataRootService = new JsonWorkspaceDataRootService(
            workspaceMetadataStoreFactory,
            workspaceDataStoreFactory);

        var bundleArtifactCachePrunePolicy = new PreferencesBundleArtifactCachePrunePolicy(appPreferencesService);

        var assetBundleDecryptorRegistry = new ReloadableAssetBundleDecryptorRegistry();
        Action reloadAssetBundleDecryptorRegistry = () => assetBundleDecryptorRegistry.Reload();
        var assetBundleDecryptorCatalogStore = new FileSystemAssetBundleDecryptorCatalogStore();
        var resourceProcessorCatalogStore = new FileSystemResourceProcessorCatalogStore();

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
        var replacementPreviewSourceProvider = new FileSystemReplacementPreviewSourceProvider();
        var bundleArtifactSessionInvalidator = new AssetsToolsBundleArtifactSessionInvalidator();
        var spineAnimationRuntimePreviewService = new SpineAnimationRuntimePreviewService();
        var workspaceSaveOutputProbe = new FileSystemWorkspaceSaveOutputProbe();
        var workspaceBundleExporter = new FileSystemWorkspaceBundleExporter();
        var workspaceBundleExportManifestStore = new FileSystemWorkspaceBundleExportManifestStore();
        var workspaceResourceProcessorExecutor = new FileSystemResourceProcessorExecutor();

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
            assetBundleReader: assetBundleReader,
            bundleArtifactSessionInvalidator: bundleArtifactSessionInvalidator,
            bundleArtifactCachePrunePolicy: bundleArtifactCachePrunePolicy);
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
            assetBundleReader,
            bundleArtifactSessionInvalidator,
            bundleArtifactCachePrunePolicy);
        var removeWorkspaceBundleHandler = new RemoveWorkspaceBundleHandler(
            workspaceMetadataStoreFactory,
            workspaceDataStoreFactory,
            bundleArtifactSessionInvalidator);
        var loadBundleTreeHandler = new LoadBundleTreeHandler(workspaceSessionRegistry);
        var loadAssetListHandler = new LoadAssetListHandler(workspaceSessionRegistry);
        var loadSpineAssetGroupsHandler = new LoadSpineAssetGroupsHandler(
            workspaceSessionRegistry,
            workspaceDataStoreFactory);
        var getAssetDetailsHandler = new GetAssetDetailsHandler(workspaceSessionRegistry);
        var getAssetPreviewHandler = new GetAssetPreviewHandler(
            workspaceSessionRegistry,
            workspaceDataStoreFactory,
            assetBundleDecryptorRegistry,
            assetPreviewGenerator,
            audioPayloadTranscodingService,
            bundleArtifactSessionInvalidator,
            bundleArtifactCachePrunePolicy,
            bundleArtifactCachePrunePolicy,
            replacementPreviewSourceProvider);
        var loadPreviewTextChunkHandler = new LoadPreviewTextChunkHandler(
            workspaceDataStoreFactory,
            replacementPreviewSourceProvider);
        var loadSpineAssetGroupDetailsHandler = new LoadSpineAssetGroupDetailsHandler(
            workspaceSessionRegistry,
            getAssetPreviewHandler,
            spineAnimationRuntimePreviewService);
        var getWorkspaceChangeSummaryHandler = new GetWorkspaceChangeSummaryHandler(workspaceSessionRegistry);
        var getWorkspaceSavePreviewHandler = new GetWorkspaceSavePreviewHandler(
            workspaceSessionRegistry,
            workspaceDataStoreFactory,
            workspaceSaveOutputProbe);
        var saveWorkspaceAsNewBundleHandler = new SaveWorkspaceAsNewBundleHandler(
            workspaceSessionRegistry,
            workspaceDataStoreFactory,
            workspaceSaveOutputProbe,
            workspaceBundleExporter,
            workspaceBundleExportManifestStore,
            resourceProcessorCatalogStore,
            workspaceResourceProcessorExecutor);
        var assetExtractionFileStore = new FileSystemAssetExtractionFileStore();
        var workspaceExportManifestStore = new FileSystemWorkspaceExportManifestStore();
        var workspaceBatchReplacementSourceFileProbe = new FileSystemWorkspaceBatchReplacementSourceFileProbe();
        var workspaceAssetExtractionExporter = new WorkspaceAssetExtractionExporter(
            getAssetPreviewHandler,
            assetExtractionFileStore);
        var workspaceExtractionOperationRecorder = new WorkspaceExtractionOperationRecorder(
            workspaceDataStoreFactory,
            workspaceOperationLogService);
        var extractAssetHandler = new ExtractAssetHandler(
            workspaceSessionRegistry,
            workspaceAssetExtractionExporter,
            workspaceExtractionOperationRecorder);
        var getWorkspaceBatchExtractionPreviewHandler = new GetWorkspaceBatchExtractionPreviewHandler(
            workspaceSessionRegistry,
            loadSpineAssetGroupsHandler);
        var extractSpineAssetGroupHandler = new ExtractSpineAssetGroupHandler(
            workspaceSessionRegistry,
            loadSpineAssetGroupDetailsHandler,
            extractAssetHandler,
            workspaceExtractionOperationRecorder,
            loadSpineAssetGroupsHandler);
        var extractWorkspaceAssetsHandler = new ExtractWorkspaceAssetsHandler(
            workspaceSessionRegistry,
            extractAssetHandler,
            workspaceExtractionOperationRecorder,
            loadSpineAssetGroupsHandler,
            extractSpineAssetGroupHandler,
            workspaceExportManifestStore);
        var getWorkspaceBatchReplacementPlanHandler = new GetWorkspaceBatchReplacementPlanHandler(
            workspaceSessionRegistry,
            workspaceExportManifestStore,
            workspaceBatchReplacementSourceFileProbe,
            loadSpineAssetGroupsHandler);
        var getFileReplacementCandidateHandler = new GetFileReplacementCandidateHandler(workspaceSessionRegistry);
        var getWorkspaceReplacementCandidatesHandler = new GetWorkspaceReplacementCandidatesHandler(workspaceSessionRegistry);
        var workspaceReplacementOperationRecorder = new WorkspaceReplacementOperationRecorder(
            workspaceDataStoreFactory,
            workspaceOperationLogService);
        var replaceAssetFromFileHandler = new ReplaceAssetFromFileHandler(
            workspaceSessionRegistry,
            workspaceReplacementOperationRecorder);
        var executeWorkspaceBatchReplacementHandler = new ExecuteWorkspaceBatchReplacementHandler(
            workspaceSessionRegistry,
            replaceAssetFromFileHandler,
            workspaceBatchReplacementSourceFileProbe);
        var replaceAssetFromWorkspaceHandler = new ReplaceAssetFromWorkspaceHandler(
            workspaceSessionRegistry,
            workspaceReplacementOperationRecorder);
        var undoAssetReplacementHandler = new UndoAssetReplacementHandler(
            workspaceSessionRegistry,
            workspaceReplacementOperationRecorder,
            workspaceSessionRegistry);
        var redoAssetReplacementHandler = new RedoAssetReplacementHandler(
            workspaceSessionRegistry,
            workspaceReplacementOperationRecorder,
            workspaceSessionRegistry);
        var workspaceSpriteEditOperationRecorder = new WorkspaceSpriteEditOperationRecorder(
            workspaceDataStoreFactory,
            workspaceOperationLogService);
        var applySpriteEditHandler = new ApplySpriteEditHandler(
            workspaceSessionRegistry,
            workspaceSessionRegistry,
            workspaceSpriteEditOperationRecorder);
        var deleteWorkspaceOperationRecordHandler = new DeleteWorkspaceOperationRecordHandler(
            workspaceSessionRegistry,
            workspaceDataStoreFactory,
            workspaceOperationLogService);
        var openAssetInPaneHandler = new OpenAssetInPaneHandler(workspaceSessionRegistry);
        var getWorkspaceCatalogSnapshotHandler = new GetWorkspaceCatalogSnapshotHandler(workspaceCatalogStore);
        var registerWorkspaceCatalogEntryHandler = new RegisterWorkspaceCatalogEntryHandler(workspaceCatalogStore);
        var removeWorkspaceCatalogEntryHandler = new RemoveWorkspaceCatalogEntryHandler(
            workspaceCatalogStore,
            workspaceMetadataStoreFactory,
            workspaceDataStoreFactory,
            bundleArtifactSessionInvalidator);
        var setWorkspaceCatalogEntryPinnedStateHandler = new SetWorkspaceCatalogEntryPinnedStateHandler(workspaceCatalogStore);
        var getWorkspaceCacheUsageHandler = new GetWorkspaceCacheUsageHandler(workspaceDataStoreFactory);
        var clearWorkspaceCacheHandler = new ClearWorkspaceCacheHandler(
            workspaceDataStoreFactory,
            bundleArtifactSessionInvalidator);
        var getBundleArtifactCacheUsageHandler = new GetBundleArtifactCacheUsageHandler(workspaceDataStoreFactory);
        var pruneBundleArtifactCacheHandler = new PruneBundleArtifactCacheHandler(
            workspaceDataStoreFactory,
            bundleArtifactSessionInvalidator);
        var getAssetBundleDecryptorCatalogEntriesHandler = new GetAssetBundleDecryptorCatalogEntriesHandler(assetBundleDecryptorCatalogStore);
        var upsertAssetBundleDecryptorHandler = new UpsertAssetBundleDecryptorHandler(assetBundleDecryptorCatalogStore);
        var deleteAssetBundleDecryptorHandler = new DeleteAssetBundleDecryptorHandler(assetBundleDecryptorCatalogStore);
        var getResourceProcessorCatalogEntriesHandler = new GetResourceProcessorCatalogEntriesHandler(resourceProcessorCatalogStore);
        var upsertResourceProcessorHandler = new UpsertResourceProcessorHandler(resourceProcessorCatalogStore);
        var deleteResourceProcessorHandler = new DeleteResourceProcessorHandler(resourceProcessorCatalogStore);
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
            loadAssetListHandler: loadAssetListHandler,
            loadSpineAssetGroupsHandler: loadSpineAssetGroupsHandler);
        var workspaceAssetInspectionGateway = new WorkspaceAssetInspectionGateway(
            getAssetDetailsHandler: getAssetDetailsHandler,
            getAssetPreviewHandler: getAssetPreviewHandler,
            loadPreviewTextChunkHandler: loadPreviewTextChunkHandler,
            loadSpineAssetGroupDetailsHandler: loadSpineAssetGroupDetailsHandler,
            openAssetInPaneHandler: openAssetInPaneHandler);
        var workspaceOperationChangeNotifier = new WorkspaceOperationChangeNotifier();
        var workspaceAssetOperationGatewayCore = new WorkspaceAssetOperationGateway(
            extractAssetHandler: extractAssetHandler,
            extractSpineAssetGroupHandler: extractSpineAssetGroupHandler,
            getFileReplacementCandidateHandler: getFileReplacementCandidateHandler,
            getWorkspaceReplacementCandidatesHandler: getWorkspaceReplacementCandidatesHandler,
            replaceAssetFromFileHandler: replaceAssetFromFileHandler,
            replaceAssetFromWorkspaceHandler: replaceAssetFromWorkspaceHandler,
            undoAssetReplacementHandler: undoAssetReplacementHandler,
            redoAssetReplacementHandler: redoAssetReplacementHandler,
            applySpriteEditHandler: applySpriteEditHandler,
            deleteWorkspaceOperationRecordHandler: deleteWorkspaceOperationRecordHandler);
        var workspaceAssetOperationGateway = new NotifyingWorkspaceAssetOperationGateway(
            workspaceAssetOperationGatewayCore,
            workspaceOperationChangeNotifier,
            workspaceSessionRegistry);
        var workspaceBatchOperationsGatewayCore = new WorkspaceBatchOperationsGateway(
            getWorkspaceChangeSummaryHandler: getWorkspaceChangeSummaryHandler,
            getWorkspaceSavePreviewHandler: getWorkspaceSavePreviewHandler,
            saveWorkspaceAsNewBundleHandler: saveWorkspaceAsNewBundleHandler,
            getWorkspaceBatchExtractionPreviewHandler: getWorkspaceBatchExtractionPreviewHandler,
            extractWorkspaceAssetsHandler: extractWorkspaceAssetsHandler,
            getWorkspaceBatchReplacementPlanHandler: getWorkspaceBatchReplacementPlanHandler,
            executeWorkspaceBatchReplacementHandler: executeWorkspaceBatchReplacementHandler);
        var workspaceBatchOperationsGateway = new NotifyingWorkspaceBatchOperationsGateway(
            workspaceBatchOperationsGatewayCore,
            workspaceOperationChangeNotifier,
            workspaceSessionRegistry);
        var workspaceCatalogGateway = new WorkspaceCatalogGateway(
            getWorkspaceCatalogSnapshotHandler: getWorkspaceCatalogSnapshotHandler,
            registerWorkspaceCatalogEntryHandler: registerWorkspaceCatalogEntryHandler,
            removeWorkspaceCatalogEntryHandler: removeWorkspaceCatalogEntryHandler,
            setWorkspaceCatalogEntryPinnedStateHandler: setWorkspaceCatalogEntryPinnedStateHandler);
        var workspaceCacheMaintenanceGateway = new WorkspaceCacheMaintenanceGateway(
            getWorkspaceCacheUsageHandler,
            clearWorkspaceCacheHandler,
            getBundleArtifactCacheUsageHandler,
            pruneBundleArtifactCacheHandler);
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
        var resourceProcessorCatalogService = new ResourceProcessorCatalogService(
            getResourceProcessorCatalogEntriesHandler,
            upsertResourceProcessorHandler,
            deleteResourceProcessorHandler);
        var appInteractionFeedbackService = new AppInteractionFeedbackService();
        var workspaceDialogService = new WorkspaceDialogService(
            localizationService,
            workspaceDataRootService,
            appInteractionFeedbackService,
            assetBundleDecryptorRegistry);
        var workspaceWindowService = new WorkspaceWindowService(workspaceProcessCoordinatorService);
        var appNotificationService = new AppNotificationService();
        var appLogService = new AppLogService(localizationService);
        var appBackgroundTaskStatusService = new AppBackgroundTaskStatusService(localizationService);
        var backgroundTaskRunner = new ThreadPoolBackgroundTaskRunner();
        var backgroundWorkScheduler = new BackgroundWorkScheduler(
            backgroundTaskRunner,
            appBackgroundTaskStatusService,
            () => appPreferencesService.PreviewWorkerCount);
        var workspaceImportCoordinatorService = new WorkspaceImportCoordinatorService(
            workspaceContextService,
            workspaceExplorerGateway,
            filePickerService,
            appLogService,
            appNotificationService,
            localizationService,
            backgroundTaskRunner,
            backgroundWorkScheduler);

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
            WorkspaceOperationChangeNotifier: workspaceOperationChangeNotifier,
            AssetEditDraftRestorePlanGateway: assetEditDraftRestorePlanGateway,
            WorkspaceContextService: workspaceContextService,
            WorkspaceProcessCoordinatorService: workspaceProcessCoordinatorService,
            FilePickerService: filePickerService,
            PathNavigationService: pathNavigationService,
            WorkspaceCatalogGateway: workspaceCatalogGateway,
            WorkspaceCacheMaintenanceGateway: workspaceCacheMaintenanceGateway,
            AssetBundleDecryptorCatalogService: assetBundleDecryptorCatalogService,
            ResourceProcessorCatalogService: resourceProcessorCatalogService,
            WorkspaceDialogService: workspaceDialogService,
            AppInteractionFeedbackService: appInteractionFeedbackService,
            WorkspaceWindowService: workspaceWindowService,
            AppNotificationService: appNotificationService,
            AppLogService: appLogService,
            AppBackgroundTaskStatusService: appBackgroundTaskStatusService,
            BackgroundTaskRunner: backgroundTaskRunner,
            BackgroundWorkScheduler: backgroundWorkScheduler,
            WorkspaceImportCoordinatorService: workspaceImportCoordinatorService,
            SpineAnimationRuntimePlaybackService: spineAnimationRuntimePreviewService);
    }
}
