using AssestBundleTools.Presentation.WinUI.ViewModels.Shell;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 基于主应用窗口创建能力，在新窗口中承载工作区打开请求。
/// </summary>
public sealed class WorkspaceWindowService : IWorkspaceWindowService
{
    public void OpenWorkspaceInNewWindow(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);

        // 新建窗口模式下先尝试复用已经打开该工作区的窗口，
        // 避免同一工作区被重复派生出多个主窗口，降低状态分叉风险。
        if (App.CurrentApp.TryActivateWindowForWorkspace(navigationRequest))
        {
            return;
        }

        var launchRequest = new ShellLaunchRequest(
            NavigationKey: ShellNavigationKeys.Workbench,
            NavigationParameter: navigationRequest);

        var window = App.CurrentApp.CreateMainWindow(launchRequest);
        window.Activate();
    }
}
