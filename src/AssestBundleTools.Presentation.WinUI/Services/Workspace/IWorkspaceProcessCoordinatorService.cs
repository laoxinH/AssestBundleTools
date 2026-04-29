namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 负责协调“一个工作区由哪个应用进程承载”这一层的进程级行为。
/// </summary>
/// <remarks>
/// 该服务只处理进程启动、实例注册与跨进程激活，
/// 不直接感知具体页面、ViewModel 或工作区业务逻辑。
/// </remarks>
public interface IWorkspaceProcessCoordinatorService : IDisposable
{
    event EventHandler<WorkspaceProcessActivationRequestedEventArgs>? WorkspaceActivationRequested;

    bool TryRedirectLaunchToExistingProcess(ShellLaunchRequest? launchRequest);

    /// <summary>
    /// 尝试激活其他进程中已经承载的目标工作区。
    /// 当前进程内已打开工作区的窗口复用由 <c>App</c> 层统一处理，
    /// 以便同时掌握窗口集合、前台激活和 Shell 导航。
    /// </summary>
    bool TryActivateExistingWorkspace(WorkspaceNavigationRequest navigationRequest);

    /// <summary>
    /// 在工作区真正完成页面绑定前，先为目标工作区建立一次临时预登记。
    /// 这样其他进程在加载窗口期间再次打开同一工作区时，也能命中既有实例并被重定向。
    /// </summary>
    bool TryBeginPendingWorkspaceRegistration(WorkspaceNavigationRequest navigationRequest);

    void RegisterWorkspace(WorkspaceNavigationRequest navigationRequest);

    void ClearPendingWorkspaceRegistration();

    void ClearWorkspaceRegistration();

    void StartNewProcess(ShellLaunchRequest launchRequest);
}
