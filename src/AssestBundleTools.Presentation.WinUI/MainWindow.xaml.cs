using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using AssestBundleTools.Presentation.WinUI.Views;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using System.Runtime.InteropServices;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI;

/// <summary>
/// 应用主窗口，当前仅承载后续工作区 UI 的外层容器。
/// </summary>
public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    private readonly ILocalizationService _localizationService;
    private readonly ShellLaunchRequest? _launchRequest;
    private AppWindow? _appWindow;

    public MainWindow()
        : this(new ResourceLocalizationService())
    {
    }

    internal MainWindow(ILocalizationService localizationService, ShellLaunchRequest? launchRequest = null)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _launchRequest = launchRequest;

        StartupDiagnostics.Log("MainWindow.InitializeComponent starting.");
        InitializeComponent();
        StartupDiagnostics.Log("MainWindow.InitializeComponent completed.");

        TryInitializeWindowChrome();
        StartupDiagnostics.Log("MainWindow window chrome initialization step completed.");

        TryInitializeSystemBackdrop();
        StartupDiagnostics.Log("MainWindow system backdrop initialization step completed.");

        InitializeLocalizedText();
        StartupDiagnostics.Log("MainWindow localized text initialized.");
    }

    /// <summary>
    /// 记录当前窗口最近一次成功绑定的工作区键值。
    /// 这里只保存轻量映射信息，真正的去重查找由 App 统一维护。
    /// </summary>
    internal string? RegisteredWorkspaceKey { get; private set; }

    /// <summary>
    /// 尝试初始化 Win11 风格的窗口标题栏。
    /// 这里把内容区域延伸到标题栏，以实现更接近系统设置页的无边框观感。
    /// 若当前环境不支持，也仅回退为默认标题栏，不阻断应用启动。
    /// </summary>
    private void TryInitializeWindowChrome()
    {
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            if (!AppWindowTitleBar.IsCustomizationSupported() || _appWindow is null)
            {
                StartupDiagnostics.Log("AppWindow title bar customization is not supported in the current environment.");
                return;
            }

            var titleBar = _appWindow.TitleBar;
            titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(20, 255, 255, 255);
            titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(40, 255, 255, 255);
            UpdateTitleBarInsetColumns(titleBar);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Window chrome initialization failed: {ex}");
            Debug.WriteLine($"Window chrome initialization failed: {ex}");
        }
    }

    /// <summary>
    /// 尝试启用窗口背景材质。
    /// 某些系统组合或调试场景下，WinUI 的系统背景初始化可能抛出 COM 异常，
    /// 此时应回退为普通窗口，而不是让应用在启动阶段直接退出。
    /// </summary>
    private void TryInitializeSystemBackdrop()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("ABT_DISABLE_SYSTEM_BACKDROP"),
                "1",
                StringComparison.Ordinal))
        {
            StartupDiagnostics.Log("System backdrop initialization skipped by environment variable.");
            Debug.WriteLine("System backdrop initialization skipped by environment variable.");
            return;
        }

        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch (COMException ex)
        {
            StartupDiagnostics.Log($"System backdrop initialization failed with COM exception: {ex}");
            Debug.WriteLine($"System backdrop initialization failed with COM exception: {ex}");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"System backdrop initialization failed with unexpected exception: {ex}");
            Debug.WriteLine($"System backdrop initialization failed with unexpected exception: {ex}");
        }
    }

    private void InitializeLocalizedText()
    {
        try
        {
            Title = _localizationService.GetRequiredString(ResourceKeys.MainWindowTitle);
            WindowTitleText.Text = Title;
            WindowSubtitleText.Text = _localizationService.GetRequiredString(ResourceKeys.MainWindowSubtitle);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Main window title localization failed: {ex}");
            Title = "AssestBundleTools";
            WindowTitleText.Text = Title;
            WindowSubtitleText.Text = string.Empty;
        }

        try
        {
            WindowContentHost.Content = new ShellPage(this, _launchRequest);
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Log($"Shell page initialization failed and switched to fallback view: {ex}");
            WindowContentHost.Content = BuildStartupFallbackView(ex);
        }
    }

    /// <summary>
    /// 由页面在工作区真正加载成功后回写当前窗口绑定关系。
    /// 这样“已打开工作区置顶”能力就不需要依赖页面树或临时导航参数做猜测。
    /// </summary>
    internal void RegisterWorkspace(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);
        RegisteredWorkspaceKey = App.CurrentApp.RegisterWindowWorkspace(this, navigationRequest);
    }

    internal void ClearRegisteredWorkspace()
    {
        RegisteredWorkspaceKey = null;
        App.CurrentApp.UnregisterWindowWorkspace(this);
    }

    private void UpdateTitleBarInsetColumns(AppWindowTitleBar titleBar)
    {
        LeftInsetColumn.Width = new GridLength(titleBar.LeftInset);
        RightInsetColumn.Width = new GridLength(titleBar.RightInset);
    }

    /// <summary>
    /// 当启动过程中的页面初始化失败时，提供一个最小可视诊断页，
    /// 避免整个应用在 F5 调试阶段直接闪退退出。
    /// </summary>
    private UIElement BuildStartupFallbackView(Exception exception)
    {
        return new ScrollViewer
        {
            Content = new Grid
            {
                Padding = new Thickness(24),
                Children =
                {
                    new Border
                    {
                        Padding = new Thickness(20),
                        CornerRadius = new CornerRadius(12),
                        Background = new SolidColorBrush(Microsoft.UI.Colors.WhiteSmoke),
                        Child = new StackPanel
                        {
                            Spacing = 10,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = _localizationService.GetRequiredString(ResourceKeys.StartupFallbackTitle),
                                    FontSize = 24,
                                    FontWeight = FontWeights.SemiBold
                                },
                                new TextBlock
                                {
                                    Text = _localizationService.GetRequiredString(ResourceKeys.StartupFallbackDescription),
                                    TextWrapping = TextWrapping.WrapWholeWords
                                },
                                new TextBlock
                                {
                                    Text = _localizationService.GetFormattedString(
                                        ResourceKeys.StartupFallbackExceptionFormat,
                                        exception.GetType().FullName ?? exception.GetType().Name),
                                    TextWrapping = TextWrapping.WrapWholeWords
                                },
                                new TextBlock
                                {
                                    Text = _localizationService.GetFormattedString(
                                        ResourceKeys.StartupFallbackMessageFormat,
                                        exception.Message),
                                    TextWrapping = TextWrapping.WrapWholeWords
                                },
                                new TextBlock
                                {
                                    Text = _localizationService.GetFormattedString(
                                        ResourceKeys.StartupFallbackLogFileFormat,
                                        StartupDiagnostics.LogFilePath),
                                    TextWrapping = TextWrapping.WrapWholeWords
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
