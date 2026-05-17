using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.Composition;

/// <summary>
/// 承载 WinUI 组合根创建出的应用服务图。
/// </summary>
internal sealed record AppCompositionServices(
    ILocalizationService LocalizationService,
    IAppPreferencesService AppPreferencesService,
    IAssetBundleDecryptorRegistry AssetBundleDecryptorRegistry,
    Action ReloadAssetBundleDecryptorRegistry,
    IWorkspaceExplorerGateway WorkspaceExplorerGateway,
    IWorkspaceAssetInspectionGateway WorkspaceAssetInspectionGateway,
    IWorkspaceAssetOperationGateway WorkspaceAssetOperationGateway,
    IWorkspaceBatchOperationsGateway WorkspaceBatchOperationsGateway,
    IWorkspaceOperationChangeNotifier WorkspaceOperationChangeNotifier,
    IAssetEditDraftRestorePlanGateway AssetEditDraftRestorePlanGateway,
    IWorkspaceContextService WorkspaceContextService,
    IWorkspaceProcessCoordinatorService WorkspaceProcessCoordinatorService,
    IFilePickerService FilePickerService,
    IPathNavigationService PathNavigationService,
    IWorkspaceCatalogGateway WorkspaceCatalogGateway,
    IWorkspaceCacheMaintenanceGateway WorkspaceCacheMaintenanceGateway,
    IAssetBundleDecryptorCatalogService AssetBundleDecryptorCatalogService,
    IWorkspaceDialogService WorkspaceDialogService,
    IAppInteractionFeedbackService AppInteractionFeedbackService,
    IWorkspaceWindowService WorkspaceWindowService,
    IAppNotificationService AppNotificationService,
    IAppLogService AppLogService,
    IAppBackgroundTaskStatusService AppBackgroundTaskStatusService,
    IBackgroundTaskRunner BackgroundTaskRunner,
    IBackgroundWorkScheduler BackgroundWorkScheduler,
    IWorkspaceImportCoordinatorService WorkspaceImportCoordinatorService,
    ISpineAnimationRuntimePlaybackService SpineAnimationRuntimePlaybackService);
