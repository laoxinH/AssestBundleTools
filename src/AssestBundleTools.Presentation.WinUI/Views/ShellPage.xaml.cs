using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;
using AssestBundleTools.Presentation.WinUI.Views.Home;
using AssestBundleTools.Presentation.WinUI.Views.PluginManagement;
using AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics;
using AssestBundleTools.Presentation.WinUI.Views.Shell.Coordinators;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;
namespace AssestBundleTools.Presentation.WinUI.Views;

/// <summary>
/// 应用主外壳，负责一级导航和页面切换。
/// </summary>
/// <remarks>
/// 当前窗口只维护两种 Presentation 级模式：
/// 1. 控制台态：承载工作区新建、打开、恢复等入口。
/// 2. 工作台态：承载正式资源浏览与编辑流程。
/// 两种模式都复用同一个一级入口，但允许在显式操作时从工作台返回控制台，
/// 避免重新启动应用才能切换入口。
/// </remarks>
public sealed partial class ShellPage : Page
{
    private const double CompactNavigationWidthThreshold = 1360d;
    private readonly AssestBundleTools.Presentation.WinUI.MainWindow? _hostWindow;
    private readonly ShellLaunchRequest? _launchRequest;
    private readonly ShellNavigationPaneCoordinator _navigationPaneCoordinator;
    private readonly DispatcherTimer _backgroundTaskPanelCloseTimer = new();
    private bool _isSynchronizingNavigationSelection;
    private bool _isPointerOverBackgroundTaskSummary;
    private bool _isPointerOverBackgroundTasksPanel;
    private SettingsDiagnosticsSection _lastSettingsSection = SettingsDiagnosticsSection.Preferences;
    private ShellWindowMode _windowMode;

    public ShellPage()
        : this(null, null)
    {
    }

    internal ShellPage(AssestBundleTools.Presentation.WinUI.MainWindow? hostWindow, ShellLaunchRequest? launchRequest)
    {
        _hostWindow = hostWindow;
        _launchRequest = launchRequest;
        _windowMode = ResolveInitialWindowMode(launchRequest);
        NotificationService = App.CurrentApp.AppNotificationService;
        BackgroundTaskStatusService = App.CurrentApp.AppBackgroundTaskStatusService;
        BackgroundTasks = new ShellBackgroundTasksViewModel(
            App.CurrentApp.BackgroundWorkScheduler,
            App.CurrentApp.LocalizationService,
            DispatcherQueue);
        ViewModel = new ShellViewModel(App.CurrentApp.LocalizationService);
        InitializeComponent();
        _navigationPaneCoordinator = new ShellNavigationPaneCoordinator(
            RootNavigationView,
            SettingsDiagnosticsNavItem,
            ContentFrame);
        if (BackgroundTaskStatusService is AppBackgroundTaskStatusService backgroundTaskStatusService)
        {
            backgroundTaskStatusService.AttachDispatcherQueue(DispatcherQueue);
        }

        AppStatusBar.DataContext = BackgroundTaskStatusService;
        BackgroundTasks.PropertyChanged += OnBackgroundTasksPropertyChanged;
        _backgroundTaskPanelCloseTimer.Interval = TimeSpan.FromMilliseconds(260);
        _backgroundTaskPanelCloseTimer.Tick += OnBackgroundTaskPanelCloseTimerTick;
        ApplyLocalizedText();
        ApplyWindowMode();
        SizeChanged += OnShellPageSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ShellViewModel ViewModel { get; }

    public IAppNotificationService NotificationService { get; }

    public IAppBackgroundTaskStatusService BackgroundTaskStatusService { get; }

    public ShellBackgroundTasksViewModel BackgroundTasks { get; }

    public string CancelBackgroundTaskToolTipText =>
        App.CurrentApp.LocalizationService.GetRequiredString(ResourceKeys.AppStatusBarCancelTaskToolTip);

    internal AssestBundleTools.Presentation.WinUI.MainWindow? HostWindow => _hostWindow;

    private enum ShellWindowMode
    {
        LaunchGuide,
        Workspace
    }

    /// <summary>
    /// 表示一次 Shell 级导航解析后的最终目标。
    /// 控制台页当前复用第一导航入口承载，因此导航项允许与正式工作台共用。
    /// </summary>
    private readonly record struct ShellNavigationTarget(
        string ShellSectionKey,
        Type PageType,
        NavigationViewItem? NavigationItem,
        SettingsDiagnosticsSection? SettingsSection = null);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveNavigationLayout(ActualWidth);

        if (ContentFrame.CurrentSourcePageType is not null)
        {
            return;
        }

        if (_launchRequest is not null)
        {
            NavigateTo(_launchRequest.NavigationKey, _launchRequest.NavigationParameter, updateSelection: true);
            return;
        }

        NavigateTo(ShellNavigationKeys.LaunchGuide, parameter: null, updateSelection: true);
    }

