using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;
using AssestBundleTools.Presentation.WinUI.ViewModels.Home;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;
namespace AssestBundleTools.Presentation.WinUI.Views.Home;

/// <summary>
/// 应用控制台页，负责承载最近工作区和工作区入口。
/// </summary>
/// <remarks>
/// 该页在窗口处于控制台态时，作为一级入口承载“新建 / 打开 / 恢复工作区”等操作。
/// 当前实现允许从正式工作台返回这里重新整理工作区入口，
/// 但不会清空现有活动工作区上下文。
/// </remarks>
public sealed partial class HomePage : Page
{
    private bool _isDisposed;

    public HomePage()
    {
        ViewModel = new HomeViewModel(
            App.CurrentApp.WorkspaceContextService,
            App.CurrentApp.AppLogService,
            App.CurrentApp.LocalizationService);

        InitializeComponent();
        ApplyLocalizedText();
        Unloaded += OnUnloaded;
    }

    public HomeViewModel ViewModel { get; }

    /// <summary>
    /// 供数据模板中的删除按钮读取悬浮提示文本。
    /// 这里保持为页面属性，避免在模板里硬编码字符串。
    /// </summary>
    public string RecentWorkspaceDeleteToolTipText =>
        UiText.Get(ResourceKeys.HomePageRecentWorkspaceDeleteButtonToolTip);

    private async void OnNewWorkspaceButtonClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var request = await App.CurrentApp.WorkspaceDialogService.RequestNewWorkspaceAsync(XamlRoot);
        OpenWorkspace(request);
    }

    private async void OnOpenWorkspaceButtonClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var request = await App.CurrentApp.WorkspaceDialogService.RequestOpenWorkspaceFromDirectoryAsync(XamlRoot);
        OpenWorkspace(request);
    }

    private async void OnOpenLatestWorkspaceButtonClick(object sender, RoutedEventArgs e)
    {
        var latestWorkspace = ViewModel.LatestRecentWorkspaceItem;
        if (latestWorkspace is null)
        {
            return;
        }

        await OpenRecentWorkspaceAsync(latestWorkspace);
    }

    private async void OnRecentWorkspaceButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HomeRecentWorkspaceItemViewModel workspaceItem })
        {
            return;
        }

        if (!workspaceItem.CanOpen)
        {
            return;
        }

        await OpenRecentWorkspaceAsync(workspaceItem);
    }

    private async void OnRecentWorkspacePinButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HomeRecentWorkspaceItemViewModel workspaceItem })
        {
            return;
        }

        var setPinnedResult = await App.CurrentApp.WorkspaceCatalogGateway.SetWorkspaceEntryPinnedStateAsync(
            new SetWorkspaceCatalogEntryPinnedStateCommand(
                workspaceItem.WorkspaceKey,
                !workspaceItem.IsPinned));

        if (!setPinnedResult.IsSuccess)
        {
            App.CurrentApp.AppLogService.AppendError(UiText.Get(ResourceKeys.HomePagePinWorkspaceFailedMessage));
            ShowNotification(
                ResourceKeys.AppNotificationErrorTitle,
                ResourceKeys.HomePagePinWorkspaceFailedMessage,
                AppNotificationSeverity.Error);
            return;
        }

        ShowNotification(
            ResourceKeys.AppNotificationSuccessTitle,
            workspaceItem.IsPinned
                ? ResourceKeys.HomePagePinWorkspaceUnpinnedMessageFormat
                : ResourceKeys.HomePagePinWorkspacePinnedMessageFormat,
            AppNotificationSeverity.Success,
            workspaceItem.DisplayName);
        App.CurrentApp.WorkspaceContextService.ReloadWorkspaceCatalogEntries();
    }

    private async void OnRecentWorkspaceDeleteButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HomeRecentWorkspaceItemViewModel workspaceItem } ||
            XamlRoot is null)
        {
            return;
        }

        var confirmed = await App.CurrentApp.WorkspaceDialogService.ConfirmDeleteWorkspaceAsync(
            XamlRoot,
            workspaceItem.DisplayName,
            workspaceItem.WorkspaceDataRootPath);

        if (!confirmed)
        {
            return;
        }

        var removeResult = await App.CurrentApp.WorkspaceCatalogGateway.RemoveWorkspaceEntryAsync(
            new RemoveWorkspaceCatalogEntryCommand(workspaceItem.WorkspaceKey));

        if (!removeResult.IsSuccess)
        {
            App.CurrentApp.AppLogService.AppendError(UiText.Get(ResourceKeys.HomePageDeleteWorkspaceFailedMessage));
            ShowNotification(
                ResourceKeys.AppNotificationErrorTitle,
                ResourceKeys.HomePageDeleteWorkspaceFailedMessage,
                AppNotificationSeverity.Error);
            return;
        }

        ShowNotification(
            ResourceKeys.AppNotificationSuccessTitle,
            ResourceKeys.HomePageDeleteWorkspaceSucceededMessageFormat,
            AppNotificationSeverity.Success,
            workspaceItem.DisplayName);
        ClearDeletedCurrentWorkspaceRegistration(workspaceItem);
        App.CurrentApp.WorkspaceContextService.ReloadWorkspaceCatalogEntries();
    }

    /// <summary>
    /// 控制台中的最近工作区入口遵循用户偏好设置。
    /// 默认当前窗口、新建窗口或每次询问三种策略都在这里统一收敛。
    /// </summary>
    private async Task OpenRecentWorkspaceAsync(HomeRecentWorkspaceItemViewModel workspaceItem)
    {
        if (!workspaceItem.CanOpen)
        {
            return;
        }

        var request = new WorkspaceNavigationRequest(
            SourcePath: workspaceItem.SourcePath,
            WorkspaceDisplayName: workspaceItem.DisplayName,
            WorkspaceDataRootPath: workspaceItem.WorkspaceDataRootPath,
            OpenTarget: WorkspaceOpenTarget.CurrentWindow);
        var openTarget = await ResolveRecentWorkspaceOpenTargetAsync(request);
        if (!openTarget.HasValue)
        {
            return;
        }

        OpenWorkspace(request with { OpenTarget = openTarget.Value });
    }

    /// <summary>
    /// “每次询问”并不意味着任何场景都必须弹框。
    /// 若当前窗口尚未加载工作区，或当前窗口本来就承载着目标工作区，
    /// 这两种场景的结果都应当稳定落到“当前窗口打开”，无需再向用户重复确认。
    /// </summary>
    private async Task<WorkspaceOpenTarget?> ResolveRecentWorkspaceOpenTargetAsync(WorkspaceNavigationRequest navigationRequest)
    {
        if (TryGetHostWindow(out var hostWindow) &&
            (App.CurrentApp.ShouldReuseCurrentWindowForFirstWorkspace(hostWindow) ||
             App.CurrentApp.DoesWindowHostWorkspace(hostWindow, navigationRequest)))
        {
            return WorkspaceOpenTarget.CurrentWindow;
        }

        return App.CurrentApp.AppPreferencesService.RecentWorkspaceOpenBehavior switch
        {
            RecentWorkspaceOpenBehavior.NewWindow => WorkspaceOpenTarget.NewWindow,
            RecentWorkspaceOpenBehavior.AskEveryTime when XamlRoot is not null =>
                await App.CurrentApp.WorkspaceDialogService.RequestWorkspaceOpenTargetAsync(
                    XamlRoot,
                    navigationRequest.WorkspaceDisplayName),
            RecentWorkspaceOpenBehavior.AskEveryTime => null,
            _ => WorkspaceOpenTarget.CurrentWindow
        };
    }

    /// <summary>
    /// 根据用户选择决定在当前窗口跳转，还是派生一个新的应用进程。
    /// 若目标工作区已在任一已运行窗口中承载，则优先激活已有实例，避免重复打开同一工作区。
    /// </summary>
    private void OpenWorkspace(WorkspaceNavigationRequest? request)
    {
        if (request is null)
        {
            return;
        }

        if (!request.ForceNewWorkspaceSession &&
            App.CurrentApp.TryActivateOpenedWorkspace(request))
        {
            return;
        }

        // 控制台首次进入工作区时，即便用户偏好是“新建窗口”，
        // 只要当前窗口还是空白态，就优先直接复用当前窗口进入工作台。
        if (request.OpenTarget == WorkspaceOpenTarget.NewWindow &&
            TryGetHostWindow(out var hostWindow) &&
            App.CurrentApp.ShouldReuseCurrentWindowForFirstWorkspace(hostWindow))
        {
            request = request with { OpenTarget = WorkspaceOpenTarget.CurrentWindow };
        }

        if (request.OpenTarget == WorkspaceOpenTarget.NewWindow)
        {
            App.CurrentApp.WorkspaceWindowService.OpenWorkspaceInNewWindow(request);
            return;
        }

        if (TryNavigateFromShell(ShellNavigationKeys.Workbench, request))
        {
            return;
        }

        Frame?.Navigate(typeof(BundleExplorerPage), request);
    }

    private bool TryNavigateFromShell(string navigationKey, object? parameter = null)
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is ShellPage shellPage)
            {
                shellPage.NavigateToSection(navigationKey, parameter);
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool TryGetHostWindow([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssestBundleTools.Presentation.WinUI.MainWindow? hostWindow)
    {
        hostWindow = null;
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is ShellPage shellPage && shellPage.HostWindow is not null)
            {
                hostWindow = shellPage.HostWindow;
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void ClearDeletedCurrentWorkspaceRegistration(HomeRecentWorkspaceItemViewModel workspaceItem)
    {
        var request = new WorkspaceNavigationRequest(
            SourcePath: workspaceItem.SourcePath,
            WorkspaceDisplayName: workspaceItem.DisplayName,
            WorkspaceDataRootPath: workspaceItem.WorkspaceDataRootPath,
            OpenTarget: WorkspaceOpenTarget.CurrentWindow);

        if (TryGetHostWindow(out var hostWindow) &&
            App.CurrentApp.DoesWindowHostWorkspace(hostWindow, request))
        {
            hostWindow.ClearRegisteredWorkspace();
        }
    }

    private void ApplyLocalizedText()
    {
        PageTitleText.Text = UiText.Get(ResourceKeys.HomePageTitle);
        QuickActionsTitleText.Text = UiText.Get(ResourceKeys.HomePageQuickActionsTitle);
        QuickActionHintText.Text = UiText.Get(ResourceKeys.HomePageQuickActionHint);
        NewWorkspaceButtonText.Text = UiText.Get(ResourceKeys.HomePageNewWorkspaceButton);
        OpenWorkspaceButtonText.Text = UiText.Get(ResourceKeys.HomePageOpenWorkspaceButton);
        OpenLatestWorkspaceButtonText.Text = UiText.Get(ResourceKeys.HomePageOpenLatestWorkspaceButton);
        RecentOpenedTitleText.Text = UiText.Get(ResourceKeys.HomePageRecentOpenedTitle);
        RecentWorkspaceEmptyText.Text = UiText.Get(ResourceKeys.HomePageRecentWorkspaceEmptyState);
        RecentActivityTitleText.Text = UiText.Get(ResourceKeys.HomePageRecentActivityTitle);
        RecentActivityEmptyText.Text = UiText.Get(ResourceKeys.HomePageRecentActivityEmptyState);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        ViewModel.Dispose();
        Unloaded -= OnUnloaded;
    }

    /// <summary>
    /// 控制台页只把“无需进一步确认”的结果反馈提升为应用级 toast。
    /// 阻断性确认和表单校验仍保留在原有对话框里处理。
    /// </summary>
    private void ShowNotification(
        string titleResourceKey,
        string messageResourceKey,
        AppNotificationSeverity severity,
        params object[] formatArguments)
    {
        var title = UiText.Get(titleResourceKey);
        var message = formatArguments.Length == 0
            ? UiText.Get(messageResourceKey)
            : App.CurrentApp.LocalizationService.GetFormattedString(messageResourceKey, formatArguments);

        App.CurrentApp.AppNotificationService.Show(title, message, severity);
    }
}

