using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 在 Presentation 层广播工作区操作完成后的读模型失效事件。
/// </summary>
public interface IWorkspaceOperationChangeNotifier
{
    event EventHandler<WorkspaceOperationChangedEventArgs>? WorkspaceOperationsChanged;

    void NotifyWorkspaceOperationsChanged(
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind);
}

[Flags]
public enum WorkspaceOperationChangeKind
{
    None = 0,
    Extraction = 1,
    Replacement = 2,
    Save = 4
}

public sealed class WorkspaceOperationChangedEventArgs : EventArgs
{
    public WorkspaceOperationChangedEventArgs(
        WorkspaceId workspaceId,
        WorkspaceOperationChangeKind changeKind)
    {
        WorkspaceId = workspaceId;
        ChangeKind = changeKind;
    }

    public WorkspaceId WorkspaceId { get; }

    public WorkspaceOperationChangeKind ChangeKind { get; }
}
