namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示其他进程请求当前进程激活某个工作区窗口。
/// </summary>
public sealed class WorkspaceProcessActivationRequestedEventArgs : EventArgs
{
    public WorkspaceProcessActivationRequestedEventArgs(WorkspaceNavigationRequest navigationRequest)
    {
        NavigationRequest = navigationRequest ?? throw new ArgumentNullException(nameof(navigationRequest));
    }

    public WorkspaceNavigationRequest NavigationRequest { get; }
}
