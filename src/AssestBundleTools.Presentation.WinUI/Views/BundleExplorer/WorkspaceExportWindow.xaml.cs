using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class WorkspaceExportWindow : Window
{
    private const int InitialWidth = 1360;
    private const int InitialHeight = 920;
    private const int MinimumWidth = 1080;
    private const int MinimumHeight = 720;
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private AppWindow? _appWindow;

    public event EventHandler? StartExportRequested;

    public WorkspaceExportWindow(
        WorkspaceExportDialogViewModel viewModel,
        IAppNotificationService notificationService)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        NotificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        InitializeComponent();
        ContentRoot.DataContext = ViewModel;
        ContentHost.Content = new WorkspaceExportDialogContent(ViewModel);
        ApplyLocalizedText();
        TryInitializeWindowChromeAndLayout();
        Closed += OnClosed;
    }

    public WorkspaceExportDialogViewModel ViewModel { get; }

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
            System.Diagnostics.Debug.WriteLine($"Workspace export window foreground activation failed: {ex}");
        }

        Activate();
    }

    private void OnStartExportButtonClick(object sender, RoutedEventArgs e)
    {
        StartExportRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelButtonClick(object sender, RoutedEventArgs e) => Close();

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnClosed;
        if (_appWindow is not null)
        {
            _appWindow.Changed -= OnAppWindowChanged;
            _appWindow = null;
        }
    }

    private void ApplyLocalizedText()
    {
        var title = UiText.Get(ResourceKeys.WorkspaceExportDialogTitle);
        Title = title;
        TitleBarText.Text = title;
        WindowTitleText.Text = title;
        WindowSubtitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogHeaderHint);
        PlanSummaryTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogPreviewSectionTitle);
        CancelButton.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogCloseButton);
    }

    private void TryInitializeWindowChromeAndLayout()
    {
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.Resize(new SizeInt32(InitialWidth, InitialHeight));
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = MinimumWidth;
                presenter.PreferredMinimumHeight = MinimumHeight;
            }

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                ExtendsContentIntoTitleBar = true;
                SetTitleBar(AppTitleBar);
                ConfigureTitleBar(_appWindow.TitleBar);
                UpdateTitleBarInsetColumns(_appWindow.TitleBar);
            }

            _appWindow.Changed += OnAppWindowChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Workspace export window layout initialization failed: {ex}");
        }
    }

    private static void ConfigureTitleBar(AppWindowTitleBar titleBar)
    {
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(24, 128, 128, 128);
        titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(38, 128, 128, 128);
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPresenterChange && AppWindowTitleBar.IsCustomizationSupported())
        {
            ConfigureTitleBar(sender.TitleBar);
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
