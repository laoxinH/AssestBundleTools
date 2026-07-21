using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class WorkspaceExportWindow : Window
{
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private AppWindow? _appWindow;
    private DpiAwareWindowSizeTracker? _windowSizeTracker;
    private DpiAwareWindowSize? _lastRestoredSize;

    public event EventHandler? StartExportRequested;

    public event EventHandler? CancelExportRequested;

    public WorkspaceExportWindow(
        WorkspaceExportDialogViewModel viewModel,
        IAppNotificationService notificationService)
        : this(viewModel, notificationService, rememberedSize: null)
    {
    }

    internal WorkspaceExportWindow(
        WorkspaceExportDialogViewModel viewModel,
        IAppNotificationService notificationService,
        DpiAwareWindowSize? rememberedSize)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        NotificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        InitializeComponent();
        ModernWindowAppearanceService.TryApplySystemBackdrop(this);
        ContentRoot.DataContext = ViewModel;
        ContentHost.Content = new WorkspaceExportDialogContent(ViewModel);
        ApplyLocalizedText();
        TryInitializeWindowChromeAndLayout(rememberedSize);
        Closed += OnClosed;
    }

    public WorkspaceExportDialogViewModel ViewModel { get; }

    public IAppNotificationService NotificationService { get; }

    internal DpiAwareWindowSize? LastRestoredSize => _lastRestoredSize;

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

    private void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsExecuting)
        {
            CancelExportRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnClosed;
        CaptureRestoredSize();
        if (_appWindow is not null)
        {
            _appWindow.Changed -= OnAppWindowChanged;
            _appWindow = null;
        }
        _windowSizeTracker = null;
    }

    private void ApplyLocalizedText()
    {
        var title = UiText.Get(ResourceKeys.WorkspaceExportDialogTitle);
        Title = title;
        TitleBarText.Text = title;
        WindowTitleText.Text = title;
        WindowSubtitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogHeaderHint);
        PlanSummaryTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogPreviewSectionTitle);
    }

    private void TryInitializeWindowChromeAndLayout(DpiAwareWindowSize? rememberedSize)
    {
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            DpiAwareWindowLayoutService.Apply(
                windowHandle,
                _appWindow,
                DpiAwareWindowLayoutProfile.WorkspaceOperationWindow,
                rememberedSize);
            _windowSizeTracker = new DpiAwareWindowSizeTracker(windowHandle, _appWindow);
            CaptureRestoredSize();

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
            System.Diagnostics.Debug.WriteLine($"Workspace export window layout initialization failed: {ex}");
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        _windowSizeTracker?.HandleWindowChanged(args);
        CaptureRestoredSize();
        if (args.DidPresenterChange && AppWindowTitleBar.IsCustomizationSupported())
        {
            ModernWindowAppearanceService.ConfigureTitleBar(sender.TitleBar);
            UpdateTitleBarInsetColumns(sender.TitleBar);
        }
    }

    private void CaptureRestoredSize()
    {
        _windowSizeTracker?.CaptureCurrentSize();
        if (_windowSizeTracker?.LastRestoredSize is { IsValid: true } size)
        {
            _lastRestoredSize = size;
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
