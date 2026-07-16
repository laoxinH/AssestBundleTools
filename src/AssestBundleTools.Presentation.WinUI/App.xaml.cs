using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Composition;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Services;
using System.Diagnostics;

using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;
namespace AssestBundleTools.Presentation.WinUI;

/// <summary>
/// 应用入口，负责初始化全局级别的 UI 资源和主窗口。
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly List<MainWindow> _windows = [];
    private readonly List<Microsoft.UI.Xaml.Window> _activeOwnerWindows = [];
    private readonly Dictionary<Microsoft.UI.Xaml.Window, MainWindow> _auxiliaryWindowOwners = [];
    private MainWindow? _activeWindow;
    private Microsoft.UI.Xaml.Window? _activeOwnerWindow;
    private IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;
    private readonly Action _reloadAssetBundleDecryptorRegistry;

    public App()
    {
        StartupDiagnostics.Log("App constructor started.");

        try
        {
            var services = AppCompositionRoot.Build(new AppCompositionContext(
                ActiveWindowAccessor: ResolveActiveOwnerWindow,
                WorkspaceActivationRequestedHandler: OnWorkspaceProcessActivationRequested));

            LocalizationService = services.LocalizationService;
            AppPreferencesService = services.AppPreferencesService;
            _assetBundleDecryptorRegistry = services.AssetBundleDecryptorRegistry;
            _reloadAssetBundleDecryptorRegistry = services.ReloadAssetBundleDecryptorRegistry;
            WorkspaceExplorerGateway = services.WorkspaceExplorerGateway;
            WorkspaceAssetInspectionGateway = services.WorkspaceAssetInspectionGateway;
            WorkspaceAssetOperationGateway = services.WorkspaceAssetOperationGateway;
            WorkspaceBatchOperationsGateway = services.WorkspaceBatchOperationsGateway;
            WorkspaceOperationChangeNotifier = services.WorkspaceOperationChangeNotifier;
            AssetEditDraftRestorePlanGateway = services.AssetEditDraftRestorePlanGateway;
            WorkspaceContextService = services.WorkspaceContextService;
            WorkspaceProcessCoordinatorService = services.WorkspaceProcessCoordinatorService;
            ElevatedDragDropCompatibilityService = services.ElevatedDragDropCompatibilityService;
            ElevatedFileDropRouter = services.ElevatedFileDropRouter;
            ElevatedDragExportBrokerService = services.ElevatedDragExportBrokerService;
            ElevationRestartPromptCoordinator = services.ElevationRestartPromptCoordinator;
            FilePickerService = services.FilePickerService;
            PathNavigationService = services.PathNavigationService;
            WorkspaceCatalogGateway = services.WorkspaceCatalogGateway;
            WorkspaceCacheMaintenanceGateway = services.WorkspaceCacheMaintenanceGateway;
            AssetBundleDecryptorCatalogService = services.AssetBundleDecryptorCatalogService;
            ResourceProcessorCatalogService = services.ResourceProcessorCatalogService;
            WorkspaceDialogService = services.WorkspaceDialogService;
            AppInteractionFeedbackService = services.AppInteractionFeedbackService;
            WorkspaceWindowService = services.WorkspaceWindowService;
            AppNotificationService = services.AppNotificationService;
            AppLogService = services.AppLogService;
            AppBackgroundTaskStatusService = services.AppBackgroundTaskStatusService;
            BackgroundTaskRunner = services.BackgroundTaskRunner;
            BackgroundWorkScheduler = services.BackgroundWorkScheduler;
            WorkspaceImportCoordinatorService = services.WorkspaceImportCoordinatorService;
            SpineAnimationRuntimePlaybackService = services.SpineAnimationRuntimePlaybackService;

            RegisterGlobalExceptionHandlers();
            StartupDiagnostics.Log("Global exception handlers registered.");

            StartupDiagnostics.Log("Calling InitializeComponent.");
            InitializeComponent();
            StartupDiagnostics.Log("App.InitializeComponent completed.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"App constructor failed with exception: {ex}");
            throw;
        }
    }

    public static App CurrentApp => (App)Current;

    public ILocalizationService LocalizationService { get; }

    public IAppPreferencesService AppPreferencesService { get; }

    public IAssetBundleDecryptorRegistry AssetBundleDecryptorRegistry => _assetBundleDecryptorRegistry;

    public IWorkspaceExplorerGateway WorkspaceExplorerGateway { get; }

    public IWorkspaceAssetInspectionGateway WorkspaceAssetInspectionGateway { get; }

    public IWorkspaceAssetOperationGateway WorkspaceAssetOperationGateway { get; }

    public IWorkspaceBatchOperationsGateway WorkspaceBatchOperationsGateway { get; }

    public IWorkspaceOperationChangeNotifier WorkspaceOperationChangeNotifier { get; }

    public IAssetEditDraftRestorePlanGateway AssetEditDraftRestorePlanGateway { get; }

    public IWorkspaceContextService WorkspaceContextService { get; }

    public IWorkspaceProcessCoordinatorService WorkspaceProcessCoordinatorService { get; }

    internal IElevatedDragDropCompatibilityService ElevatedDragDropCompatibilityService { get; }

    internal IElevatedFileDropRouter ElevatedFileDropRouter { get; }

    internal IElevatedDragExportBrokerService ElevatedDragExportBrokerService { get; }

    internal ElevationRestartPromptCoordinator ElevationRestartPromptCoordinator { get; }

    public IFilePickerService FilePickerService { get; }

    public IPathNavigationService PathNavigationService { get; }

    public IWorkspaceCatalogGateway WorkspaceCatalogGateway { get; }

    public IWorkspaceCacheMaintenanceGateway WorkspaceCacheMaintenanceGateway { get; }

    public IAssetBundleDecryptorCatalogService AssetBundleDecryptorCatalogService { get; }

    public IResourceProcessorCatalogService ResourceProcessorCatalogService { get; }

    public IWorkspaceDialogService WorkspaceDialogService { get; }

    public IAppInteractionFeedbackService AppInteractionFeedbackService { get; }

    public IWorkspaceWindowService WorkspaceWindowService { get; }

    public IAppNotificationService AppNotificationService { get; }

    public IAppLogService AppLogService { get; }

    public IAppBackgroundTaskStatusService AppBackgroundTaskStatusService { get; }

    public IBackgroundTaskRunner BackgroundTaskRunner { get; }

    public IBackgroundWorkScheduler BackgroundWorkScheduler { get; }

    public IWorkspaceImportCoordinatorService WorkspaceImportCoordinatorService { get; }

    public ISpineAnimationRuntimePlaybackService SpineAnimationRuntimePlaybackService { get; }

    public void ReloadAssetBundleDecryptorRegistry()
    {
        StartupDiagnostics.Log("Reloading asset bundle decryptor registry.");
        _reloadAssetBundleDecryptorRegistry();
        StartupDiagnostics.Log("Asset bundle decryptor registry reloaded.");
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupDiagnostics.Log("OnLaunched started.");

        try
        {
            if (ElevatedDragExportBrokerHost.TryRunCurrentProcess())
            {
                StartupDiagnostics.Log("Elevated drag-export broker host completed.");
                Exit();
                return;
            }

            var launchRequest = ResolveInitialLaunchRequest();
            if (!ElevationRestartPromptCoordinator.IsUnelevatedRestartLaunch &&
                WorkspaceProcessCoordinatorService.TryRedirectLaunchToExistingProcess(launchRequest))
            {
                StartupDiagnostics.Log("Launch redirected to an existing workspace process.");
                Exit();
                return;
            }

            var window = CreateMainWindow(launchRequest);
            StartupDiagnostics.Log("MainWindow created.");

            window.Activate();
            StartupDiagnostics.Log("MainWindow activated.");
            _ = HandleElevationRestartPromptAsync(window, launchRequest);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"OnLaunched failed: {ex}");
            throw;
        }
    }

    private async Task HandleElevationRestartPromptAsync(
        MainWindow window,
        ShellLaunchRequest? launchRequest)
    {
        var restartStarted = await ElevationRestartPromptCoordinator.PromptAndRestartIfRequestedAsync(
            window,
            launchRequest);
        if (!restartStarted)
        {
            return;
        }

        WorkspaceProcessCoordinatorService.ClearWorkspaceRegistration();
        Exit();
    }

    public MainWindow CreateMainWindow(ShellLaunchRequest? launchRequest = null)
    {
        var window = new MainWindow(LocalizationService, launchRequest);
        RegisterWindow(window);
        return window;
    }

    public void RegisterAuxiliaryWindow(Microsoft.UI.Xaml.Window window)
    {
        RegisterAuxiliaryWindow(window, ownerWindow: null);
    }

    public void RegisterAuxiliaryWindow(Microsoft.UI.Xaml.Window window, MainWindow? ownerWindow)
    {
        ArgumentNullException.ThrowIfNull(window);
        RegisterOwnerWindow(window);
        if (ownerWindow is not null)
        {
            _auxiliaryWindowOwners[window] = ownerWindow;
        }
    }

    /// <summary>
    /// 注册 Presentation 层的全局异常兜底。
    /// 当前阶段先将异常输出到调试器，便于 VS 初步联调时快速定位启动问题。
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        try
        {
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Failed to register global exception handlers: {ex}");
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.Log($"UI thread unhandled exception: {e.Exception}");
        Debug.WriteLine($"UI thread unhandled exception: {e.Exception}");
    }

    private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        StartupDiagnostics.Log($"AppDomain unhandled exception: {e.ExceptionObject}");
        Debug.WriteLine($"AppDomain unhandled exception: {e.ExceptionObject}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupDiagnostics.Log($"Unobserved task exception: {e.Exception}");
        Debug.WriteLine($"Unobserved task exception: {e.Exception}");
    }
}

