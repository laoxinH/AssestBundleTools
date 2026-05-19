namespace AssestBundleTools.Application.Workspace.Services;

public sealed class WorkspaceCacheChangedEventArgs : EventArgs
{
    public WorkspaceCacheChangedEventArgs(string workspaceKey)
    {
        WorkspaceKey = workspaceKey ?? throw new ArgumentNullException(nameof(workspaceKey));
    }

    public string WorkspaceKey { get; }
}
