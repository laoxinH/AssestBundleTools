using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 负责把“新窗口打开工作区”转换为“新应用进程承载工作区”。
/// </summary>
public sealed class WorkspaceWindowService : IWorkspaceWindowService
{
    private readonly IWorkspaceProcessCoordinatorService _workspaceProcessCoordinatorService;

    public WorkspaceWindowService(IWorkspaceProcessCoordinatorService workspaceProcessCoordinatorService)
    {
        _workspaceProcessCoordinatorService = workspaceProcessCoordinatorService
            ?? throw new ArgumentNullException(nameof(workspaceProcessCoordinatorService));
    }

    public void OpenWorkspaceInNewWindow(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);

        // “新窗口”现在代表“必要时派生新主进程”，
        // 但如果目标工作区已经在任一运行中的窗口里承载，则仍应优先激活既有实例。
        if (App.CurrentApp.TryActivateOpenedWorkspace(navigationRequest))
        {
            return;
        }

        var launchRequest = new ShellLaunchRequest(
            NavigationKey: ShellNavigationKeys.Workbench,
            NavigationParameter: navigationRequest with
            {
                OpenTarget = WorkspaceOpenTarget.CurrentWindow
            });

        _workspaceProcessCoordinatorService.StartNewProcess(launchRequest);
    }
}