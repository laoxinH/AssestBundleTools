using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Home;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views;

/// <summary>
/// 应用控制台页，负责承载最近工作区和工作区入口。
/// </summary>
/// <remarks>
/// 该页在窗口处于控制台态时，作为一级入口承载“新建 / 打开 / 恢复工作区”等操作。
/// 当前实现允许从正式工作台返回这里重新整理工作区入口，但不会清空现有活动工作区上下文。
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

        var setPinnedResult = await App.CurrentApp.WorkspaceCatalogService.SetWorkspaceEntryPinnedStateAsync(
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

        var removeResult = await App.CurrentApp.WorkspaceCatalogService.RemoveWorkspaceEntryAsync(
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
        App.CurrentApp.WorkspaceContextService.ReloadWorkspaceCatalogEntries();
    }

    /// <summary>
    /// 控制台中的最近工作区入口遵循用户偏好设置：
    /// 默认当前窗口、新建窗口或每次询问三种策略都在这里统一收敛。
    /// </summary>
    private async Task OpenRecentWorkspaceAsync(HomeRecentWorkspaceItemViewModel workspaceItem)
    {
        if (!workspaceItem.CanOpen)
        {
            return;
        }

        var openTarget = await ResolveRecentWorkspaceOpenTargetAsync(workspaceItem.DisplayName);
        if (!openTarget.HasValue)
        {
            return;
        }

        OpenWorkspace(new WorkspaceNavigationRequest(
            SourcePath: workspaceItem.SourcePath,
            WorkspaceDisplayName: workspaceItem.DisplayName,
            WorkspaceDataRootPath: workspaceItem.WorkspaceDataRootPath,
            OpenTarget: openTarget.Value));
    }

    private async Task<WorkspaceOpenTarget?> ResolveRecentWorkspaceOpenTargetAsync(string workspaceDisplayName)
    {
        return App.CurrentApp.AppPreferencesService.RecentWorkspaceOpenBehavior switch
        {
            RecentWorkspaceOpenBehavior.NewWindow => WorkspaceOpenTarget.NewWindow,
            RecentWorkspaceOpenBehavior.AskEveryTime when XamlRoot is not null =>
                await App.CurrentApp.WorkspaceDialogService.RequestWorkspaceOpenTargetAsync(
                    XamlRoot,
                    workspaceDisplayName),
            RecentWorkspaceOpenBehavior.AskEveryTime => null,
            _ => WorkspaceOpenTarget.CurrentWindow
        };
    }

    /// <summary>
    /// 根据用户选择决定在当前窗口跳转，还是派生一个新的主窗口。
    /// 当前窗口打开时会优先走 Shell 级导航，保持侧边栏选中状态同步。
    /// </summary>
    private void OpenWorkspace(WorkspaceNavigationRequest? request)
    {
        if (request is null)
        {
            return;
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

    private void ApplyLocalizedText()
    {
        PageBadgeText.Text = UiText.Get(ResourceKeys.HomePageBadgeText);
        PageTitleText.Text = UiText.Get(ResourceKeys.HomePageTitle);
        PageSubtitleText.Text = UiText.Get(ResourceKeys.HomePageSubtitle);
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
