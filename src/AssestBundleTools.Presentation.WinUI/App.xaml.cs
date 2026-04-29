using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Composition;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Workspace.Services;
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
    private readonly Action _reloadAssetBundleDecryptorRegistry;

    public App()
    {
        StartupDiagnostics.Log("App constructor started.");

        try
        {
            var services = AppCompositionRoot.Build(new AppCompositionContext(
                ActiveWindowAccessor: () => _activeWindow,
                WorkspaceActivationRequestedHandler: OnWorkspaceProcessActivationRequested));

            LocalizationService = services.LocalizationService;
            AppPreferencesService = services.AppPreferencesService;
            _assetBundleDecryptorRegistry = services.AssetBundleDecryptorRegistry;
            _reloadAssetBundleDecryptorRegistry = services.ReloadAssetBundleDecryptorRegistry;
            WorkspaceExplorerGateway = services.WorkspaceExplorerGateway;
            WorkspaceAssetInspectionGateway = services.WorkspaceAssetInspectionGateway;
            WorkspaceAssetOperationGateway = services.WorkspaceAssetOperationGateway;
            WorkspaceBatchOperationsGateway = services.WorkspaceBatchOperationsGateway;
            WorkspaceContextService = services.WorkspaceContextService;
            WorkspaceProcessCoordinatorService = services.WorkspaceProcessCoordinatorService;
            FilePickerService = services.FilePickerService;
            PathNavigationService = services.PathNavigationService;
            WorkspaceCatalogGateway = services.WorkspaceCatalogGateway;
            AssetBundleDecryptorCatalogService = services.AssetBundleDecryptorCatalogService;
            WorkspaceDialogService = services.WorkspaceDialogService;
            AppInteractionFeedbackService = services.AppInteractionFeedbackService;
            WorkspaceWindowService = services.WorkspaceWindowService;
            AppNotificationService = services.AppNotificationService;
            AppLogService = services.AppLogService;
            WorkspaceImportCoordinatorService = services.WorkspaceImportCoordinatorService;

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

    public IWorkspaceContextService WorkspaceContextService { get; }

    public IWorkspaceProcessCoordinatorService WorkspaceProcessCoordinatorService { get; }

    public IFilePickerService FilePickerService { get; }

    public IPathNavigationService PathNavigationService { get; }

    public IWorkspaceCatalogGateway WorkspaceCatalogGateway { get; }

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
        _reloadAssetBundleDecryptorRegistry();
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
            var targetWindow = FindWindowByOwnedWorkspaceKey(workspaceKey);
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
               string.IsNullOrWhiteSpace(window.RegisteredWorkspaceKey) &&
               string.IsNullOrWhiteSpace(window.PendingWorkspaceKey);
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
               window.OwnsWorkspace(workspaceKey);
    }

    /// <summary>
    /// 页面进入真正的工作区加载前，先在进程级建立一次临时预登记。
    /// 这样其他窗口或进程即便在加载中再次命中同一工作区，也能稳定重定向到当前窗口。
    /// </summary>
    public bool TryBeginWindowWorkspaceLoad(MainWindow window, WorkspaceNavigationRequest navigationRequest)
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
            WorkspaceProcessCoordinatorService.ClearPendingWorkspaceRegistration();
            return false;
        }

        _activeWindow = window;
        return WorkspaceProcessCoordinatorService.TryBeginPendingWorkspaceRegistration(normalizedNavigationRequest);
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
        var workspaceKey = CreateWorkspaceKey(e.NavigationRequest);
        var targetWindow = !string.IsNullOrWhiteSpace(workspaceKey)
            ? FindWindowByOwnedWorkspaceKey(workspaceKey)
            : null;
        targetWindow ??= _activeWindow ?? _windows.LastOrDefault();
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
    private MainWindow? FindWindowByOwnedWorkspaceKey(string workspaceKey)
    {
        if (_activeWindow is not null &&
            _activeWindow.OwnsWorkspace(workspaceKey))
        {
            return _activeWindow;
        }

        for (var index = _windows.Count - 1; index >= 0; index--)
        {
            var window = _windows[index];
            if (window.OwnsWorkspace(workspaceKey))
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
