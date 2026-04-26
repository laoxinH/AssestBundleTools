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
using AssestBundleTools.Infrastructure.Bundles.Reading;
using AssestBundleTools.Infrastructure.Settings.Persistence;
using AssestBundleTools.Infrastructure.Workspace.Importing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;
using AssestBundleTools.Infrastructure.Workspace.Runtime;
using System.Diagnostics;

namespace AssestBundleTools.Presentation.WinUI;

/// <summary>
/// 应用入口，负责初始化全局级别的 UI 资源和主窗口。
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly List<MainWindow> _windows = [];
    private readonly Dictionary<string, MainWindow> _workspaceWindows = new(StringComparer.Ordinal);
    private readonly Dictionary<MainWindow, string> _windowWorkspaceKeys = new();
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
            var assetPreviewGenerator = new AssetsToolsAssetPreviewGenerator();
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
                assetPreviewGenerator);
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
            WorkspaceWindowService = new WorkspaceWindowService();
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
            var window = CreateMainWindow();
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
    /// 尝试把已经承载目标工作区的窗口激活到前台。
    /// 当前仅按稳定工作区键去重，避免“新建窗口模式”不断派生相同工作区副本。
    /// </summary>
    public bool TryActivateWindowForWorkspace(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);

        var workspaceKey = CreateWorkspaceKey(navigationRequest);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return false;
        }

        if (!_workspaceWindows.TryGetValue(workspaceKey, out var window) ||
            !_windows.Contains(window))
        {
            _workspaceWindows.Remove(workspaceKey);
            if (window is not null)
            {
                _windowWorkspaceKeys.Remove(window);
            }

            return false;
        }

        _activeWindow = window;
        window.Activate();
        return true;
    }

    /// <summary>
    /// 记录“哪个主窗口当前承载哪个工作区”。
    /// 页面层只需在工作区真正绑定成功后调用这里，窗口去重与清理则统一由 App 管理。
    /// </summary>
    public string RegisterWindowWorkspace(MainWindow window, WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(navigationRequest);

        var workspaceKey = CreateWorkspaceKey(navigationRequest);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            UnregisterWindowWorkspace(window);
            return string.Empty;
        }

        UnregisterWindowWorkspace(window);
        if (_workspaceWindows.TryGetValue(workspaceKey, out var existingWindow) &&
            !ReferenceEquals(existingWindow, window))
        {
            _windowWorkspaceKeys.Remove(existingWindow);
        }

        _workspaceWindows[workspaceKey] = window;
        _windowWorkspaceKeys[window] = workspaceKey;
        return workspaceKey;
    }

    public void UnregisterWindowWorkspace(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!_windowWorkspaceKeys.Remove(window, out var workspaceKey))
        {
            return;
        }

        if (_workspaceWindows.TryGetValue(workspaceKey, out var registeredWindow) &&
            ReferenceEquals(registeredWindow, window))
        {
            _workspaceWindows.Remove(workspaceKey);
        }
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