    private void OnShellPageSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyResponsiveNavigationLayout(e.NewSize.Width);

    private void ApplyResponsiveNavigationLayout(double effectiveWidth)
    {
        if (effectiveWidth > 0d &&
            effectiveWidth < CompactNavigationWidthThreshold &&
            RootNavigationView.IsPaneOpen)
        {
            RootNavigationView.IsPaneOpen = false;
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isSynchronizingNavigationSelection)
        {
            return;
        }

        if (args.SelectedItem is not NavigationViewItem selectedItem ||
            selectedItem.Tag is not string navigationKey)
        {
            return;
        }

        NavigateTo(navigationKey, parameter: null, updateSelection: false);
    }

    private void OnNavigationItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (!ReferenceEquals(args.InvokedItemContainer, SettingsDiagnosticsNavItem))
        {
            return;
        }

        // compact 状态由 NavigationView 自己打开层级浮层，立即导航会打断子项容器创建。
        if (!sender.IsPaneOpen)
        {
            return;
        }

        _navigationPaneCoordinator.ExpandSettingsIfPaneOpen();
        NavigateTo(GetSettingsNavigationKey(_lastSettingsSection), parameter: null, updateSelection: true);
    }

    private void OnCancelBackgroundTaskButtonClick(object sender, RoutedEventArgs e)
    {
        BackgroundTaskStatusService.CancelCurrentTask();
    }

    private void OnBackgroundTaskSummaryPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverBackgroundTaskSummary = true;
        _backgroundTaskPanelCloseTimer.Stop();
        ShowBackgroundTasksPanel();
    }

    private void OnBackgroundTaskSummaryPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverBackgroundTaskSummary = false;
        QueueBackgroundTaskPanelClose();
    }

    private void OnBackgroundTaskSummaryClick(object sender, RoutedEventArgs e)
    {
        ShowBackgroundTasksPanel();
    }

    private void OnBackgroundTasksPanelPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverBackgroundTasksPanel = true;
        _backgroundTaskPanelCloseTimer.Stop();
    }

    private void OnBackgroundTasksPanelPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isPointerOverBackgroundTasksPanel = false;
        QueueBackgroundTaskPanelClose();
    }

    private void ShowBackgroundTasksPanel()
    {
        if (BackgroundTasks.TaskCount > 0)
        {
            BackgroundTasksPanelHost.Visibility = Visibility.Visible;
        }
    }

    internal void NavigateToSection(string navigationKey, object? parameter = null)
    {
        NavigateTo(navigationKey, parameter, updateSelection: true);
    }

    /// <summary>
    /// 当前窗口已经持有目标工作区时，外部激活只需切回工作台即可，
    /// 避免重复带参数导航导致页面整页重建。
    /// </summary>
    internal void ActivateCurrentWorkspace()
    {
        NavigateTo(ShellNavigationKeys.Workbench, parameter: null, updateSelection: true);
    }

    /// <summary>
    /// 当外部进程要求当前窗口承载某个工作区时，统一由 Shell 执行导航。
    /// </summary>
    internal void NavigateToWorkspace(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);
        NavigateTo(ShellNavigationKeys.Workbench, navigationRequest, updateSelection: true);
    }

    /// <summary>
    /// 统一处理 Shell 级导航，并由窗口模式决定当前第一入口承载控制台还是工作台。
    /// 这样“控制台 / 工作台共用一个一级入口位”的规则可以收敛在一个入口里维护。
    /// </summary>
    private void NavigateTo(string navigationKey, object? parameter, bool updateSelection)
    {
        var navigationTarget = ResolveNavigationTarget(navigationKey, parameter);

        if (navigationTarget.SettingsSection is { } settingsSection)
        {
            _lastSettingsSection = settingsSection;
            _navigationPaneCoordinator.ExpandSettingsIfPaneOpen();
        }

        if (updateSelection)
        {
            SynchronizeNavigationSelection(navigationTarget.NavigationItem);
        }

        if (ContentFrame.CurrentSourcePageType != navigationTarget.PageType || parameter is not null)
        {
            ContentFrame.Navigate(navigationTarget.PageType, parameter);
        }

        if (navigationTarget.SettingsSection is { } activeSettingsSection &&
            ContentFrame.Content is SettingsDiagnosticsPage settingsPage)
        {
            settingsPage.ActivateSection(activeSettingsSection);
            _navigationPaneCoordinator.ApplyCurrentLayout();
        }

        ViewModel.ApplySection(navigationTarget.ShellSectionKey);
    }

    private void OnNavigationPaneOpened(NavigationView sender, object args) =>
        _navigationPaneCoordinator.HandlePaneOpened();

    private void OnNavigationPaneClosing(
        NavigationView sender,
        NavigationViewPaneClosingEventArgs args)
    {
        _navigationPaneCoordinator.HandlePaneClosing();
    }

    private ShellNavigationTarget ResolveNavigationTarget(string navigationKey, object? parameter)
    {
        if (navigationKey == ShellNavigationKeys.LaunchGuide)
        {
            SetWindowMode(ShellWindowMode.LaunchGuide);
            return CreateNavigationTarget(ShellNavigationKeys.LaunchGuide);
        }

        // 只有显式带着工作区导航请求进入工作台时，当前窗口才会从控制台态切换成正式工作台态。
        if (navigationKey == ShellNavigationKeys.Workbench &&
            parameter is WorkspaceNavigationRequest)
        {
            SetWindowMode(ShellWindowMode.Workspace);
            return CreateNavigationTarget(ShellNavigationKeys.Workbench);
        }

        // 如果当前窗口已经持有已加载工作区，则允许在不重复传入导航参数的情况下
        // 直接从控制台态回到工作台。这样既能支持“已打开工作区再次激活”，
        if (navigationKey == ShellNavigationKeys.Workbench &&
            HasRegisteredWorkspace())
        {
            SetWindowMode(ShellWindowMode.Workspace);
            return CreateNavigationTarget(ShellNavigationKeys.Workbench);
        }

        if (IsSettingsDiagnosticsNavigationKey(navigationKey))
        {
            return CreateNavigationTarget(navigationKey);
        }

        return _windowMode switch
        {
            ShellWindowMode.Workspace => ResolveWorkspaceModeTarget(navigationKey),
            _ => ResolveLaunchGuideModeTarget(navigationKey)
        };
    }

    private bool HasRegisteredWorkspace()
    {
        return !string.IsNullOrWhiteSpace(_hostWindow?.RegisteredWorkspaceKey) ||
               !string.IsNullOrWhiteSpace(_hostWindow?.PendingWorkspaceKey);
    }

    private ShellNavigationTarget ResolveLaunchGuideModeTarget(string navigationKey)
    {
        return navigationKey switch
        {
            ShellNavigationKeys.PluginManagement => CreateNavigationTarget(ShellNavigationKeys.PluginManagement),
            _ => CreateNavigationTarget(ShellNavigationKeys.LaunchGuide)
        };
    }

    private ShellNavigationTarget ResolveWorkspaceModeTarget(string navigationKey)
    {
        return navigationKey switch
        {
            ShellNavigationKeys.PluginManagement => CreateNavigationTarget(ShellNavigationKeys.PluginManagement),
            ShellNavigationKeys.LaunchGuide => CreateNavigationTarget(ShellNavigationKeys.Workbench),
            _ => CreateNavigationTarget(ShellNavigationKeys.Workbench)
        };
    }

    private ShellNavigationTarget CreateNavigationTarget(string navigationKey)
    {
        return navigationKey switch
        {
            ShellNavigationKeys.LaunchGuide => new ShellNavigationTarget(
                navigationKey,
                typeof(HomePage),
                WorkbenchNavItem),
            ShellNavigationKeys.Workbench => new ShellNavigationTarget(
                navigationKey,
                typeof(BundleExplorerPage),
                WorkbenchNavItem),
            ShellNavigationKeys.PluginManagement => new ShellNavigationTarget(
                navigationKey,
                typeof(PluginManagementPage),
                PluginManagementNavItem),
            ShellNavigationKeys.SettingsDiagnostics => CreateSettingsNavigationTarget(_lastSettingsSection),
            ShellNavigationKeys.SettingsDiagnosticsWorkspace => CreateSettingsNavigationTarget(
                SettingsDiagnosticsSection.Workspace),
            ShellNavigationKeys.SettingsDiagnosticsPreferences => CreateSettingsNavigationTarget(
                SettingsDiagnosticsSection.Preferences),
            ShellNavigationKeys.SettingsDiagnosticsPerformanceCache => CreateSettingsNavigationTarget(
                SettingsDiagnosticsSection.PerformanceCache),
            ShellNavigationKeys.SettingsDiagnosticsLogs => CreateSettingsNavigationTarget(
                SettingsDiagnosticsSection.Logs),
            ShellNavigationKeys.SettingsDiagnosticsAbout => CreateSettingsNavigationTarget(
                SettingsDiagnosticsSection.About),
            _ => CreateNavigationTarget(
                _windowMode == ShellWindowMode.Workspace
                    ? ShellNavigationKeys.Workbench
                    : ShellNavigationKeys.LaunchGuide)
        };
    }

    private static bool IsSettingsDiagnosticsNavigationKey(string navigationKey) =>
        navigationKey is ShellNavigationKeys.SettingsDiagnostics
            or ShellNavigationKeys.SettingsDiagnosticsWorkspace
            or ShellNavigationKeys.SettingsDiagnosticsPreferences
            or ShellNavigationKeys.SettingsDiagnosticsPerformanceCache
            or ShellNavigationKeys.SettingsDiagnosticsLogs
            or ShellNavigationKeys.SettingsDiagnosticsAbout;

    private static string GetSettingsNavigationKey(SettingsDiagnosticsSection section) => section switch
    {
        SettingsDiagnosticsSection.Workspace => ShellNavigationKeys.SettingsDiagnosticsWorkspace,
        SettingsDiagnosticsSection.Preferences => ShellNavigationKeys.SettingsDiagnosticsPreferences,
        SettingsDiagnosticsSection.PerformanceCache => ShellNavigationKeys.SettingsDiagnosticsPerformanceCache,
        SettingsDiagnosticsSection.Logs => ShellNavigationKeys.SettingsDiagnosticsLogs,
        SettingsDiagnosticsSection.About => ShellNavigationKeys.SettingsDiagnosticsAbout,
        _ => ShellNavigationKeys.SettingsDiagnosticsPreferences
    };

    private ShellNavigationTarget CreateSettingsNavigationTarget(SettingsDiagnosticsSection section)
    {
        var navigationItem = section switch
        {
            SettingsDiagnosticsSection.Workspace => SettingsWorkspaceNavItem,
            SettingsDiagnosticsSection.Preferences => SettingsPreferencesNavItem,
            SettingsDiagnosticsSection.PerformanceCache => SettingsPerformanceCacheNavItem,
            SettingsDiagnosticsSection.Logs => SettingsLogsNavItem,
            SettingsDiagnosticsSection.About => SettingsAboutNavItem,
            _ => SettingsPreferencesNavItem
        };

        return new ShellNavigationTarget(
            ShellNavigationKeys.SettingsDiagnostics,
            typeof(SettingsDiagnosticsPage),
            navigationItem,
            section);
    }

    /// <summary>
    /// 当前阶段显式设置导航项文本，确保导航结构不依赖 `x:Uid` 自动注入。
    /// </summary>
    private void ApplyLocalizedText()
    {
        PluginManagementNavItem.Content = UiText.Get(ResourceKeys.ShellPluginManagementNavItemContent);
        SettingsDiagnosticsNavItem.Content = UiText.Get(ResourceKeys.ShellSettingsDiagnosticsNavItemContent);
        SettingsWorkspaceNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavWorkspace);
        SettingsPreferencesNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavPreferences);
        SettingsPerformanceCacheNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavPerformanceCache);
        SettingsLogsNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavLogs);
        SettingsAboutNavItem.Content = UiText.Get(ResourceKeys.SettingsDiagnosticsNavAbout);
    }

    private void OnBackgroundTasksPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellBackgroundTasksViewModel.TaskCount) &&
            BackgroundTasks.TaskCount == 0)
        {
            HideBackgroundTasksPanel();
        }
    }

    private void QueueBackgroundTaskPanelClose()
    {
        _backgroundTaskPanelCloseTimer.Stop();
        _backgroundTaskPanelCloseTimer.Start();
    }

    private void OnBackgroundTaskPanelCloseTimerTick(object? sender, object e)
    {
        _backgroundTaskPanelCloseTimer.Stop();
        if (!_isPointerOverBackgroundTaskSummary && !_isPointerOverBackgroundTasksPanel)
        {
            HideBackgroundTasksPanel();
        }
    }

    private void HideBackgroundTasksPanel()
    {
        BackgroundTasksPanelHost.Visibility = Visibility.Collapsed;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnShellPageSizeChanged;
        BackgroundTasks.PropertyChanged -= OnBackgroundTasksPropertyChanged;
        BackgroundTasks.Dispose();
        _backgroundTaskPanelCloseTimer.Stop();
        _backgroundTaskPanelCloseTimer.Tick -= OnBackgroundTaskPanelCloseTimerTick;
    }

    /// <summary>
    /// 当前窗口仍保留“控制台态 / 工作台态”两态模型，
    /// 但第一导航入口始终保持可用。
    /// 无工作区时显示控制台，有工作区时显示正式工作台。
    /// </summary>
    private void ApplyWindowMode()
    {
        WorkbenchNavItem.Content = _windowMode == ShellWindowMode.Workspace
            ? UiText.Get(ResourceKeys.ShellWorkbenchNavItemContent)
            : UiText.Get(ResourceKeys.ShellConsoleNavItemContent);

        WorkbenchNavItem.IsEnabled = true;
    }

    private void SetWindowMode(ShellWindowMode windowMode)
    {
        if (_windowMode == windowMode)
        {
            ApplyWindowMode();
            return;
        }

        _windowMode = windowMode;
        ApplyWindowMode();
    }

    private void SynchronizeNavigationSelection(NavigationViewItem? targetItem)
    {
        if (ReferenceEquals(RootNavigationView.SelectedItem, targetItem))
        {
            return;
        }

        _isSynchronizingNavigationSelection = true;
        RootNavigationView.SelectedItem = targetItem;
        _isSynchronizingNavigationSelection = false;
    }

    private static ShellWindowMode ResolveInitialWindowMode(ShellLaunchRequest? launchRequest)
    {
        return launchRequest is not null &&
               launchRequest.NavigationKey == ShellNavigationKeys.Workbench &&
               launchRequest.NavigationParameter is WorkspaceNavigationRequest
            ? ShellWindowMode.Workspace
            : ShellWindowMode.LaunchGuide;
    }
}

