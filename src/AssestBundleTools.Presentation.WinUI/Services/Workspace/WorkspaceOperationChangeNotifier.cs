using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 轻量事件总线，仅用于提示页面级读模型需要重新读取。
/// </summary>
public sealed class WorkspaceOperationChangeNotifier : IWorkspaceOperationChangeNotifier
{
    public event EventHandler<WorkspaceOperationChangedEventArgs>? WorkspaceOperationsChanged;

    public void NotifyWorkspaceOperationsChanged(
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind)
    {
        if (changeKind == WorkspaceOperationChangeKind.None)
        {
            return;
        }

        WorkspaceOperationsChanged?.Invoke(
            this,
            new WorkspaceOperationChangedEventArgs(workspaceId, changeKind));
    }
}
