using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.Views.BatchTasks;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;
using AssestBundleTools.Presentation.WinUI.Views.Home;
using AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    private readonly AssestBundleTools.Presentation.WinUI.MainWindow? _hostWindow;
    private readonly ShellLaunchRequest? _launchRequest;
    private bool _isSynchronizingNavigationSelection;
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
        ViewModel = new ShellViewModel(App.CurrentApp.LocalizationService);
        InitializeComponent();
        ApplyLocalizedText();
        ApplyWindowMode();
        Loaded += OnLoaded;
    }

    public ShellViewModel ViewModel { get; }

    public IAppNotificationService NotificationService { get; }

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
        string NavigationKey,
        Type PageType,
        NavigationViewItem? NavigationItem);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
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

        if (updateSelection)
        {
            SynchronizeNavigationSelection(navigationTarget.NavigationItem);
        }

        if (ContentFrame.CurrentSourcePageType != navigationTarget.PageType || parameter is not null)
        {
            ContentFrame.Navigate(navigationTarget.PageType, parameter);
        }

        ViewModel.ApplySection(navigationTarget.NavigationKey);
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
            ShellNavigationKeys.BatchTasks => CreateNavigationTarget(ShellNavigationKeys.BatchTasks),
            ShellNavigationKeys.SettingsDiagnostics => CreateNavigationTarget(ShellNavigationKeys.SettingsDiagnostics),
            _ => CreateNavigationTarget(ShellNavigationKeys.LaunchGuide)
        };
    }

    private ShellNavigationTarget ResolveWorkspaceModeTarget(string navigationKey)
    {
        return navigationKey switch
        {
            ShellNavigationKeys.BatchTasks => CreateNavigationTarget(ShellNavigationKeys.BatchTasks),
            ShellNavigationKeys.SettingsDiagnostics => CreateNavigationTarget(ShellNavigationKeys.SettingsDiagnostics),
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
            ShellNavigationKeys.BatchTasks => new ShellNavigationTarget(
                navigationKey,
                typeof(BatchTasksPage),
                BatchTasksNavItem),
            ShellNavigationKeys.SettingsDiagnostics => new ShellNavigationTarget(
                navigationKey,
                typeof(SettingsDiagnosticsPage),
                SettingsDiagnosticsNavItem),
            _ => CreateNavigationTarget(
                _windowMode == ShellWindowMode.Workspace
                    ? ShellNavigationKeys.Workbench
                    : ShellNavigationKeys.LaunchGuide)
        };
    }

    /// <summary>
    /// 当前阶段显式设置导航项文本，确保导航结构不依赖 `x:Uid` 自动注入。
    /// </summary>
    private void ApplyLocalizedText()
    {
        PaneTitleText.Text = UiText.Get(ResourceKeys.ShellPaneTitle);
        PaneSubtitleText.Text = UiText.Get(ResourceKeys.ShellPaneSubtitle);
        BatchTasksNavItem.Content = UiText.Get(ResourceKeys.ShellBatchTasksNavItemContent);
        SettingsDiagnosticsNavItem.Content = UiText.Get(ResourceKeys.ShellSettingsDiagnosticsNavItemContent);
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

