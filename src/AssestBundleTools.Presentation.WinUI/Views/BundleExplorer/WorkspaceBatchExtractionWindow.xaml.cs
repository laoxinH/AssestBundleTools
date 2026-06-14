using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 工作区级批量提取窗口，承载资源包范围、类型范围和输出目录选择。
/// </summary>
public sealed partial class WorkspaceBatchExtractionWindow : Window
{
    private const int InitialWidth = 1536;
    private const int InitialHeight = 1024;
    private const int MinimumWidth = 1180;
    private const int MinimumHeight = 780;
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private readonly WorkspaceBatchExtractionDialogContent _content;
    private readonly TaskCompletionSource<WorkspaceBatchExtractionDialogSubmission?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private AppWindow? _appWindow;
    private bool _isCompleting;

    public WorkspaceBatchExtractionWindow(
        WorkspaceBatchExtractionDialogViewModel viewModel,
        IAppNotificationService notificationService)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        NotificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        InitializeComponent();
        ContentRoot.DataContext = ViewModel;

        _content = new WorkspaceBatchExtractionDialogContent(ViewModel);
        ContentHost.Content = _content;

        ApplyLocalizedText();
        TryInitializeWindowChromeAndLayout();
        Closed += OnClosed;
    }

    public WorkspaceBatchExtractionDialogViewModel ViewModel { get; }

    public IAppNotificationService NotificationService { get; }

    public Task<WorkspaceBatchExtractionDialogSubmission?> ShowForSubmissionAsync()
    {
        Activate();
        return _completion.Task;
    }

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
            System.Diagnostics.Debug.WriteLine($"Workspace batch extraction window foreground activation failed: {ex}");
        }

        Activate();
    }

    private void OnStartExtractionButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TryCreateSubmission(out var submission))
        {
            Complete(submission);
        }
    }

    private void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
        Complete(null);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnClosed;
        if (_appWindow is not null)
        {
            _appWindow.Changed -= OnAppWindowChanged;
            _appWindow = null;
        }

        if (!_isCompleting)
        {
            _isCompleting = true;
            _completion.TrySetResult(null);
        }
    }

    private void Complete(WorkspaceBatchExtractionDialogSubmission? submission)
    {
        if (_isCompleting)
        {
            return;
        }

        _isCompleting = true;
        _completion.TrySetResult(submission);
        Close();
    }

    private void ApplyLocalizedText()
    {
        var title = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogTitle);
        Title = title;
        TitleBarText.Text = title;
        WindowTitleText.Text = title;
        WindowSubtitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogHeaderHint);
        PlanSummaryTitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogPreviewSectionTitle);
        UseWorkspaceExtractionDirectoryCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogUseWorkspaceExtractionDirectoryLabel);
        ToolTipService.SetToolTip(
            UseWorkspaceExtractionDirectoryCheckBox,
            UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogUseWorkspaceExtractionDirectoryToolTip));
        StartExtractionButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogPrimaryButton);
        CancelButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogCloseButton);
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
            System.Diagnostics.Debug.WriteLine($"Workspace batch extraction window layout initialization failed: {ex}");
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
