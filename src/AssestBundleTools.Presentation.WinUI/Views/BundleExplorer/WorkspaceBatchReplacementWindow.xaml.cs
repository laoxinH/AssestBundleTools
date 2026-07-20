using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 工作区级批量替换窗口，承载来源目录、manifest 计划和候选确认。
/// </summary>
public sealed partial class WorkspaceBatchReplacementWindow : Window
{
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private readonly WorkspaceBatchReplacementDialogContent _content;
    private AppWindow? _appWindow;

    public WorkspaceBatchReplacementWindow(
        WorkspaceBatchReplacementDialogViewModel viewModel,
        IAppNotificationService notificationService)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        NotificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        InitializeComponent();
        ModernWindowAppearanceService.TryApplySystemBackdrop(this);
        ContentRoot.DataContext = ViewModel;

        _content = new WorkspaceBatchReplacementDialogContent(ViewModel);
        _content.SourceDirectoryRequested += OnSourceDirectoryRequested;
        _content.RefreshRequested += OnRefreshRequested;
        ContentHost.Content = _content;

        ApplyLocalizedText();
        TryInitializeWindowChromeAndLayout();
        Closed += OnClosed;
    }

    public event EventHandler? SourceDirectoryRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? StartReplacementRequested;

    public WorkspaceBatchReplacementDialogViewModel ViewModel { get; }

    public IAppNotificationService NotificationService { get; }

    public void BringToFront()
    {
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            if (windowHandle != IntPtr.Zero)
            {
                ShowWindow(windowHandle, IsIconic(windowHandle) ? SwRestore : SwShow);
                SetForegroundWindow(windowHandle);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Workspace batch replacement window foreground activation failed: {ex}");
        }

        Activate();
    }

    private void OnSourceDirectoryRequested(object? sender, EventArgs e) =>
        SourceDirectoryRequested?.Invoke(this, EventArgs.Empty);

    private void OnRefreshRequested(object? sender, EventArgs e) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);

    private void OnStartReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        StartReplacementRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelButtonClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnClosed;
        _content.SourceDirectoryRequested -= OnSourceDirectoryRequested;
        _content.RefreshRequested -= OnRefreshRequested;
        if (_appWindow is not null)
        {
            _appWindow.Changed -= OnAppWindowChanged;
            _appWindow = null;
        }
    }

    private void ApplyLocalizedText()
    {
        var title = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogTitle);
        Title = title;
        TitleBarText.Text = title;
        WindowTitleText.Text = title;
        WindowSubtitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogHeaderHint);
        PlanSummaryTitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogPreviewSectionTitle);
        CancelButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogCloseButton);
    }

    private void TryInitializeWindowChromeAndLayout()
    {
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            DpiAwareWindowLayoutService.Apply(
                windowHandle,
                _appWindow,
                DpiAwareWindowLayoutProfile.WorkspaceOperationWindow);

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);
                ModernWindowAppearanceService.ConfigureTitleBar(_appWindow.TitleBar);
                UpdateTitleBarInsetColumns(_appWindow.TitleBar);
            }

            _appWindow.Changed += OnAppWindowChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Workspace batch replacement window layout initialization failed: {ex}");
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange && AppWindowTitleBar.IsCustomizationSupported())
        {
            ModernWindowAppearanceService.ConfigureTitleBar(sender.TitleBar);
            UpdateTitleBarInsetColumns(sender.TitleBar);
        }
    }

    private void UpdateTitleBarInsetColumns(AppWindowTitleBar titleBar)
    {
        LeftInsetColumn.Width = new GridLength(titleBar.LeftInset);
        RightInsetColumn.Width = new GridLength(titleBar.RightInset);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);
}
