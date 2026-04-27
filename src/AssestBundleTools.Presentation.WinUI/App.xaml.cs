using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles.Queries;
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
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell;
using System.Diagnostics;

namespace AssestBundleTools.Presentation.WinUI;

/// <summary>
/// 应用入口，负责初始化全局级别的 UI 资源和主窗口。
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly List<MainWindow> _windows = [];
    private MainWindow? _activeWindow;
    private IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;

    public App()
    {
        StartupDiagnostics.Log("App constructor started.");

        try
        {
            var appPreferencesStore = new JsonAppPreferencesStore();
            var workspaceCatalogStore = new JsonWorkspaceCatalogStore();
            var workspaceMetadataStoreFactory = new JsonWorkspaceMetadataStoreFactory();
            var workspaceDataStoreFactory = new JsonWorkspaceDataStoreFactory();
            var workspaceSessionRegistry = new CachedWorkspaceSessionRegistry(workspaceDataStoreFactory);
            var workspaceDataRootService = new JsonWorkspaceDataRootService(
                workspaceMetadataStoreFactory,
                workspaceDataStoreFactory);

            StartupDiagnostics.Log("Creating localization service.");
            LocalizationService = new ResourceLocalizationService();
            StartupDiagnostics.Log("Localization service created.");

            StartupDiagnostics.Log("Creating app preferences service.");
            AppPreferencesService = new AppPreferencesService(appPreferencesStore);
            StartupDiagnostics.Log("App preferences service created.");

            StartupDiagnostics.Log("Creating asset bundle decryptor registry.");
            _assetBundleDecryptorRegistry = new ReloadableAssetBundleDecryptorRegistry();
            StartupDiagnostics.Log("Asset bundle decryptor registry created.");

            StartupDiagnostics.Log("Creating asset bundle decryptor catalog store.");
            var assetBundleDecryptorCatalogStore = new FileSystemAssetBundleDecryptorCatalogStore();
            StartupDiagnostics.Log("Asset bundle decryptor catalog store created.");

            StartupDiagnostics.Log("Creating application handlers.");
            var workspaceImportSourceDiscoveryService = new FileSystemWorkspaceImportSourceDiscoveryService();
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
                assetBundleSourceInspector: assetBundleSourceInspector,
                assetBundleDecryptorRegistry: _assetBundleDecryptorRegistry,
                assetBundleReader: assetBundleReader);
            var registerWorkspaceImportHandler = new RegisterWorkspaceImportHandler(workspaceMetadataStoreFactory);
            var processWorkspaceImportsHandler = new ProcessWorkspaceImportsHandler(
                workspaceMetadataStoreFactory,
                workspaceDataStoreFactory,
                workspaceImportSourceDiscoveryService,
                assetBundleSourceInspector,
                _assetBundleDecryptorRegistry,
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
                _assetBundleDecryptorRegistry,
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
            StartupDiagnostics.Log("Application handlers created.");

            StartupDiagnostics.Log("Creating workspace browser service.");
            WorkspaceBrowserService = new WorkspaceBrowserService(
                openWorkspaceHandler: openWorkspaceHandler,
                registerWorkspaceImportHandler: registerWorkspaceImportHandler,
                processWorkspaceImportsHandler: processWorkspaceImportsHandler,
                removeWorkspaceBundleHandler: removeWorkspaceBundleHandler,
                loadBundleTreeHandler: loadBundleTreeHandler,
                loadAssetListHandler: loadAssetListHandler,
                getAssetDetailsHandler: getAssetDetailsHandler,
                getAssetPreviewHandler: getAssetPreviewHandler,
                getWorkspaceChangeSummaryHandler: getWorkspaceChangeSummaryHandler,
                getWorkspaceSavePreviewHandler: getWorkspaceSavePreviewHandler,
                saveWorkspaceAsNewBundleHandler: saveWorkspaceAsNewBundleHandler,
                extractAssetHandler: extractAssetHandler,
                getWorkspaceBatchExtractionPreviewHandler: getWorkspaceBatchExtractionPreviewHandler,
                extractWorkspaceAssetsHandler: extractWorkspaceAssetsHandler,
                getWorkspaceBatchReplacementPreviewHandler: getWorkspaceBatchReplacementPreviewHandler,
                executeWorkspaceBatchReplacementHandler: executeWorkspaceBatchReplacementHandler,
                getFileReplacementCandidateHandler: getFileReplacementCandidateHandler,
                getWorkspaceReplacementCandidatesHandler: getWorkspaceReplacementCandidatesHandler,
                replaceAssetFromFileHandler: replaceAssetFromFileHandler,
                replaceAssetFromWorkspaceHandler: replaceAssetFromWorkspaceHandler,
                undoAssetReplacementHandler: undoAssetReplacementHandler,
                openAssetInPaneHandler: openAssetInPaneHandler);
            StartupDiagnostics.Log("Workspace browser service created.");

            StartupDiagnostics.Log("Creating workspace context service.");
            WorkspaceContextService = new WorkspaceContextService(
                workspaceCatalogStore: workspaceCatalogStore,
                workspaceMetadataStoreFactory: workspaceMetadataStoreFactory);
            StartupDiagnostics.Log("Workspace context service created.");

            StartupDiagnostics.Log("Creating workspace process coordinator service.");
            WorkspaceProcessCoordinatorService = new WorkspaceProcessCoordinatorService();
            WorkspaceProcessCoordinatorService.WorkspaceActivationRequested += OnWorkspaceProcessActivationRequested;
            StartupDiagnostics.Log("Workspace process coordinator service created.");

            StartupDiagnostics.Log("Creating file picker service.");
            FilePickerService = new WinUIFilePickerService(() => _activeWindow);
            StartupDiagnostics.Log("File picker service created.");

            StartupDiagnostics.Log("Creating path navigation service.");
            PathNavigationService = new WindowsExplorerPathNavigationService();
            StartupDiagnostics.Log("Path navigation service created.");

            StartupDiagnostics.Log("Creating workspace catalog service.");
            WorkspaceCatalogService = new WorkspaceCatalogService(
                getWorkspaceCatalogSnapshotHandler: getWorkspaceCatalogSnapshotHandler,
                registerWorkspaceCatalogEntryHandler: registerWorkspaceCatalogEntryHandler,
                removeWorkspaceCatalogEntryHandler: removeWorkspaceCatalogEntryHandler,
                setWorkspaceCatalogEntryPinnedStateHandler: setWorkspaceCatalogEntryPinnedStateHandler);
            StartupDiagnostics.Log("Workspace catalog service created.");

            StartupDiagnostics.Log("Creating asset bundle decryptor catalog service.");
            AssetBundleDecryptorCatalogService = new AssetBundleDecryptorCatalogService(
                getEntriesHandler: getAssetBundleDecryptorCatalogEntriesHandler,
                upsertHandler: upsertAssetBundleDecryptorHandler,
                deleteHandler: deleteAssetBundleDecryptorHandler,
                onCatalogChanged: ReloadAssetBundleDecryptorRegistry);
            StartupDiagnostics.Log("Asset bundle decryptor catalog service created.");

            StartupDiagnostics.Log("Creating app interaction feedback service.");
            AppInteractionFeedbackService = new AppInteractionFeedbackService();
            StartupDiagnostics.Log("App interaction feedback service created.");

            StartupDiagnostics.Log("Creating workspace dialog service.");
            WorkspaceDialogService = new WorkspaceDialogService(
                LocalizationService,
                workspaceDataRootService,
                AppInteractionFeedbackService,
                AssetBundleDecryptorRegistry);
            StartupDiagnostics.Log("Workspace dialog service created.");

            StartupDiagnostics.Log("Creating workspace window service.");
            WorkspaceWindowService = new WorkspaceWindowService(WorkspaceProcessCoordinatorService);
            StartupDiagnostics.Log("Workspace window service created.");

            StartupDiagnostics.Log("Creating app notification service.");
            AppNotificationService = new AppNotificationService();
            StartupDiagnostics.Log("App notification service created.");

            StartupDiagnostics.Log("Creating app log service.");
            AppLogService = new AppLogService(LocalizationService);
            StartupDiagnostics.Log("App log service created.");

            StartupDiagnostics.Log("Creating workspace import coordinator service.");
            WorkspaceImportCoordinatorService = new WorkspaceImportCoordinatorService(
                WorkspaceContextService,
                WorkspaceBrowserService,
                FilePickerService,
                AppLogService,
                AppNotificationService,
                LocalizationService);
            StartupDiagnostics.Log("Workspace import coordinator service created.");

            RegisterGlobalExceptionHandlers();
            StartupDiagnostics.Log("Global exception handlers registered.");

            StartupDiagnostics.Log("Calling InitializeComponent.");
            InitializeComponent();
            StartupDiagnostics.Log("App.InitializeComponent completed.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"App constructor failed with exception: {ex.GetType().Name}: {ex.Message}");
            StartupDiagnostics.Log($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public static App CurrentApp => (App)Current;

    public ILocalizationService LocalizationService { get; }

    public IAppPreferencesService AppPreferencesService { get; }

    public IAssetBundleDecryptorRegistry AssetBundleDecryptorRegistry => _assetBundleDecryptorRegistry;

    public IWorkspaceBrowserService WorkspaceBrowserService { get; }

    public IWorkspaceContextService WorkspaceContextService { get; }

    public IWorkspaceProcessCoordinatorService WorkspaceProcessCoordinatorService { get; }

    public IFilePickerService FilePickerService { get; }

    public IPathNavigationService PathNavigationService { get; }

    public IWorkspaceCatalogService WorkspaceCatalogService { get; }

    public IAssetBundleDecryptorCatalogService AssetBundleDecryptorCatalogService { get; }

    public IWorkspaceDialogService WorkspaceDialogService { get; }

    public IAppInteractionFeedbackService AppInteractionFeedbackService { get; }

    public IWorkspaceWindowService WorkspaceWindowService { get; }

    public IAppNotificationService AppNotificationService { get; }

    public IAppLogService AppLogService { get; }

    public IWorkspaceImportCoordinatorService WorkspaceImportCoordinatorService { get; }

    public void ReloadAssetBundleDecryptorRegistry()
    {
        StartupDiagnostics.Log("Reloading asset bundle decryptor registry.");
        if (_assetBundleDecryptorRegistry is ReloadableAssetBundleDecryptorRegistry reloadableRegistry)
        {
            reloadableRegistry.Reload();
        }
        else
        {
            _assetBundleDecryptorRegistry = new ReloadableAssetBundleDecryptorRegistry();
        }
        StartupDiagnostics.Log("Asset bundle decryptor registry reloaded.");
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupDiagnostics.Log("OnLaunched started.");

        try
        {
            var launchRequest = ResolveInitialLaunchRequest();
            if (WorkspaceProcessCoordinatorService.TryRedirectLaunchToExistingProcess(launchRequest))
            {
                StartupDiagnostics.Log("Launch redirected to an existing workspace process.");
                Exit();
                return;
            }

            var window = CreateMainWindow(launchRequest);
            StartupDiagnostics.Log("MainWindow created.");

            window.Activate();
            StartupDiagnostics.Log("MainWindow activated.");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"OnLaunched failed: {ex}");
            throw;
        }
    }

    public MainWindow CreateMainWindow(ShellLaunchRequest? launchRequest = null)
    {
        var window = new MainWindow(LocalizationService, launchRequest);
        RegisterWindow(window);
        return window;
    }

    /// <summary>
    /// 统一处理“工作区是否已经打开”的激活逻辑：
    /// 1. 先检查当前进程中的窗口是否已经承载该工作区；
    /// 2. 若没有，再尝试激活其他进程中的既有实例。
    /// 这样控制台页和工作台页都不需要各自维护一套同进程/跨进程判断。
    /// </summary>
    public bool TryActivateOpenedWorkspace(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);

        var workspaceKey = CreateWorkspaceKey(navigationRequest);
        if (!string.IsNullOrWhiteSpace(workspaceKey))
        {
            var targetWindow = FindWindowByWorkspaceKey(workspaceKey);
            if (targetWindow is not null)
            {
                return TryActivateWorkspaceWindow(targetWindow, navigationRequest);
            }
        }

        return WorkspaceProcessCoordinatorService.TryActivateExistingWorkspace(navigationRequest);
    }

    /// <summary>
    /// 当前窗口尚未承载任何工作区时，首次打开工作区应优先复用当前窗口。
    /// 这样“新建窗口”偏好不会在应用刚启动、控制台还是空白态时立刻派生第二个主进程，
    /// 更符合桌面应用首次进入工作台的直觉行为。
    /// </summary>
    public bool ShouldReuseCurrentWindowForFirstWorkspace(MainWindow? window)
    {
        return window is not null &&
               string.IsNullOrWhiteSpace(window.RegisteredWorkspaceKey);
    }

    /// <summary>
    /// 判断某个窗口当前是否已经承载了指定工作区。
    /// 该判断无副作用，可用于在弹出“打开方式询问框”之前先做短路决策。
    /// </summary>
    public bool DoesWindowHostWorkspace(
        MainWindow? window,
        WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);

        if (window is null || string.IsNullOrWhiteSpace(window.RegisteredWorkspaceKey))
        {
            return false;
        }

        var workspaceKey = CreateWorkspaceKey(navigationRequest);
        return !string.IsNullOrWhiteSpace(workspaceKey) &&
               string.Equals(window.RegisteredWorkspaceKey, workspaceKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// 页面层只在工作区真正绑定成功后回写当前窗口的承载关系，
    /// 进程级占用与跨进程激活则统一由协调服务管理。
    /// </summary>
    public string RegisterWindowWorkspace(MainWindow window, WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(navigationRequest);

        var normalizedNavigationRequest = navigationRequest with
        {
            OpenTarget = WorkspaceOpenTarget.CurrentWindow
        };
        var workspaceKey = CreateWorkspaceKey(normalizedNavigationRequest);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            WorkspaceProcessCoordinatorService.ClearWorkspaceRegistration();
            return string.Empty;
        }

        _activeWindow = window;
        WorkspaceProcessCoordinatorService.RegisterWorkspace(normalizedNavigationRequest);
        return workspaceKey;
    }

    public void UnregisterWindowWorkspace(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        WorkspaceProcessCoordinatorService.ClearWorkspaceRegistration();
    }

    private void RegisterWindow(MainWindow window)
    {
        _windows.Add(window);
        _activeWindow = window;
        window.Activated += OnWindowActivated;
        window.Closed += OnWindowClosed;
    }

    private void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
    {
        if (sender is MainWindow window)
        {
            _activeWindow = window;
        }
    }

    private void OnWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        if (sender is not MainWindow window)
        {
            return;
        }

        window.Activated -= OnWindowActivated;
        window.Closed -= OnWindowClosed;
        UnregisterWindowWorkspace(window);
        _windows.Remove(window);

        if (ReferenceEquals(_activeWindow, window))
        {
            _activeWindow = _windows.LastOrDefault();
        }
    }

    private static string CreateWorkspaceKey(WorkspaceNavigationRequest navigationRequest)
    {
        return WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            navigationRequest.SourcePath,
            navigationRequest.WorkspaceDataRootPath);
    }

    /// <summary>
    /// 启动入口统一在这里决定首屏导航目标。
    /// 这样“命令行启动 / 自动恢复上次工作区 / 默认控制台页”三类入口
    /// 都能在创建主窗口前先完成跨进程去重判断。
    /// </summary>
    private ShellLaunchRequest? ResolveInitialLaunchRequest()
    {
        if (ShellLaunchArgumentCodec.TryParseCurrentProcessLaunchRequest(out var launchRequest))
        {
            return launchRequest;
        }

        if (AppPreferencesService.RestoreLastWorkspaceOnStartup &&
            WorkspaceContextService.TryGetLastActiveWorkspaceNavigationRequest(out var workspaceNavigationRequest))
        {
            return new ShellLaunchRequest(
                NavigationKey: ShellNavigationKeys.Workbench,
                NavigationParameter: workspaceNavigationRequest);
        }

        return null;
    }

    /// <summary>
    /// 其他进程命中“该工作区已在当前进程中打开”后，
    /// 最终会通过这里把窗口带到前台并切回工作台。
    /// </summary>
    private void OnWorkspaceProcessActivationRequested(
        object? sender,
        WorkspaceProcessActivationRequestedEventArgs e)
    {
        var targetWindow = _activeWindow ?? _windows.LastOrDefault();
        if (targetWindow is null)
        {
            return;
        }

        void ActivateWindow() => ActivateWorkspaceInCurrentProcess(targetWindow, e.NavigationRequest);

        var dispatcherQueue = targetWindow.DispatcherQueue;
        if (dispatcherQueue.HasThreadAccess)
        {
            ActivateWindow();
            return;
        }

        if (!dispatcherQueue.TryEnqueue(ActivateWindow))
        {
            StartupDiagnostics.Log("Failed to enqueue cross-process workspace activation to the UI thread.");
        }
    }

    private void ActivateWorkspaceInCurrentProcess(
        MainWindow window,
        WorkspaceNavigationRequest navigationRequest)
    {
        _activeWindow = window;
        window.ActivateFromExternalWorkspaceRequest(navigationRequest);
    }

    /// <summary>
    /// 优先返回当前激活窗口，确保“已打开工作区再次打开”时能复用最贴近用户上下文的窗口。
    /// </summary>
    private MainWindow? FindWindowByWorkspaceKey(string workspaceKey)
    {
        if (_activeWindow is not null &&
            string.Equals(_activeWindow.RegisteredWorkspaceKey, workspaceKey, StringComparison.Ordinal))
        {
            return _activeWindow;
        }

        for (var index = _windows.Count - 1; index >= 0; index--)
        {
            var window = _windows[index];
            if (string.Equals(window.RegisteredWorkspaceKey, workspaceKey, StringComparison.Ordinal))
            {
                return window;
            }
        }

        return null;
    }

    /// <summary>
    /// 同进程激活也统一走窗口分发，避免未来若激活请求来自后台线程时直接触碰 UI。
    /// </summary>
    private bool TryActivateWorkspaceWindow(
        MainWindow window,
        WorkspaceNavigationRequest navigationRequest)
    {
        void ActivateWindow() => ActivateWorkspaceInCurrentProcess(window, navigationRequest);

        var dispatcherQueue = window.DispatcherQueue;
        if (dispatcherQueue.HasThreadAccess)
        {
            ActivateWindow();
            return true;
        }

        var enqueued = dispatcherQueue.TryEnqueue(ActivateWindow);
        if (!enqueued)
        {
            StartupDiagnostics.Log("Failed to enqueue same-process workspace activation to the UI thread.");
        }

        return enqueued;
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
